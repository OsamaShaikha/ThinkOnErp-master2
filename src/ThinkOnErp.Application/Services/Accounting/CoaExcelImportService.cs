using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class CoaExcelImportService : ICoaExcelImportService
{
    private const int ExpectedAccountCount = 315;

    private static readonly IReadOnlyDictionary<string, CategoryDefinition> CategoryDefinitions =
        new Dictionary<string, CategoryDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["ASSETS"] = new(1, "D", "BALANCE_SHEET"),
            ["LIABILITIES"] = new(2, "C", "BALANCE_SHEET"),
            ["EQUITY"] = new(3, "C", "BALANCE_SHEET"),
            ["REVENUE"] = new(4, "C", "INCOME_STATEMENT"),
            ["COGS"] = new(5, "D", "INCOME_STATEMENT"),
            ["OPEX"] = new(6, "D", "INCOME_STATEMENT"),
            ["OTHER_INCOME"] = new(7, "C", "INCOME_STATEMENT"),
            ["OTHER_EXPENSE"] = new(8, "D", "INCOME_STATEMENT")
        };

    private static readonly HashSet<string> ControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AR",
        "AP",
        "INVENTORY"
    };

    private readonly ICoaWorkbookReader _workbookReader;
    private readonly IGlAccountRepository _repository;
    private readonly ICurrentTenantContext _tenantContext;

    public CoaExcelImportService(
        ICoaWorkbookReader workbookReader,
        IGlAccountRepository repository,
        ICurrentTenantContext tenantContext)
    {
        _workbookReader = workbookReader;
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<CoaImportResultDto> ValidateAsync(
        Stream workbook,
        CancellationToken cancellationToken = default)
    {
        var (rows, result) = await ReadAndValidateAsync(workbook, cancellationToken);
        result.TotalRows = rows.Count;
        return result;
    }

    public async Task<CoaImportResultDto> ImportAsync(
        Stream workbook,
        long defaultBranchId,
        CancellationToken cancellationToken = default)
    {
        var (rows, result) = await ReadAndValidateAsync(workbook, cancellationToken);
        result.TotalRows = rows.Count;
        if (!result.IsValid)
        {
            return result;
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        if (defaultBranchId <= 0 ||
            !await _repository.BranchBelongsToCompanyAsync(
                companyId,
                defaultBranchId,
                cancellationToken))
        {
            result.Errors.Add(new CoaImportErrorDto(
                null,
                "defaultBranchId",
                "COA_BRANCH_NOT_FOUND",
                "The default branch does not belong to the current company or is inactive."));
            return result;
        }

        if (await _repository.HasAccountsAsync(companyId, cancellationToken))
        {
            result.Errors.Add(new CoaImportErrorDto(
                null,
                null,
                "COA_ALREADY_INITIALIZED",
                "The current company already has a chart of accounts; the standard import is only allowed on an empty chart."));
            return result;
        }

        var accountsByCode = new Dictionary<string, GlAccount>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var accountCode = Normalize(row.AccountCode);
            var account = new GlAccount
            {
                AccountCode = accountCode,
                OldAccountCode = NullIfWhiteSpace(row.OldAccountCode),
                AccountNameLocal = row.AccountNameLocal.Trim(),
                AccountNameEn = row.AccountNameEn.Trim(),
                ParentAccountCode = NullIfWhiteSpace(row.ParentCode),
                AccountLevel = row.AccountLevel,
                AccountType = Normalize(row.AccountType),
                NormalBalance = Normalize(row.NormalBalance),
                IsContra = row.IsContra,
                IsControlAccount = row.IsControlAccount,
                ControlAccountType = NullIfWhiteSpace(row.ControlAccountType)?.ToUpperInvariant(),
                IsBranchSpecific = row.IsBranchSpecific,
                IsClearing = row.IsClearing,
                IsActive = true,
                Notes = NullIfWhiteSpace(row.Notes)
            };

            if (account.IsBranchSpecific)
            {
                account.BranchLinks.Add(new GlAccountBranch
                {
                    AccountCode = accountCode,
                    BranchId = defaultBranchId,
                    IsActive = true
                });
            }

            accountsByCode.Add(account.AccountCode, account);
        }

        await _repository.ImportAsync(accountsByCode.Values.ToList(), cancellationToken);
        result.ImportedCount = accountsByCode.Count;
        return result;

    }

    private async Task<(IReadOnlyList<CoaImportRowDto> Rows, CoaImportResultDto Result)>
        ReadAndValidateAsync(Stream workbook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (!workbook.CanRead)
        {
            throw new ArgumentException("The workbook stream must be readable.", nameof(workbook));
        }

        var readResult = await _workbookReader.ReadAsync(workbook, cancellationToken);
        var result = new CoaImportResultDto
        {
            TotalRows = readResult.Rows.Count,
            Errors = readResult.Errors.ToList()
        };

        if (readResult.Errors.Count == 0)
        {
            ValidateRows(readResult.Rows, result.Errors);
        }

        return (readResult.Rows, result);
    }

    private static void ValidateRows(
        IReadOnlyList<CoaImportRowDto> rows,
        ICollection<CoaImportErrorDto> errors)
    {
        if (rows.Count != ExpectedAccountCount)
        {
            AddError(
                errors,
                null,
                null,
                "COA_ACCOUNT_COUNT",
                $"The approved workbook must contain exactly {ExpectedAccountCount} accounts; found {rows.Count}.");
        }

        var rowsByCode = new Dictionary<string, CoaImportRowDto>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var code = Normalize(row.AccountCode);
            if (string.IsNullOrEmpty(code))
            {
                AddError(errors, row.RowNumber, "account_code", "COA_CODE_REQUIRED", "Account code is required.");
            }
            else if (!code.All(char.IsAsciiDigit))
            {
                AddError(errors, row.RowNumber, "account_code", "COA_CODE_FORMAT", "Account code must contain digits only.");
            }
            else if (!rowsByCode.TryAdd(code, row))
            {
                AddError(errors, row.RowNumber, "account_code", "COA_DUPLICATE_CODE", $"Duplicate account code '{code}'.");
            }
        }

        var childCount = rows
            .Select(row => NullIfWhiteSpace(row.ParentCode))
            .Where(parentCode => parentCode != null)
            .GroupBy(parentCode => parentCode!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var row in rows)
        {
            ValidateRow(row, rowsByCode, childCount, errors);
        }
    }

    private static void ValidateRow(
        CoaImportRowDto row,
        IReadOnlyDictionary<string, CoaImportRowDto> rowsByCode,
        IReadOnlyDictionary<string, int> childCount,
        ICollection<CoaImportErrorDto> errors)
    {
        var code = Normalize(row.AccountCode);
        var parentCode = NullIfWhiteSpace(row.ParentCode);
        var accountType = Normalize(row.AccountType);
        var categoryCode = Normalize(row.CategoryCode);
        var normalBalance = Normalize(row.NormalBalance);
        var financialStatement = Normalize(row.FinancialStatement);
        var controlType = NullIfWhiteSpace(row.ControlAccountType)?.ToUpperInvariant();

        ValidateRequiredText(row.AccountNameLocal, 200, row.RowNumber, "account_name_ar", errors);
        ValidateRequiredText(row.AccountNameEn, 200, row.RowNumber, "account_name_en", errors);
        ValidateOptionalText(row.Notes, 2000, row.RowNumber, "notes", errors);

        if (row.AccountLevel is < 1 or > 5)
        {
            AddError(errors, row.RowNumber, "level", "COA_LEVEL_RANGE", "Level must be from 1 to 5.");
        }
        else if (!string.IsNullOrEmpty(code))
        {
            var expectedLength = row.AccountLevel == 5 ? 6 : row.AccountLevel;
            if (code.Length != expectedLength)
            {
                AddError(
                    errors,
                    row.RowNumber,
                    "account_code",
                    "COA_CODE_LENGTH",
                    $"A level {row.AccountLevel} account code must contain {expectedLength} digits.");
            }
        }

        if (accountType is not ("HEADER" or "DETAIL"))
        {
            AddError(errors, row.RowNumber, "account_type", "COA_ACCOUNT_TYPE", "Account type must be HEADER or DETAIL.");
        }

        if (row.AccountLevel == 5 && accountType != "DETAIL")
        {
            AddError(errors, row.RowNumber, "account_type", "COA_LEVEL5_NOT_DETAIL", "A level-5 account must be DETAIL.");
        }

        if (!CategoryDefinitions.TryGetValue(categoryCode, out var category))
        {
            AddError(errors, row.RowNumber, "category_code", "COA_CATEGORY", $"Unknown category '{categoryCode}'.");
        }
        else
        {
            if (code.Length > 0 && code[0].ToString() != category.NumericCode.ToString())
            {
                AddError(errors, row.RowNumber, "category_code", "COA_CATEGORY_ROOT", "Category does not match the account-code root.");
            }

            if (normalBalance != category.NormalBalance)
            {
                AddError(errors, row.RowNumber, "normal_balance", "COA_NORMAL_BALANCE", "Normal balance does not match the category base balance.");
            }

            if (financialStatement != category.FinancialStatement)
            {
                AddError(errors, row.RowNumber, "financial_statement", "COA_FINANCIAL_STATEMENT", "Financial statement does not match the category.");
            }
        }

        if (row.AccountLevel == 1)
        {
            if (parentCode != null)
            {
                AddError(errors, row.RowNumber, "parent_code", "COA_ROOT_PARENT", "A level-1 account cannot have a parent.");
            }
        }
        else if (parentCode == null)
        {
            AddError(errors, row.RowNumber, "parent_code", "COA_PARENT_REQUIRED", "A non-root account requires a parent.");
        }
        else if (!rowsByCode.TryGetValue(parentCode, out var parent))
        {
            AddError(errors, row.RowNumber, "parent_code", "COA_PARENT_NOT_FOUND", $"Parent '{parentCode}' does not exist.");
        }
        else
        {
            if (row.AccountLevel != parent.AccountLevel + 1)
            {
                AddError(errors, row.RowNumber, "level", "COA_PARENT_LEVEL", "Child level must equal parent level plus one.");
            }

            if (!code.StartsWith(parentCode, StringComparison.Ordinal) || code.Length <= parentCode.Length)
            {
                AddError(errors, row.RowNumber, "account_code", "COA_PARENT_PREFIX", "Account code must extend its parent code.");
            }

            if (!string.Equals(Normalize(parent.AccountType), "HEADER", StringComparison.Ordinal))
            {
                AddError(errors, row.RowNumber, "parent_code", "COA_DETAIL_AS_PARENT", "A DETAIL account cannot be used as a parent.");
            }

            if (!string.Equals(Normalize(parent.CategoryCode), categoryCode, StringComparison.Ordinal))
            {
                AddError(errors, row.RowNumber, "category_code", "COA_PARENT_CATEGORY", "Child category must match its parent category.");
            }
        }

        var hasChildren = childCount.ContainsKey(code);
        if (hasChildren && accountType != "HEADER")
        {
            AddError(errors, row.RowNumber, "account_type", "COA_PARENT_NOT_HEADER", "An account with children must be HEADER.");
        }

        if (!hasChildren && accountType == "HEADER")
        {
            AddError(errors, row.RowNumber, "account_type", "COA_EMPTY_HEADER", "A HEADER account must have at least one child.");
        }

        if (row.IsControlAccount)
        {
            if (controlType == null || !ControlTypes.Contains(controlType))
            {
                AddError(errors, row.RowNumber, "control_account_type", "COA_CONTROL_TYPE", "A control account requires AR, AP, or INVENTORY.");
            }

            if (accountType != "DETAIL")
            {
                AddError(errors, row.RowNumber, "account_type", "COA_CONTROL_NOT_DETAIL", "A control account must be DETAIL.");
            }
        }
        else if (controlType != null)
        {
            AddError(errors, row.RowNumber, "control_account_type", "COA_UNEXPECTED_CONTROL_TYPE", "Control type must be blank when is_control_account is FALSE.");
        }

        if ((row.IsBranchSpecific || row.IsClearing) && accountType != "DETAIL")
        {
            AddError(errors, row.RowNumber, "account_type", "COA_DETAIL_FLAG_ON_HEADER", "Only DETAIL accounts can be branch-specific or clearing accounts.");
        }
    }

    private static void ValidateRequiredText(
        string? value,
        int maxLength,
        int rowNumber,
        string column,
        ICollection<CoaImportErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, rowNumber, column, "COA_REQUIRED_FIELD", $"{column} is required.");
        }
        else if (value.Trim().Length > maxLength)
        {
            AddError(errors, rowNumber, column, "COA_FIELD_TOO_LONG", $"{column} cannot exceed {maxLength} characters.");
        }
    }

    private static void ValidateOptionalText(
        string? value,
        int maxLength,
        int rowNumber,
        string column,
        ICollection<CoaImportErrorDto> errors)
    {
        if (value?.Trim().Length > maxLength)
        {
            AddError(errors, rowNumber, column, "COA_FIELD_TOO_LONG", $"{column} cannot exceed {maxLength} characters.");
        }
    }

    private static void AddError(
        ICollection<CoaImportErrorDto> errors,
        int? rowNumber,
        string? column,
        string code,
        string message) =>
        errors.Add(new CoaImportErrorDto(rowNumber, column, code, message));

    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CategoryDefinition(
        int NumericCode,
        string NormalBalance,
        string FinancialStatement);
}
