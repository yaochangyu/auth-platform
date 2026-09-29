using System.Security.Claims;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DeveloperApi.Controllers;

[ApiController]
public abstract class DeveloperApiControllerBase : ControllerBase
{
    // 會員身分只取自已驗證 Access Token 的 sub claim，請求內容中的任何欄位都不能指定擁有者。
    protected Guid CurrentMemberId() => Guid.Parse(this.User.FindFirstValue("sub")!);

    // 不存在與不屬於呼叫者一律回 404，避免洩漏他人專案是否存在。
    protected ObjectResult NotFoundProblem() => this.Problem(
        type: "https://auth.1111.com.tw/errors/not-found", title: "找不到指定的資源", statusCode: StatusCodes.Status404NotFound);

    protected ObjectResult ConflictProblem(string title) => this.Problem(
        type: "https://auth.1111.com.tw/errors/conflict", title: title, statusCode: StatusCodes.Status409Conflict);

    protected ObjectResult ToValidationProblem(ValidationResult result)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..], error.ErrorMessage);
        }

        var problem = (ObjectResult)this.ValidationProblem(modelState);
        if (problem.Value is ValidationProblemDetails details)
        {
            details.Title = "請求參數驗證失敗";
            details.Type = "https://auth.1111.com.tw/errors/validation-failed";
        }

        return problem;
    }
}
