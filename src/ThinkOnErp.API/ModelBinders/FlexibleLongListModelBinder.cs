using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ThinkOnErp.API.ModelBinders;

/// <summary>
/// A flexible model binder for List&lt;long&gt; that seamlessly binds:
/// 1. Repeated form keys (e.g., SystemIds=165&amp;SystemIds=181)
/// 2. Indexed keys (e.g., SystemIds[0]=165&amp;SystemIds[1]=181)
/// 3. Comma-separated strings (e.g., SystemIds=165,181)
/// 4. JSON array strings (e.g., SystemIds=[165,181])
/// </summary>
public class FlexibleLongListModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
            throw new ArgumentNullException(nameof(bindingContext));

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        var resultList = new List<long>();

        foreach (var value in valueProviderResult)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var trimmed = value.Trim().Trim('[', ']');
            var items = trimmed.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                if (long.TryParse(item.Trim(), out var num) && !resultList.Contains(num))
                {
                    resultList.Add(num);
                }
            }
        }

        bindingContext.Result = ModelBindingResult.Success(resultList);
        return Task.CompletedTask;
    }
}

public class FlexibleLongListModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (context.Metadata.ModelType == typeof(List<long>))
        {
            return new FlexibleLongListModelBinder();
        }

        return null;
    }
}
