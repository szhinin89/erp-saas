using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// PHYSICAL-FISCAL-F1 — autorización del SRI para imprimir y emitir comprobantes preimpresos de un
/// tipo documental en un punto de emisión físico (Reglamento de Comprobantes de Venta, D.E. 430,
/// art. 5: autorización de impresión; art. 6: vigencia; art. 18 num. 1, 5 y 7: número y fecha de
/// autorización, numeración y fecha de caducidad preimpresos). Registra el dato tal como consta en
/// la autorización: el ERP no consulta ni certifica la autorización ante el SRI.
/// Inmutable: una autorización distinta (renovación o corrección) se registra como un nuevo
/// registro y la anterior se deshabilita; nunca se borra.
/// </summary>
public sealed class PhysicalDocumentAuthorization
    : MasterEntity,
        ITenantScopedEntity,
        ICompanyScopedEntity
{
    public const int AuthorizationNumberMaxLen = 49;

    public Guid CompanyId { get; private set; }
    public Guid EmissionPointId { get; private set; }

    /// <summary>Código SRI del tipo de comprobante autorizado (catálogo <c>sri_doc_type</c>).</summary>
    public string DocTypeCode { get; private set; } = null!;

    /// <summary>Número de la autorización de impresión otorgada por el SRI (art. 18 num. 1).</summary>
    public string AuthorizationNumber { get; private set; } = null!;

    /// <summary>Fecha de la autorización (art. 18 num. 1).</summary>
    public DateOnly AuthorizedOn { get; private set; }

    /// <summary>Fecha de caducidad según la autorización (art. 18 num. 7); último día de uso.</summary>
    public DateOnly ExpiresOn { get; private set; }

    /// <summary>Primer secuencial autorizado (los 9 dígitos de la numeración, art. 18 num. 5 c).</summary>
    public int FirstSequential { get; private set; }

    /// <summary>Último secuencial autorizado.</summary>
    public int LastSequential { get; private set; }

    private PhysicalDocumentAuthorization() { }

    public static PhysicalDocumentAuthorization Create(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        string docTypeCode,
        string authorizationNumber,
        DateOnly authorizedOn,
        DateOnly expiresOn,
        int firstSequential,
        int lastSequential,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(docTypeCode))
            throw new ArgumentException(
                "El tipo de comprobante es obligatorio.",
                nameof(docTypeCode)
            );

        var number = authorizationNumber?.Trim() ?? string.Empty;
        if (number.Length == 0 || number.Length > AuthorizationNumberMaxLen || !number.All(char.IsAsciiDigit))
            throw new ArgumentException(
                "El número de autorización del SRI debe contener solo dígitos.",
                nameof(authorizationNumber)
            );

        // Art. 6: la vigencia máxima es de un año desde la autorización.
        if (expiresOn < authorizedOn || expiresOn > authorizedOn.AddYears(1))
            throw new ArgumentOutOfRangeException(
                nameof(expiresOn),
                expiresOn,
                "La fecha de caducidad debe estar dentro del año siguiente a la fecha de autorización."
            );

        if (
            firstSequential < 1
            || lastSequential < firstSequential
            || lastSequential > DocumentSequence.MaxSequentialValue
        )
            throw new ArgumentOutOfRangeException(
                nameof(lastSequential),
                lastSequential,
                $"El rango autorizado debe estar entre 1 y {DocumentSequence.MaxSequentialValue} y el último secuencial no puede ser menor que el primero."
            );

        var authorization = new PhysicalDocumentAuthorization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EmissionPointId = emissionPointId,
            DocTypeCode = docTypeCode.Trim(),
            AuthorizationNumber = number,
            AuthorizedOn = authorizedOn,
            ExpiresOn = expiresOn,
            FirstSequential = firstSequential,
            LastSequential = lastSequential,
        };
        authorization.SetCreated(createdBy);
        return authorization;
    }

    public bool Covers(int sequential) =>
        sequential >= FirstSequential && sequential <= LastSequential;

    public bool IsInForceOn(DateOnly day) => day >= AuthorizedOn && day <= ExpiresOn;

    public bool OverlapsRange(int firstSequential, int lastSequential) =>
        firstSequential <= LastSequential && lastSequential >= FirstSequential;
}
