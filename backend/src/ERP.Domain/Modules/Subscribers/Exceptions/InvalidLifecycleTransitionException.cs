using ERP.Domain.Subscribers.Entities;

namespace ERP.Domain.Subscribers.Exceptions;

/// <summary>
/// Thrown when a lifecycle transition is not allowed from the current state.
/// Maps to 409 Conflict at the API layer.
/// </summary>
public sealed class InvalidLifecycleTransitionException : InvalidOperationException
{
    public SubscriberLifecycleStatus From { get; }
    public string                    To   { get; }

    public InvalidLifecycleTransitionException(SubscriberLifecycleStatus from, string to)
        : base($"Transición inválida: {from} → {to}")
    {
        From = from;
        To   = to;
    }
}
