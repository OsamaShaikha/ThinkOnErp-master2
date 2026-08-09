using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class AccountCategoriesControllerTests
{
    [Fact]
    public async Task GetAll_WhenCategoriesExist_ReturnsSuccessfulEnvelope()
    {
        var service = new Mock<IGlAccountService>(MockBehavior.Strict);
        IReadOnlyList<AccountCategoryDto> categories = new[]
        {
            new AccountCategoryDto
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
        service
            .Setup(item => item.GetCategoriesAsync(CancellationToken.None))
            .ReturnsAsync(categories);

        var action = await new AccountCategoriesController(service.Object)
            .GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<List<AccountCategoryDto>>>(ok.Value);
        Assert.Equal(1, Assert.Single(response.Data!).CategoryCode);
        service.VerifyAll();
    }

    [Fact]
    public void Controller_UsesAuthenticatedTenantScope()
    {
        var controllerType = typeof(AccountCategoriesController);

        Assert.NotNull(Attribute.GetCustomAttribute(
            controllerType,
            typeof(TenantScopedAttribute)));
        Assert.Contains(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>(),
            attribute => string.IsNullOrEmpty(attribute.Policy));
    }
}
