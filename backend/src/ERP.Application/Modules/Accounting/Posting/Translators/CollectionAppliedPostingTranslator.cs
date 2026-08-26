using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce CollectionAppliedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine — no
/// crea JournalEntry, no contiene lógica financiera (ADR-026 §8, Fase 5.6.1). Un cobro no tiene
/// desglose de impuestos: Subtotal/TotalVat/TotalIce/TotalDiscount van en cero deliberadamente (no
/// son "montos inventados" — un cobro real no tiene esos componentes) y GrandTotal transporta el
/// monto cobrado, mismo campo genérico que ya usan los traductores de Ventas/Compras.
///
/// ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — si el cobro especificó un destino financiero
/// (<see cref="CollectionAppliedEvent.FinancialDestinationId"/>), lee su
/// <c>AccountingAccountId</c> ya validado/postable (<c>CompanyFinancialDestination</c> exige cuenta
/// activa y postable desde su propia creación, ver <c>CreateCompanyFinancialDestinationHandler</c>)
/// y lo pasa como override de la línea Debe de la PostingRule (el lado "caja/banco" de un cobro,
/// nunca el lado CxC) — no es "resolver una regla contable" (eso sigue siendo exclusivo del
/// Posting Engine), es leer tal cual un dato ya elegido por el usuario en Finance, mismo principio
/// que Subtotal/TotalVat arriba. Destino ausente, no encontrado o inactivo → sin override,
/// comportamiento previo intacto (log-and-continue, nunca bloquea el cobro ya aplicado).
/// </summary>
public sealed class CollectionAppliedPostingTranslator
    : INotificationHandler<CollectionAppliedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "CollectionApplied";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyFinancialDestinationRepository _financialDestinations;
    private readonly ILogger<CollectionAppliedPostingTranslator> _logger;

    public CollectionAppliedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyFinancialDestinationRepository financialDestinations,
        ILogger<CollectionAppliedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _financialDestinations = financialDestinations;
        _logger = logger;
    }

    public async Task Handle(CollectionAppliedEvent e, CancellationToken ct)
    {
        Guid? overrideAccountId = null;
        if (e.FinancialDestinationId is { } destinationId)
        {
            var destination = await _financialDestinations.GetByIdAsync(
                e.TenantId!.Value,
                destinationId,
                ct
            );
            if (destination is { IsActive: true })
            {
                overrideAccountId = destination.AccountingAccountId;
            }
            else
            {
                _logger.LogWarning(
                    "Financial destination {FinancialDestinationId} not found or inactive for "
                        + "Collection {PaymentId} — falling back to the PostingRule default account.",
                    destinationId,
                    e.PaymentId
                );
            }
        }

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.PaymentId,
            e.PaymentDate,
            0m,
            0m,
            0m,
            0m,
            e.Amount,
            OverrideAmountKind: overrideAccountId is null ? null : PostingAmountKind.GrandTotal,
            OverrideAccountNature: overrideAccountId is null ? null : AccountNature.Debit,
            OverrideAccountId: overrideAccountId
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for Collection {PaymentId}: {Code} — {Error}",
                e.PaymentId,
                result.Code,
                result.Error
            );
        }
    }
}
