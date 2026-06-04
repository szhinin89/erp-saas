using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Aggregate Root de listas de precio.
/// Scope: company — cada empresa puede tener sus propias listas.
/// INVARIANTE: Solo una lista con IsDefault=true por (SubscriberId, CompanyId).
/// </summary>
public sealed class PriceList : MasterEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    private readonly List<PriceListEntry> _entries = new();
    private readonly List<PriceListDiscount> _discounts = new();

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string CurrencyCode { get; private set; } = null!;
    public bool PriceIncludesVat { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public bool IsDefault { get; private set; }
    public PriceListTarget Target { get; private set; }

    public IReadOnlyList<PriceListEntry> Entries => _entries.AsReadOnly();
    public IReadOnlyList<PriceListDiscount> Discounts => _discounts.AsReadOnly();

    private PriceList() { }

    public static PriceList Create(
        Guid subscriberId,
        Guid companyId,
        string code,
        string name,
        string currencyCode,
        DateOnly validFrom,
        Guid createdBy,
        bool priceIncludesVat = false,
        DateOnly? validTo = null,
        bool isDefault = false,
        PriceListTarget target = PriceListTarget.General)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de la lista de precios es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la lista de precios es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new ArgumentException("El código de moneda debe tener exactamente 3 caracteres (ISO 4217).", nameof(currencyCode));
        if (validTo.HasValue && validTo < validFrom)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(validTo));

        var pl = new PriceList
        {
            SubscriberId    = subscriberId,
            CompanyId       = companyId,
            Code            = code.Trim().ToUpperInvariant(),
            Name            = name.Trim(),
            CurrencyCode    = currencyCode.Trim().ToUpperInvariant(),
            PriceIncludesVat = priceIncludesVat,
            ValidFrom       = validFrom,
            ValidTo         = validTo,
            IsDefault       = isDefault,
            Target          = target,
        };
        pl.SetCreated(createdBy);
        return pl;
    }

    public void Update(
        string name, bool priceIncludesVat,
        DateOnly validFrom, DateOnly? validTo,
        PriceListTarget target, Guid updatedBy)
    {
        if (validTo.HasValue && validTo < validFrom)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(validTo));

        Name             = name.Trim();
        PriceIncludesVat = priceIncludesVat;
        ValidFrom        = validFrom;
        ValidTo          = validTo;
        Target           = target;
        SetUpdated(updatedBy);
    }

    public void SetAsDefault(Guid updatedBy)
    {
        IsDefault = true;
        SetUpdated(updatedBy);
    }

    public void ClearDefault(Guid updatedBy)
    {
        IsDefault = false;
        SetUpdated(updatedBy);
    }

    // ── Entradas de precio ────────────────────────────────────────────────

    /// <returns>(entry, isNew) — isNew=true when the entry was just created and must be tracked explicitly.</returns>
    public (PriceListEntry Entry, bool IsNew) SetItemPrice(
        Guid itemId, Guid? variantId, string uomCode, decimal price, decimal minQty)
    {
        var existing = _entries.FirstOrDefault(e =>
            e.ItemId == itemId &&
            e.VariantId == variantId &&
            e.UomCode == uomCode.Trim().ToUpperInvariant() &&
            e.MinQty == minQty &&
            e.IsActive);

        if (existing is not null)
        {
            existing.UpdatePrice(price);
            return (existing, false);
        }

        var entry = PriceListEntry.Create(Id, SubscriberId, itemId, variantId, uomCode, price, minQty);
        _entries.Add(entry);
        return (entry, true);
    }

    public void DisableEntry(Guid entryId)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entrada de precio no encontrada.");
        entry.Disable();
    }

    // ── Descuentos ────────────────────────────────────────────────────────

    public PriceListDiscount AddDiscount(
        DiscountType discountType, decimal discountValue,
        Guid? itemId = null, Guid? categoryNodeId = null,
        decimal? minQty = null,
        DateOnly? validFrom = null, DateOnly? validTo = null)
    {
        var discount = PriceListDiscount.Create(
            Id, SubscriberId, discountType, discountValue,
            itemId, categoryNodeId, minQty, validFrom, validTo);
        _discounts.Add(discount);
        return discount;
    }
}
