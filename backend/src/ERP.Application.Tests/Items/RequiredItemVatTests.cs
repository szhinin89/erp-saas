using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.UpdateItem;
using FluentAssertions;

namespace ERP.Application.Tests.Items;

public sealed class RequiredItemVatTests
{
    [Theory]
    [InlineData(null, "0")]
    [InlineData("", "0")]
    [InlineData("   ", "0")]
    [InlineData("4", null)]
    [InlineData("4", "")]
    [InlineData("4", "   ")]
    public void Crear_y_actualizar_exigen_ambos_iva(string? sale, string? purchase)
    {
        var create = new CreateItemCommand("VAT", "VAT", "VAT", Guid.NewGuid(), "19",
            Guid.NewGuid(), Guid.NewGuid(), [new("VAT-BC", "Internal", true)], SaleVatCode: sale, PurchaseVatCode: purchase);
        var update = new UpdateItemCommand(Guid.NewGuid(), "VAT", "VAT", "VAT", "19",
            SaleVatCode: sale, PurchaseVatCode: purchase, BaseSalePrice: 10);
        var field = string.IsNullOrWhiteSpace(sale) ? "SaleVatCode" : "PurchaseVatCode";
        new CreateItemCommandValidator().Validate(create).Errors.Should().Contain(x => x.PropertyName == field);
        new UpdateItemCommandValidator().Validate(update).Errors.Should().Contain(x => x.PropertyName == field);
    }
}
