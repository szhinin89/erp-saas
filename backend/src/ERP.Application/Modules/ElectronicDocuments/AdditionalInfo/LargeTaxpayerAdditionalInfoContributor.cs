using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.SriCatalogs.Constants;

namespace ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// R22 (ADR-038 D6) — emite <c>&lt;campoAdicional nombre="Gran Contribuyente"&gt;resolución&lt;/campoAdicional&gt;</c>
/// (Ficha Técnica 2.34, Anexo 24) para los emisores calificados como Gran Contribuyente.
///
/// Lee únicamente el perfil del emisor ya proyectado en el contexto
/// (<see cref="ElectronicDocumentIssuerData.LargeTaxpayerResolutionNumber"/>), que ya viene evaluado
/// contra la fecha de emisión de negocio: no consulta <c>Company</c> ni el reloj. Sin resolución
/// aplicable no aporta campos. El formato del dato lo garantizan el dominio y Readiness; el composer
/// revisa además el límite de 300 caracteres del XSD.
/// </summary>
public sealed class LargeTaxpayerAdditionalInfoContributor
    : IElectronicDocumentAdditionalInfoContributor
{
    public const string ContributorId = "sri.anexo24.large-taxpayer";

    /// <summary>Anexo 24: "comprobantes de venta, notas de crédito y notas de débito". La
    /// retención (07) y la guía de remisión (06) no lo llevan.</summary>
    private static readonly HashSet<ElectronicDocumentType> ApplicableTypes =
    [
        ElectronicDocumentType.Invoice,
        ElectronicDocumentType.PurchaseSettlement,
        ElectronicDocumentType.CreditNote,
        ElectronicDocumentType.DebitNote,
    ];

    private static readonly IReadOnlyList<ElectronicDocumentAdditionalField> NoFields = [];

    public string Id => ContributorId;

    /// <summary>Antes del RUC Proveedor (100): identidad fiscal del emisor primero.</summary>
    public int Order => 50;

    public bool AppliesTo(ElectronicDocumentType documentType) =>
        ApplicableTypes.Contains(documentType);

    public Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ContributeAsync(
        AdditionalInfoCompositionContext context,
        CancellationToken ct = default
    )
    {
        var resolution = context.Issuer.LargeTaxpayerResolutionNumber;
        return Task.FromResult(
            string.IsNullOrWhiteSpace(resolution)
                ? Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success(NoFields)
                : Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success([
                    new ElectronicDocumentAdditionalField(
                        SriAdditionalInfoFieldNames.LargeTaxpayer,
                        resolution
                    ),
                ])
        );
    }
}
