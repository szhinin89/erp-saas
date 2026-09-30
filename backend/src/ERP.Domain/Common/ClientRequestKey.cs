namespace ERP.Domain.Common;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — identidad de UNA intención de negocio enviada por el
/// cliente para un comando financiero creador de dinero (pago a proveedor, cobro de CxC,
/// movimiento manual de caja): <see cref="Id"/> la genera el frontend una vez por intención y
/// <see cref="PayloadHash"/> es la huella SHA-256 (hex mayúsculas) de la representación canónica
/// del request. Misma convención que ya usan <c>CashFundingRequest</c> y
/// <c>SupplierCreditMovement</c> (ClientRequestId + huella, único por Tenant + ClientRequestId).
/// </summary>
public readonly record struct ClientRequestKey
{
    public const int PayloadHashLength = 64;

    public Guid Id { get; }
    public string PayloadHash { get; }

    public ClientRequestKey(Guid id, string payloadHash)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("El identificador de la intención es obligatorio.", nameof(id));
        if (string.IsNullOrWhiteSpace(payloadHash) || payloadHash.Length != PayloadHashLength)
            throw new ArgumentException(
                $"La huella de la intención debe tener {PayloadHashLength} caracteres.",
                nameof(payloadHash)
            );
        Id = id;
        PayloadHash = payloadHash;
    }

    /// <summary>Mismo request que el ya registrado con este identificador (comparación ordinal de huellas).</summary>
    public bool Matches(string? storedPayloadHash) =>
        string.Equals(PayloadHash, storedPayloadHash, StringComparison.Ordinal);
}
