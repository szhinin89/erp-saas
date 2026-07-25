using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Mapeo de una cuenta contable dentro de una <see cref="PostingRule"/> — entidad hija del mismo
/// aggregate, sin repositorio propio (mismo patrón encabezado + líneas ya usado en todo el ERP:
/// <c>PurchaseInvoice</c>+<c>PurchaseInvoiceDetail</c>, <c>SalesInvoice</c>+<c>SalesInvoiceDetail</c>).
/// Configuración de datos (ADR-026 §6.2) — nunca lógica condicional cerrada por tipo de hecho
/// contable.
/// </summary>
public sealed class PostingRuleLine : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PostingRuleId { get; private set; }
    public Guid AccountId { get; private set; }
    public AccountNature Nature { get; private set; }
    public PostingAmountKind AmountKind { get; private set; }
    public short SortOrder { get; private set; }

    private PostingRuleLine() { }

    public static PostingRuleLine Create(
        Guid postingRuleId, Guid tenantId, Guid accountId, AccountNature nature,
        PostingAmountKind amountKind, short sortOrder)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountId));

        return new PostingRuleLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PostingRuleId = postingRuleId,
            AccountId = accountId,
            Nature = nature,
            AmountKind = amountKind,
            SortOrder = sortOrder,
        };
    }
}
