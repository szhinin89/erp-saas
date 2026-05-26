using FluentAssertions;
using FluentValidation;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;

namespace ERP.Application.Tests.Compras;

public sealed class CreatePurchaseOrderCommandValidatorTests
{
    private static readonly CreatePurchaseOrderCommandValidator _v = new();

    private static CreatePurchaseOrderCommand ValidCmd(
        Guid? proveedorId = null,
        DateTime? fecha = null,
        List<PurchaseOrderItemRequest>? items = null) =>
        new(
            proveedorId ?? Guid.NewGuid(),
            fecha ?? DateTime.UtcNow.AddDays(10),
            TargetWarehouseId:  null,
            DeliveryAddress: null,
            Notes:    null,
            Items: items ?? [new PurchaseOrderItemRequest(Guid.NewGuid(), 5m, 10m, 15m)]);

    // ── ProveedorId ───────────────────────────────────────────────────────

    [Fact]
    public void ProveedorId_vacio_falla()
    {
        var result = _v.Validate(ValidCmd(proveedorId: Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SupplierId");
    }

    // ── RequiredDate ────────────────────────────────────────────────────

    [Fact]
    public void FechaRequerida_minvalue_falla()
    {
        // NotEmpty() en DateTime considera DateTime.MinValue (default) como vacío
        var result = _v.Validate(ValidCmd(fecha: DateTime.MinValue));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RequiredDate");
    }

    // ── Items ─────────────────────────────────────────────────────────────

    [Fact]
    public void Items_vacio_falla()
    {
        var result = _v.Validate(ValidCmd(items: []));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public void Item_cantidad_cero_falla()
    {
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.NewGuid(), Quantity: 0m, UnitPrice: 10m, VatPct: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Quantity"));
    }

    [Fact]
    public void Item_cantidad_negativa_falla()
    {
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.NewGuid(), Quantity: -1m, UnitPrice: 10m, VatPct: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Item_precio_negativo_falla()
    {
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.NewGuid(), Quantity: 5m, UnitPrice: -1m, VatPct: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("UnitPrice"));
    }

    [Fact]
    public void Item_precio_cero_es_valido()
    {
        // Precio 0 permitido (servicios gratuitos, muestras)
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.NewGuid(), Quantity: 5m, UnitPrice: 0m, VatPct: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Item_iva_negativo_falla()
    {
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.NewGuid(), Quantity: 5m, UnitPrice: 10m, VatPct: -1m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("VatPct"));
    }

    [Fact]
    public void Item_iva_cero_es_valido()
    {
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.NewGuid(), Quantity: 5m, UnitPrice: 10m, VatPct: 0m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Item_productoId_vacio_falla()
    {
        var items = new List<PurchaseOrderItemRequest>
            { new(Guid.Empty, Quantity: 5m, UnitPrice: 10m, VatPct: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ProductId"));
    }

    // ── Comando válido ────────────────────────────────────────────────────

    [Fact]
    public void Comando_valido_pasa()
    {
        var result = _v.Validate(ValidCmd());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Comando_dos_items_valido_pasa()
    {
        var items = new List<PurchaseOrderItemRequest>
        {
            new(Guid.NewGuid(), Quantity: 10m, UnitPrice:  5m, VatPct: 15m),
            new(Guid.NewGuid(), Quantity:  5m, UnitPrice: 10m, VatPct: 15m),
        };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeTrue();
    }

}
