namespace ERP.Application.Modules.Sales.DTOs;

/// <summary>
/// Política fiscal efectiva de Consumidor Final para la empresa activa. <c>BlockConsumerFinalCredit</c>
/// siempre es <c>true</c> — regla fija, nunca editable — se expone para que ningún consumidor la
/// hardcodee. <c>ConsumerFinalMaxAmountSource</c> es uno de: "Manual", "TaxRegimeDefault", "Fallback".
/// Los dos mensajes son texto ya formateado por el backend — el frontend nunca redacta ni calcula
/// el monto máximo en el mensaje.
/// </summary>
public sealed record SalesFiscalPolicyDto(
    bool BlockConsumerFinalCredit,
    decimal ConsumerFinalMaxAmount,
    string ConsumerFinalMaxAmountSource,
    string? TaxRegimeCode,
    string CreditBlockedMessage,
    string AmountExceededMessage
);

/// <summary>
/// Contexto agregado que Ventas (y futuras pantallas: POS, facturación rápida, pedidos, app
/// móvil) consumen en un solo GET al cargar. Estructurado por bloques — nunca un DTO plano.
/// </summary>
public sealed record SalesRuntimeContextDto(
    SalesFiscalPolicyDto ConsumerFinalPolicy,
    SalesRuntimeDefaultsDto Defaults
);

public sealed record SalesRuntimeDefaultsDto(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultEmissionPointId,
    Guid? DefaultWarehouseId,
    Guid? DefaultPaymentTermId,
    string FallbackDocTypeCode,
    string FallbackSriPaymentMethodCode,
    string DefaultWarehouseSource,
    bool RequiresManualWarehouseSelection,
    IReadOnlyList<string> ConfigurationWarnings
);
