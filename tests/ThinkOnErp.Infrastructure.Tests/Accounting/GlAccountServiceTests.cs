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
            Account("111101", "1111", 5, "DETAIL"),
            Account("12", "1", 2, "HEADER"),
            Account("1", null, 1, "HEADER"),
            Account("11", "1", 2, "HEADER"),
            Account("1111", "11", 4, "HEADER")
        };
        _repository
            .Setup(repository => repository.GetAllAsync(TenantCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);

        var result = await CreateService().GetTreeAsync();

        var root = Assert.Single(result);
        Assert.Equal("1", root.AccountCode);
        Assert.Equal(new[] { "11", "12" }, root.Children.Select(child => child.AccountCode));
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task GetPostableAccountsAsync_UsesTenantAndBranchScope()
    {
        const long branchId = 9;
        IReadOnlyList<GlAccount> postable = new[]
        {
            Account("111101", "1111", 5, "DETAIL")
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
    public async Task GetAccountByCodeAsync_UsesCurrentTenantAndMapsAccount()
    {
        var account = Account("111101", "1111", 5, "DETAIL");
        account.BranchLinks.Add(new GlAccountBranch
        {
            CompanyId = TenantCompanyId,
            AccountCode = account.AccountCode,
            BranchId = 9,
            IsActive = true
        });
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                account.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await CreateService().GetAccountByCodeAsync(account.AccountCode);

        Assert.Equal(account.AccountCode, result.AccountCode);
        Assert.Equal("1111", result.ParentAccountCode);
        Assert.Equal(new long[] { 9 }, result.BranchIds);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task GetAccountByCodeAsync_WhenAccountDoesNotExist_ThrowsNotFound()
    {
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                "999999",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlAccount?)null);

        var exception = await Assert.ThrowsAsync<AccountingNotFoundException>(
            () => CreateService().GetAccountByCodeAsync("999999"));

        Assert.Equal("GL_ACCOUNT_NOT_FOUND", exception.ErrorCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task CreateAccountAsync_AssignsCurrentTenantAndValidatedParent()
    {
        var parent = Account("1111", "111", 4, "HEADER");

        GlAccount? addedAccount = null;
        _repository
            .Setup(repository => repository.AccountCodeExistsAsync(
                TenantCompanyId,
                "111101",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                parent.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _repository
            .Setup(repository => repository.AddAsync(
                It.IsAny<GlAccount>(),
                It.IsAny<CancellationToken>()))
            .Callback<GlAccount, CancellationToken>((account, _) =>
            {
                addedAccount = account;
            })
            .Returns(Task.CompletedTask);
        _repository
            .Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await CreateService().CreateAccountAsync(new CreateGlAccountDto
        {
            AccountCode = "111101",
            AccountNameLocal = "الصندوق الرئيسي",
            AccountNameEn = "Main cash",
            ParentAccountCode = parent.AccountCode,
            AccountType = "detail",
            NormalBalance = "d"
        });

        Assert.NotNull(addedAccount);
        Assert.Equal(TenantCompanyId, addedAccount.CompanyId);
        Assert.Equal(parent.AccountCode, addedAccount.ParentAccountCode);
        Assert.Equal(5, addedAccount.AccountLevel);
        Assert.Equal("DETAIL", addedAccount.AccountType);
        Assert.Equal("111101", result.AccountCode);
        Assert.True(result.IsPostable);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task UpdateAccountAsync_UpdatesEditableFieldsAndSynchronizesBranches()
    {
        var account = Account("111101", "1111", 5, "DETAIL");
        account.BranchLinks.Add(new GlAccountBranch
        {
            CompanyId = TenantCompanyId,
            AccountCode = account.AccountCode,
            BranchId = 9,
            IsActive = true
        });
        account.BranchLinks.Add(new GlAccountBranch
        {
            CompanyId = TenantCompanyId,
            AccountCode = account.AccountCode,
            BranchId = 10,
            IsActive = false
        });
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                account.AccountCode,
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
            account.AccountCode,
            new UpdateGlAccountDto
            {
                AccountNameLocal = "  حساب محدث  ",
                AccountNameEn = "  Updated account  ",
                IsContra = true,
                IsBranchSpecific = true,
                Description = " Updated description ",
                BranchIds = new List<long> { 10, 11, 11 }
            });

        Assert.Equal("111101", result.AccountCode);
        Assert.Equal("1111", result.ParentAccountCode);
        Assert.Equal("DETAIL", result.AccountType);
        Assert.Equal("حساب محدث", result.AccountNameLocal);
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
        var account = Account("1111", "111", 4, "HEADER");
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                account.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _repository
            .Setup(repository => repository.HasChildrenAsync(
                TenantCompanyId,
                account.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<AccountingConflictException>(
            () => CreateService().DeleteAccountAsync(account.AccountCode));

        Assert.Equal("GL_ACCOUNT_HAS_CHILDREN", exception.ErrorCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task DeleteAccountAsync_WhenAccountIsRoot_ThrowsConflict()
    {
        var account = Account("1", null, 1, "HEADER");
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                account.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var exception = await Assert.ThrowsAsync<AccountingConflictException>(
            () => CreateService().DeleteAccountAsync(account.AccountCode));

        Assert.Equal("GL_ROOT_ACCOUNT_DELETE_FORBIDDEN", exception.ErrorCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    [Fact]
    public async Task DeleteAccountAsync_WhenLeafAccountExists_DeletesAndReturnsSnapshot()
    {
        var account = Account("111101", "1111", 5, "DETAIL");
        _repository
            .Setup(repository => repository.GetByCodeAsync(
                TenantCompanyId,
                account.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _repository
            .Setup(repository => repository.HasChildrenAsync(
                TenantCompanyId,
                account.AccountCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(repository => repository.DeleteAsync(
                account,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().DeleteAccountAsync(account.AccountCode);

        Assert.Equal(account.AccountCode, result.AccountCode);
        _repository.VerifyAll();
        _tenantContext.VerifyAll();
    }

    private GlAccountService CreateService() => new(_repository.Object, _tenantContext.Object);

    private static GlAccount Account(
        string code,
        string? parentCode,
        int level,
        string accountType) => new()
        {
            CompanyId = TenantCompanyId,
            AccountCode = code,
            AccountNameLocal = $"حساب {code}",
            AccountNameEn = $"Account {code}",
            ParentAccountCode = parentCode,
            AccountLevel = level,
            AccountType = accountType,
            NormalBalance = "D",
            IsActive = true
        };
}

