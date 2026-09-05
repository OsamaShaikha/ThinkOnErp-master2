using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class GlAccountsControllerTests
{
    private readonly Mock<IGlAccountService> _service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetTree_WhenServiceReturnsTree_ReturnsSuccessfulEnvelope()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        IReadOnlyList<GlAccountTreeDto> tree = new[]
        {
            new GlAccountTreeDto
            {
                AccountCode = "1",
                AccountNameLocal = "الأصول",
                AccountNameEn = "Assets",
                AccountType = "HEADER",
                IsActive = true
            }
        };
        _service
            .Setup(service => service.GetTreeAsync(cancellationToken))
            .ReturnsAsync(tree);

        var action = await CreateController().GetTree(cancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<List<GlAccountTreeDto>>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("1", Assert.Single(response.Data!).AccountCode);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Create_WhenRequestIsValid_Returns201AndForwardsRequest()
    {
        var request = new CreateGlAccountDto
        {
            AccountCode = "111101",
            AccountNameLocal = "الصندوق الرئيسي",
            AccountNameEn = "Main cash",
            ParentAccountCode = "1111",
            AccountType = "DETAIL",
            NormalBalance = "D"
        };
        var created = new GlAccountDto
        {
            AccountCode = request.AccountCode,
            AccountNameLocal = request.AccountNameLocal,
            AccountNameEn = request.AccountNameEn,
            AccountType = request.AccountType,
            NormalBalance = request.NormalBalance,
            IsActive = true,
            IsPostable = true
        };
        _service
            .Setup(service => service.CreateAccountAsync(request, CancellationToken.None))
            .ReturnsAsync(created);

        var action = await CreateController().Create(request, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(201, result.StatusCode);
        var response = Assert.IsType<ApiResponse<GlAccountDto>>(result.Value);
        Assert.True(response.Success);
        Assert.Equal("111101", response.Data!.AccountCode);
        _service.VerifyAll();
    }

    [Fact]
    public async Task GetByCode_WhenAccountExists_ReturnsSuccessfulEnvelope()
    {
        var account = new GlAccountDto
        {
            AccountCode = "111101",
            AccountNameLocal = "الصندوق الرئيسي",
            AccountNameEn = "Main cash"
        };
        _service
            .Setup(service => service.GetAccountByCodeAsync("111101", CancellationToken.None))
            .ReturnsAsync(account);

        var action = await CreateController().GetByCode("111101", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<GlAccountDto>>(ok.Value);
        Assert.Equal("111101", response.Data!.AccountCode);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Update_WhenRequestIsValid_ReturnsUpdatedAccount()
    {
        var request = new UpdateGlAccountDto
        {
            AccountNameLocal = "الصندوق - عمان",
            AccountNameEn = "Cash - Amman"
        };
        var updated = new GlAccountDto
        {
            AccountCode = "111101",
            AccountNameLocal = request.AccountNameLocal,
            AccountNameEn = request.AccountNameEn
        };
        _service
            .Setup(service => service.UpdateAccountAsync(
                "111101",
                request,
                CancellationToken.None))
            .ReturnsAsync(updated);

        var action = await CreateController().Update(
            "111101",
            request,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<GlAccountDto>>(ok.Value);
        Assert.Equal("الصندوق - عمان", response.Data!.AccountNameLocal);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Update_WhenRequestIsMissing_Returns400WithoutCallingService()
    {
        var action = await CreateController().Update(
            "111101",
            null,
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<GlAccountDto>>(badRequest.Value);
        Assert.False(response.Success);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_WhenServiceDeletesAccount_ReturnsDeletedSnapshot()
    {
        var deleted = new GlAccountDto
        {
            AccountCode = "111101",
            AccountNameLocal = "الصندوق الرئيسي",
            AccountNameEn = "Main cash"
        };
        _service
            .Setup(service => service.DeleteAccountAsync("111101", CancellationToken.None))
            .ReturnsAsync(deleted);

        var action = await CreateController().Delete("111101", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<GlAccountDto>>(ok.Value);
        Assert.Equal("111101", response.Data!.AccountCode);
        _service.VerifyAll();
    }

    [Fact]
    public async Task UpdateStatus_WhenIsActiveIsMissing_Returns400WithoutCallingService()
    {
        var action = await CreateController().UpdateStatus(
            "111101",
            new UpdateGlAccountStatusDto(),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<GlAccountStatusDto>>(badRequest.Value);
        Assert.False(response.Success);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public void Controller_UsesTenantScopeAndTenantAdminPolicyForMutations()
    {
        var controllerType = typeof(GlAccountsController);

        Assert.NotNull(Attribute.GetCustomAttribute(controllerType, typeof(TenantScopedAttribute)));
        Assert.Contains(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>(),
            attribute => string.IsNullOrEmpty(attribute.Policy));

        Assert.Equal(
            "TenantAdminOnly",
            controllerType.GetMethod(nameof(GlAccountsController.Create))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Single()
                .Policy);
        Assert.Equal(
            "TenantAdminOnly",
            controllerType.GetMethod(nameof(GlAccountsController.UpdateStatus))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Single()
                .Policy);
        Assert.Equal(
            "TenantAdminOnly",
            controllerType.GetMethod(nameof(GlAccountsController.Update))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Single()
                .Policy);
        Assert.Equal(
            "TenantAdminOnly",
            controllerType.GetMethod(nameof(GlAccountsController.Delete))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Single()
                .Policy);
    }

    private GlAccountsController CreateController() => new(
        _service.Object,
        Mock.Of<ILogger<GlAccountsController>>());
}

