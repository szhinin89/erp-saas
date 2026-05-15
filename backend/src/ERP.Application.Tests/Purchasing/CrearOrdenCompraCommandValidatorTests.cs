using FluentAssertions;
using FluentValidation;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;

namespace ERP.Application.Tests.Compras;

public sealed class CrearOrdenCompraCommandValidatorTests
{
    private static readonly CrearOrdenCompraCommandValidator _v = new();

    private static CrearOrdenCompraCommand ValidCmd(
        Guid? proveedorId = null,
        DateTime? fecha = null,
        List<ItemOrdenCompraRequest>? items = null) =>
        new(
            proveedorId ?? Guid.NewGuid(),
            fecha ?? DateTime.UtcNow.AddDays(10),
            BodegaDestinoId:  null,
            DireccionEntrega: null,
            Observaciones:    null,
            Items: items ?? [new ItemOrdenCompraRequest(Guid.NewGuid(), 5m, 10m, 15m)]);

    // ── ProveedorId ───────────────────────────────────────────────────────

    [Fact]
    public void ProveedorId_vacio_falla()
    {
        var result = _v.Validate(ValidCmd(proveedorId: Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProveedorId");
    }

    // ── FechaRequerida ────────────────────────────────────────────────────

    [Fact]
    public void FechaRequerida_minvalue_falla()
    {
        // NotEmpty() en DateTime considera DateTime.MinValue (default) como vacío
        var result = _v.Validate(ValidCmd(fecha: DateTime.MinValue));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FechaRequerida");
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
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.NewGuid(), Cantidad: 0m, PrecioUnitario: 10m, IvaPorcentaje: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Cantidad"));
    }

    [Fact]
    public void Item_cantidad_negativa_falla()
    {
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.NewGuid(), Cantidad: -1m, PrecioUnitario: 10m, IvaPorcentaje: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Item_precio_negativo_falla()
    {
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.NewGuid(), Cantidad: 5m, PrecioUnitario: -1m, IvaPorcentaje: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("PrecioUnitario"));
    }

    [Fact]
    public void Item_precio_cero_es_valido()
    {
        // Precio 0 permitido (servicios gratuitos, muestras)
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.NewGuid(), Cantidad: 5m, PrecioUnitario: 0m, IvaPorcentaje: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Item_iva_negativo_falla()
    {
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.NewGuid(), Cantidad: 5m, PrecioUnitario: 10m, IvaPorcentaje: -1m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("IvaPorcentaje"));
    }

    [Fact]
    public void Item_iva_cero_es_valido()
    {
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.NewGuid(), Cantidad: 5m, PrecioUnitario: 10m, IvaPorcentaje: 0m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Item_productoId_vacio_falla()
    {
        var items = new List<ItemOrdenCompraRequest>
            { new(Guid.Empty, Cantidad: 5m, PrecioUnitario: 10m, IvaPorcentaje: 15m) };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ProductoId"));
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
        var items = new List<ItemOrdenCompraRequest>
        {
            new(Guid.NewGuid(), Cantidad: 10m, PrecioUnitario:  5m, IvaPorcentaje: 15m),
            new(Guid.NewGuid(), Cantidad:  5m, PrecioUnitario: 10m, IvaPorcentaje: 15m),
        };
        var result = _v.Validate(ValidCmd(items: items));
        result.IsValid.Should().BeTrue();
    }

}
