namespace ERP.Application.Common;

/// <summary>Exige que el tenant tenga la feature comercial habilitada en su suscripción (MediatR pipeline).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequireFeatureAttribute : Attribute
{
    public RequireFeatureAttribute(string featureCode) => FeatureCode = featureCode;

    public string FeatureCode { get; }
}

/// <summary>
/// Antes del handler: comprueba límite de uso para la feature medida.
/// Después del handler (si <see cref="ERP.Application.Common.Result{T}"/> es éxito): incrementa consumo.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ConsumeSubscriptionUnitsAttribute : Attribute
{
    public ConsumeSubscriptionUnitsAttribute(string featureCode, long units = 1)
    {
        FeatureCode = featureCode;
        Units = units;
    }

    public string FeatureCode { get; }
    public long Units { get; }
}
