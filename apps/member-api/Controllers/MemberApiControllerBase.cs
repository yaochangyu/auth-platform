using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MemberApi.Controllers;

[ApiController]
public abstract class MemberApiControllerBase : ControllerBase
{
    protected ActionResult ToValidationProblem(ValidationResult result, string title)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(ToCamelCase(error.PropertyName), error.ErrorMessage);
        }

        var problemResult = (ObjectResult)this.ValidationProblem(modelState);
        if (problemResult.Value is ValidationProblemDetails problemDetails)
        {
            problemDetails.Title = title;
            problemDetails.Type = "https://auth.1111.com.tw/errors/validation-failed";
        }

        return problemResult;
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    }
}
