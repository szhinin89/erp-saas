using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Punto de Emisión Físico: no ejecuta ningún componente de ElectronicDocuments — sin XML, sin
/// firma, sin clave de acceso, sin autorización/consulta SRI, sin RIDE electrónico. Por eso esta
/// clase deliberadamente no depende de <c>IElectronicDocumentIssuer</c> ni de
/// <c>IElectronicDocumentRepository</c>: la ausencia de esas dependencias es la garantía
/// estructural de que la factura física nunca toca esa infraestructura.
/// PHYSICAL-FISCAL-F1..F5 — sus requisitos los decide la política fiscal física propietaria
/// (<see cref="IPhysicalEmissionRequirementsValidator"/>, módulo Company), no esta clase.
/// </summary>
public sealed class PhysicalSalesInvoiceEmissionStrategy : ISalesInvoiceEmissionStrategy
{
    private readonly IPhysicalEmissionRequirementsValidator _requirements;
    private readonly ILogger<PhysicalSalesInvoiceEmissionStrategy> _logger;

    public PhysicalSalesInvoiceEmissionStrategy(
        IPhysicalEmissionRequirementsValidator requirements,
        ILogger<PhysicalSalesInvoiceEmissionStrategy> logger
    )
    {
        _requirements = requirements;
        _logger = logger;
    }

    public EmissionType SupportedType => EmissionType.Physical;

    public Task<ElectronicEmissionRequirementsResult> CheckRequirementsAsync(
        SalesInvoiceEmissionPreflight preflight,
        CancellationToken ct
    ) =>
        _requirements.ValidateAsync(
            new PhysicalEmissionRequirementsRequest(
                preflight.TenantId,
                preflight.CompanyId,
                preflight.EmissionPointId,
                preflight.DocTypeCode
            ),
            ct
        );

    public async Task<IReadOnlyList<AuthorizedSequentialRange>?> ResolveAuthorizedSequentialRangesAsync(
        SalesInvoiceEmissionPreflight preflight,
        CancellationToken ct
    ) =>
        await _requirements.GetAuthorizedSequentialRangesAsync(
            new PhysicalEmissionRequirementsRequest(
                preflight.TenantId,
                preflight.CompanyId,
                preflight.EmissionPointId,
                preflight.DocTypeCode
            ),
            ct
        );

    public Task<string?> ExecuteAsync(SalesInvoiceEmissionContext context, CancellationToken ct)
    {
        var inv = context.Invoice;
        _logger.LogInformation(
            "Sales invoice {InvoiceNumber} ({InvoiceId}) authorized on a Physical emission point — no electronic document generated.",
            inv.InvoiceNumber,
            inv.Id
        );
        return Task.FromResult<string?>(null);
    }
}
