namespace ERP.Domain.Subscriptions.Exceptions;

/// <summary>El tenant no tiene contratada la funcionalidad (feature) solicitada.</summary>
public sealed class FeatureNotEntitledException : Exception
{
    public FeatureNotEntitledException(string featureCode)
        : base($"La funcionalidad '{featureCode}' no está incluida en la suscripción actual.")
    {
        FeatureCode = featureCode;
    }

    public string FeatureCode { get; }
}

/// <summary>Se alcanzó el límite de uso para una funcionalidad medida.</summary>
public sealed class SubscriptionLimitExceededException : Exception
{
    public SubscriptionLimitExceededException(string featureCode, long limit, long attempted)
        : base($"Límite de suscripción para '{featureCode}' ({limit}) superado. Consumo solicitado: {attempted}.")
    {
        FeatureCode = featureCode;
        Limit = limit;
        Attempted = attempted;
    }

    public string FeatureCode { get; }
    public long Limit { get; }
    public long Attempted { get; }
}
