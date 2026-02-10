namespace TakeTime.Core.Exceptions;

/// <summary>
/// Exception thrown when a business rule is violated.
/// Carries a rule name for categorization and optional details
/// for logging and troubleshooting.
/// </summary>
public class BusinessRuleException : DomainException
{
    /// <summary>
    /// The name or identifier of the business rule that was violated.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Optional additional details about the violation context.
    /// </summary>
    public string? Details { get; }

    public BusinessRuleException(string ruleName, string message)
        : base(message)
    {
        RuleName = ruleName;
    }

    public BusinessRuleException(string ruleName, string message, string details)
        : base(message)
    {
        RuleName = ruleName;
        Details = details;
    }

    public BusinessRuleException(string ruleName, string message, string errorCode, string? details = null)
        : base(message, errorCode)
    {
        RuleName = ruleName;
        Details = details;
    }

    public BusinessRuleException(string ruleName, string message, Exception innerException)
        : base(message, innerException)
    {
        RuleName = ruleName;
    }

    public override string ToString()
    {
        var result = $"Business Rule Violation [{RuleName}]: {Message}";

        if (!string.IsNullOrWhiteSpace(Details))
            result += $" | Details: {Details}";

        if (!string.IsNullOrWhiteSpace(ErrorCode))
            result += $" | Code: {ErrorCode}";

        return result;
    }
}
