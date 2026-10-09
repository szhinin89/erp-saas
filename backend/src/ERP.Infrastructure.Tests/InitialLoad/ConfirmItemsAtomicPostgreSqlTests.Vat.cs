using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.UpdateItem;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Infrastructure.Tests.InitialLoad;

public sealed partial class ConfirmItemsAtomicPostgreSqlTests
{
    private CreateItemCommand VatCommand(string? sale, string? purchase) =>
        new("VAT", "VAT", "VAT", _type, "19", _category, _brand, [new("VAT-BC", "Internal", true)],
            SaleVatCode: sale, PurchaseVatCode: purchase, BaseSalePrice: 10);

    [Theory]
    [InlineData(null, "0")]
    [InlineData("", "0")]
    [InlineData(" ", "0")]
    [InlineData("4", null)]
    [InlineData("4", "")]
    [InlineData("4", " ")]
    [InlineData("99", "0")]
    [InlineData("4", "99")]
    [InlineData("2", "0")]
    public async Task Crear_y_actualizar_rechazan_iva_vacio_inexistente_o_expirado_sin_escrituras(string? sale, string? purchase)
    {
        await using var request = _services.CreateAsyncScope();
        var mediator = request.ServiceProvider.GetRequiredService<IMediator>();
        if (sale == "2")
            await request.ServiceProvider.GetRequiredService<ErpDbContext>().SriVatRates.Where(x => x.Code == "2")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
        var empty = string.IsNullOrWhiteSpace(sale) || string.IsNullOrWhiteSpace(purchase);
        if (empty)
            await FluentActions.Awaiting(() => mediator.Send(VatCommand(sale, purchase)))
                .Should().ThrowAsync<FluentValidation.ValidationException>();
        else
            (await mediator.Send(VatCommand(sale, purchase))).IsSuccess.Should().BeFalse();
        (await request.ServiceProvider.GetRequiredService<ErpDbContext>().Items.CountAsync()).Should().Be(0);
        var created = await mediator.Send(VatCommand("4", "0"));
        created.IsSuccess.Should().BeTrue(created.Error);
        var update = new UpdateItemCommand(created.Value!.Id, "CHANGED", "VAT", "VAT", "19",
            SaleVatCode: sale, PurchaseVatCode: purchase, BaseSalePrice: 12);
        if (empty)
            await FluentActions.Awaiting(() => mediator.Send(update)).Should().ThrowAsync<FluentValidation.ValidationException>();
        else
            (await mediator.Send(update)).IsSuccess.Should().BeFalse();
        await using var check = _services.CreateAsyncScope();
        var item = await check.ServiceProvider.GetRequiredService<ErpDbContext>().Items.SingleAsync();
        item.Code.SKU.Should().Be("VAT");
        item.TaxConfig.SaleVatCode.Should().Be("4"); item.TaxConfig.PurchaseVatCode.Should().Be("0");
    }

    [Theory]
    [InlineData("0", "4")]
    [InlineData("6", "7")]
    [InlineData("7", "6")]
    public async Task Codigos_independientes_se_preservan_en_creacion_y_actualizacion(string sale, string purchase)
    {
        await using var request = _services.CreateAsyncScope();
        var mediator = request.ServiceProvider.GetRequiredService<IMediator>();
        var created = await mediator.Send(VatCommand(sale, purchase));
        created.IsSuccess.Should().BeTrue(created.Error);
        await using (var check = _services.CreateAsyncScope())
        {
            var persisted = await check.ServiceProvider.GetRequiredService<ErpDbContext>().Items.SingleAsync();
            persisted.TaxConfig.SaleVatCode.Should().Be(sale); persisted.TaxConfig.PurchaseVatCode.Should().Be(purchase);
        }
        var updated = await mediator.Send(new UpdateItemCommand(created.Value!.Id, "VAT", "VAT", "VAT", "19",
            SaleVatCode: purchase, PurchaseVatCode: sale, BaseSalePrice: 10, CategoryNodeId: _category, BrandId: _brand));
        updated.IsSuccess.Should().BeTrue(updated.Error);
        await using var finalCheck = _services.CreateAsyncScope();
        var item = await finalCheck.ServiceProvider.GetRequiredService<ErpDbContext>().Items.SingleAsync();
        item.TaxConfig.SaleVatCode.Should().Be(purchase); item.TaxConfig.PurchaseVatCode.Should().Be(sale);
    }

    [Theory]
    [InlineData(null, "0")]
    [InlineData("", "0")]
    [InlineData("4", null)]
    [InlineData("4", " ")]
    [InlineData("2", "0")]
    public async Task Preview_con_iva_invalido_bloquea_confirmacion(string? sale, string? purchase)
    {
        var batch = await BatchAsync(false, Row("INVALID"));
        var raw = new Dictionary<string, string?>
        {
            [ItemImportColumns.Sku] = "INVALID", [ItemImportColumns.Name] = "INVALID",
            [ItemImportColumns.ItemTypeCode] = "Physical", [ItemImportColumns.UomCode] = "19",
            [ItemImportColumns.CategoryName] = "Existing Category", [ItemImportColumns.BrandName] = "Existing Brand",
            [ItemImportColumns.Barcode1] = "VAT-INVALID", [ItemImportColumns.BarcodeType1] = "Internal",
            [ItemImportColumns.AvailableOnPos] = "NO", [ItemImportColumns.SaleVatCode] = sale,
            [ItemImportColumns.PurchaseVatCode] = purchase,
        };
        Mock.Get(_services.GetRequiredService<IItemImportSheetReader>())
            .Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ImportReadResult([raw]));
        var preview = await ValidateAsync(batch);
        preview.IsSuccess.Should().BeTrue(preview.Error); preview.Value!.IssueRows.Should().Be(1);
        (await ConfirmAsync(batch)).IsSuccess.Should().BeFalse();
        await using var check = _services.CreateAsyncScope();
        (await check.ServiceProvider.GetRequiredService<ErpDbContext>().Items.CountAsync()).Should().Be(0);
    }
}
