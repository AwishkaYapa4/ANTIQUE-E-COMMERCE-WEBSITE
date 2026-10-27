using AntiqueStore.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AntiqueStore.Api.Infrastructure;

/// <summary>
/// Maps expected application errors to RFC 7807 problem responses with a stable machine-readable "code".
/// Unexpected errors return a generic 500 (details are logged, never sent to clients).
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, code, errors) = exception switch
        {
            RequestValidationException v => (400, v.Message, v.Code, v.Errors),
            NotFoundException e => (404, e.Message, e.Code, null),
            ForbiddenException e => (403, e.Message, e.Code, null),
            ConflictException e => (409, e.Message, e.Code, null),
            BusinessRuleException e => (422, e.Message, e.Code, null),
            BadHttpRequestException e => (e.StatusCode, "The request was not valid.", "bad_request", null),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Request cancelled.", "cancelled", null),
            _ => (500, "Something went wrong on our side. Please try again.", "server_error", (IDictionary<string, string[]>?)null)
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else logger.LogInformation("Request failed with {Status} {Code}: {Message}", status, code, exception.Message);

        context.Response.StatusCode = status;
        var problem = errors is null
            ? new ProblemDetails { Status = status, Title = title }
            : new ValidationProblemDetails(errors) { Status = status, Title = title };
        problem.Extensions["code"] = code;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }
}

/// <summary>Runs the registered FluentValidation validator for every action argument before the action executes.</summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = ToCamelPath(failure.PropertyName);
                if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
                list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            var problem = new ValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()))
            {
                Status = 400, Title = "Please correct the highlighted fields."
            };
            problem.Extensions["code"] = "validation";
            context.Result = new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            return;
        }
        await next();
    }

    private static string ToCamelPath(string path) =>
        string.Join('.', path.Split('.').Select(p => p.Length > 0 ? char.ToLowerInvariant(p[0]) + p[1..] : p));
}
