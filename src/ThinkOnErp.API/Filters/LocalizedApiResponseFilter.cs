using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.Services.Localization;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Filters;

/// <summary>
/// Action/Result filter that intercepts all outgoing IApiResponse objects and dynamically translates
/// their Message and Errors collection from SYS_CODE using ILocalizationService based on the client's language.
/// </summary>
public class LocalizedApiResponseFilter : IAsyncResultFilter
{
    private readonly ILocalizationService _localizationService;

    public LocalizedApiResponseFilter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult && objectResult.Value is IApiResponse apiResponse)
        {
            string? explicitLang = null;
            if (context.HttpContext.Items.TryGetValue("SessionLanguage", out var langItem) && langItem != null)
            {
                explicitLang = langItem.ToString();
            }
            else if (objectResult.Value is ApiResponse<TokenDto> tokenResp && tokenResp.Data != null && tokenResp.Data.Language > 0)
            {
                explicitLang = tokenResp.Data.Language.ToString();
            }

            LocalizeApiResponse(apiResponse, explicitLang);
        }

        await next();
    }

    private void LocalizeApiResponse(IApiResponse response, string? explicitLang = null)
    {
        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            response.Message = _localizationService.GetMessage(response.Message, explicitLang);
        }
        else if (response.Success)
        {
            response.Message = _localizationService.GetMessage(ResponseCodes.OperationSuccessful, explicitLang);
        }

        if (response.Errors != null && response.Errors.Count > 0)
        {
            for (int i = 0; i < response.Errors.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(response.Errors[i]))
                {
                    response.Errors[i] = _localizationService.GetMessage(response.Errors[i], explicitLang);
                }
            }
        }
    }
}
