namespace ERP.Application.Modules.Purchases.Exceptions;

/// <summary>Aborts the shared confirmation transaction when mandatory accounting fails.</summary>
public sealed class PurchasePostingFailedException(string message, string? code = null)
    : ERP.Domain.Exceptions.DomainRuleViolationException(message)
{
    public string? Code { get; } = code;
}
