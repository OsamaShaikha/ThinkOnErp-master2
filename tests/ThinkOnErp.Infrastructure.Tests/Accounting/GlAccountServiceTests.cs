using Moq;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class GlAccountServiceTests
{
    private const long TenantCompanyId = 42;

    private readonly Mock<IGlAccountRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<ICurrentTenantContext> _tenantContext = new(MockBehavior.Strict);

    public GlAccountServiceTests()
    {
        _tenantContext
            .Setup(context => context.GetRequiredCompanyId())
            .Returns(TenantCompanyId);
    }

    [Fact]
    public async Task GetTreeAsync_BuildsSortedHierarchyForCurrentTenant()
    {
        IReadOnlyList<GlAccount> accounts = new[]
        {
            Account(4, "111101", 3, 5, "DETAIL"),
            Account(2, "12", 1, 2, "HEADER"),
            Account(1, "1", null, 1, "HEADER"),
            Account(3, "11", 1, 2, "HEADER")
        };
        _repository
            .Setup(repository => repository.GetAllAsync(TenantCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);

        var result = await CreateService().GetTreeAsync();

        var root = Assert.Single(result);
        Assert.Equal("1", root.AccountCode);
        Assert.Equal(new[] { "11", "12" }, root.Children.Select(child => child.AccountCode));
        Assert.Equal("111101", Assert.Single(root.Children[0].Children).AccountCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task GetPostableAccountsAsync_UsesTenantAndBranchScope()
    {
        const long branchId = 9;
        IReadOnlyList<GlAccount> postable = new[]
        {
            Account(7, "111101", 6, 5, "DETAIL")
        };
        _repository
            .Setup(repository => repository.BranchBelongsToCompanyAsync(
                TenantCompanyId,
                branchId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository
            .Setup(repository => repository.GetPostableAsync(
                TenantCompanyId,
                branchId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(postable);

        var result = await CreateService().GetPostableAccountsAsync(branchId);

        var account = Assert.Single(result);
        Assert.Equal("111101", account.AccountCode);
        Assert.True(account.IsPostable);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task GetAccountAsync_UsesCurrentTenantAndMapsAccount()
    {
        var account = Account(17, "111101", 16, 5, "DETAIL");
        account.Category = new AccountCategory
        {
            Id = 10,
            CategoryCode = 1,
            NameAr = "الأصول",
            NameEn = "Assets",
            NormalBalance = "D",
            FinancialStatement = "BALANCE_SHEET"
        };
        account.BranchLinks.Add(new GlAccountBranch
        {
            GlAccountId = account.Id,
            BranchId = 9,
            IsActive = true
        });
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await CreateService().GetAccountAsync(account.Id);

        Assert.Equal(account.Id, result.Id);
        Assert.Equal("Assets", result.CategoryNameEn);
        Assert.Equal(new long[] { 9 }, result.BranchIds);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task GetAccountAsync_WhenAccountDoesNotExist_ThrowsNotFound()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                404,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlAccount?)null);

        var exception = await Assert.ThrowsAsync<AccountingNotFoundException>(
            () => CreateService().GetAccountAsync(404));

        Assert.Equal("GL_ACCOUNT_NOT_FOUND", exception.ErrorCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task GetCategoriesAsync_MapsRepositoryCategories()
    {
        IReadOnlyList<AccountCategory> categories = new[]
        {
            new AccountCategory
            {
                Id = 1,
                CategoryCode = 1,
                NameAr = "الأصول",
                NameEn = "Assets",
                NormalBalance = "D",
                FinancialStatement = "BALANCE_SHEET",
                DisplayOrder = 1
            }
        };
        _repository
            .Setup(repository => repository.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        var result = await CreateService().GetCategoriesAsync();

        var category = Assert.Single(result);
        Assert.Equal(1, category.CategoryCode);
        Assert.Equal("BALANCE_SHEET", category.FinancialStatement);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task CreateAccountAsync_AssignsCurrentTenantAndValidatedParent()
    {
        var category = new AccountCategory
        {
            Id = 10,
            CategoryCode = 1,
            NameAr = "الأصول",
            NameEn = "Assets",
            NormalBalance = "D",
            FinancialStatement = "BALANCE_SHEET"
        };
        var parent = Account(70, "1111", 60, 4, "HEADER");
        parent.CategoryId = category.Id;
        parent.Category = category;

        GlAccount? addedAccount = null;
        _repository
            .Setup(repository => repository.AccountCodeExistsAsync(
                TenantCompanyId,
                "111101",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(repository => repository.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { category });
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                parent.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _repository
            .Setup(repository => repository.AddAsync(
                It.IsAny<GlAccount>(),
                It.IsAny<CancellationToken>()))
            .Callback<GlAccount, CancellationToken>((account, _) =>
            {
                addedAccount = account;
                account.Id = 71;
            })
            .Returns(Task.CompletedTask);
        _repository
            .Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await CreateService().CreateAccountAsync(new CreateGlAccountDto
        {
            AccountCode = "111101",
            AccountNameAr = "الصندوق الرئيسي",
            AccountNameEn = "Main cash",
            ParentAccountId = parent.Id,
            CategoryId = category.Id,
            AccountType = "detail",
            NormalBalance = "d"
        });

        Assert.NotNull(addedAccount);
        Assert.Equal(TenantCompanyId, addedAccount.CompanyId);
        Assert.Equal(parent.Id, addedAccount.ParentAccountId);
        Assert.Equal(5, addedAccount.AccountLevel);
        Assert.Equal("DETAIL", addedAccount.AccountType);
        Assert.Equal(71, result.Id);
        Assert.True(result.IsPostable);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task UpdateAccountAsync_UpdatesEditableFieldsAndSynchronizesBranches()
    {
        var account = Account(71, "111101", 70, 5, "DETAIL");
        account.BranchLinks.Add(new GlAccountBranch
        {
            GlAccountId = account.Id,
            BranchId = 9,
            IsActive = true
        });
        account.BranchLinks.Add(new GlAccountBranch
        {
            GlAccountId = account.Id,
            BranchId = 10,
            IsActive = false
        });
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _repository
            .Setup(repository => repository.BranchBelongsToCompanyAsync(
                TenantCompanyId,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository
            .Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await CreateService().UpdateAccountAsync(
            account.Id,
            new UpdateGlAccountDto
            {
                AccountNameAr = "  حساب محدث  ",
                AccountNameEn = "  Updated account  ",
                IsContra = true,
                IsBranchSpecific = true,
                Description = " Updated description ",
                BranchIds = new List<long> { 10, 11, 11 }
            });

        Assert.Equal("111101", result.AccountCode);
        Assert.Equal(70, result.ParentAccountId);
        Assert.Equal("DETAIL", result.AccountType);
        Assert.Equal("حساب محدث", result.AccountNameAr);
        Assert.Equal("Updated account", result.AccountNameEn);
        Assert.True(result.IsContra);
        Assert.Equal(new long[] { 10, 11 }, result.BranchIds);
        Assert.False(account.BranchLinks.Single(link => link.BranchId == 9).IsActive);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task DeleteAccountAsync_WhenAccountHasChildren_ThrowsConflict()
    {
        var account = Account(71, "1111", 70, 4, "HEADER");
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _repository
            .Setup(repository => repository.HasChildrenAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<AccountingConflictException>(
            () => CreateService().DeleteAccountAsync(account.Id));

        Assert.Equal("GL_ACCOUNT_HAS_CHILDREN", exception.ErrorCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task DeleteAccountAsync_WhenAccountIsRoot_ThrowsConflict()
    {
        var account = Account(1, "1", null, 1, "HEADER");
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var exception = await Assert.ThrowsAsync<AccountingConflictException>(
            () => CreateService().DeleteAccountAsync(account.Id));

        Assert.Equal("GL_ROOT_ACCOUNT_DELETE_FORBIDDEN", exception.ErrorCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task DeleteAccountAsync_WhenLeafAccountExists_DeletesAndReturnsSnapshot()
    {
        var account = Account(71, "111101", 70, 5, "DETAIL");
        _repository
            .Setup(repository => repository.GetByIdAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _repository
            .Setup(repository => repository.HasChildrenAsync(
                TenantCompanyId,
                account.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(repository => repository.DeleteAsync(
                account,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().DeleteAccountAsync(account.Id);

        Assert.Equal(account.Id, result.Id);
        Assert.Equal(account.AccountCode, result.AccountCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    private GlAccountService CreateService() => new(_repository.Object, _tenantContext.Object);

    private static GlAccount Account(
        long id,
        string code,
        long? parentId,
        int level,
        string accountType) => new()
        {
            Id = id,
            CompanyId = TenantCompanyId,
            AccountCode = code,
            AccountNameAr = $"حساب {code}",
            AccountNameEn = $"Account {code}",
            ParentAccountId = parentId,
            CategoryId = 10,
            AccountLevel = level,
            AccountType = accountType,
            NormalBalance = "D",
            IsActive = true
        };
}
