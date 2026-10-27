namespace AntiqueStore.Application.Common;

/// <summary>Base for expected, client-safe errors. Messages are shown to users; never put internals in them.</summary>
public abstract class AppException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string entity, object? key = null)
    : AppException(key is null ? $"{entity} was not found." : $"{entity} '{key}' was not found.", "not_found");

/// <summary>A business rule was violated (e.g. item sold, invalid status transition).</summary>
public sealed class BusinessRuleException(string message, string code = "business_rule") : AppException(message, code);

/// <summary>State changed concurrently or a uniqueness constraint failed.</summary>
public sealed class ConflictException(string message, string code = "conflict") : AppException(message, code);

public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message, "forbidden");

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.", "validation")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
