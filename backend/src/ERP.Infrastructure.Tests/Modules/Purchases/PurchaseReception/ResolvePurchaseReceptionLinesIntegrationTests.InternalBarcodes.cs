using ERP.Application.Items.UseCases.Barcodes;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using ERP.Domain.Modules.Items.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

public sealed partial class ResolvePurchaseReceptionLinesIntegrationTests
{
    private readonly BarcodeRaceInterceptor _barcodeRace = new();

    private sealed class BarcodeRaceInterceptor : SaveChangesInterceptor
    {
        public Func<string, Task>? BeforeInternalInsert { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var code = eventData.Context!.ChangeTracker.Entries<ItemVariantBarcode>()
                .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Code.StartsWith("INT-", StringComparison.Ordinal))?.Entity.Code;
            if (code is not null && BeforeInternalInsert is { } callback)
                await callback(code);
            return result;
        }
    }

    private async Task<ResolvePurchaseReceptionLinesCommand> AutomaticBatchAsync(string prefix, string[] codes)
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync(customLines:
            codes.Select((code, i) => (code, $"Pending {prefix}{i}", 1m, 1m))));
        var lines = codes.Select(code => document.Lines.Single(l => (l.SupplierCode ?? "") == code)).ToList();
        return new(
            lines.Select((_, i) => new ResolveReceptionNewItemInput(
                $"n{i}", $"{prefix}{i:D3}", $"Product {i}", $"Product {i}", _itemTypeId,
                _categories[0], _brands[0], UnitUom, "0", "0", null, 1.35m)).ToList(),
            // Como el cliente real: sin barcode explícito; el backend recupera del XML solo GTIN válidos.
            lines.Select((line, i) => new ResolveReceptionLineInput(line.Id, NewItemKey: $"n{i}")).ToList());
    }

    [Fact]
    public async Task Twenty_three_products_without_EAN_keep_supplier_codes_and_accept_real_EAN_later()
    {
        var codes = new[] { "0", "", "07", "02", "300067162" }
            .Concat(Enumerable.Range(5, 18).Select(i => $"SUP-{i}")).ToArray();
        var command = await AutomaticBatchAsync("AUTO-", codes);
        var result = await ResolveAsync(command);
        result.Applied.Should().BeTrue(string.Join(" | ", result.Errors.Select(e => e.Message)));
        result.ItemsCreated.Should().Be(23);
        result.EquivalencesLearned.Should().Be(21);
        var barcodes = new List<string>();
        for (var i = 0; i < 23; i++)
        {
            var item = await LoadCreatedAsync($"AUTO-{i:D3}");
            barcodes.Add(ActiveCode(item, "Internal"));
            item.Code.SKU.Should().Be($"AUTO-{i:D3}");
            if (i < 2) item.SupplierCodes.Should().BeEmpty();
            else item.SupplierCodes.Should().ContainSingle(c => c.Code == codes[i]);
        }
        barcodes.Should().OnlyHaveUniqueItems().And.OnlyContain(c => c.StartsWith("INT-"));

        var original = await LoadCreatedAsync("AUTO-002");
        var originalLevel = original.PackagingLevels.Single().Id;
        await using (var scope = _services.CreateAsyncScope())
        {
            var added = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                new AddBarcodeCommand(original.Id, original.Variants.Single().Id, "7861234567898", "EAN13"));
            added.IsSuccess.Should().BeTrue(added.Error);
        }
        var updated = await LoadCreatedAsync("AUTO-002");
        updated.Id.Should().Be(original.Id);
        updated.PackagingLevels.Single().Id.Should().Be(originalLevel);
        updated.SupplierCodes.Single().Id.Should().Be(original.SupplierCodes.Single().Id);
        ActiveCode(updated, "Internal").Should().Be(barcodes[2]);
        ActiveCode(updated, "EAN13").Should().Be("7861234567898");
        var reception = await LoadAsync((await LoadDocumentIdAsync(command.Lines[2].PurchaseReceptionLineId)));
        reception.Lines.Single(l => l.Id == command.Lines[2].PurchaseReceptionLineId).ItemId.Should().Be(original.Id);
    }

    private async Task<Guid> LoadDocumentIdAsync(Guid lineId)
    {
        await using var scope = _services.CreateAsyncScope();
        var document = await scope.ServiceProvider.GetRequiredService<ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.IPurchaseReceptionDocumentRepository>()
            .GetByLineIdAsync(_tenantId, lineId);
        return document!.Id;
    }

    [Fact]
    public async Task Valid_XML_EAN_is_used_without_client_hint_and_duplicate_requires_linking()
    {
        var command = await AutomaticBatchAsync("EAN-", ["7861234567898", "12345670"]);
        var result = await ResolveAsync(command);
        result.Applied.Should().BeTrue(string.Join(" | ", result.Errors.Select(e => e.Message)));
        ActiveCode(await LoadCreatedAsync("EAN-000"), "EAN13").Should().Be("7861234567898");
        ActiveCode(await LoadCreatedAsync("EAN-001"), "EAN8").Should().Be("12345670");

        var duplicate = await AutomaticBatchAsync("EAN-DUP-", ["OTHER-SUP"]);
        duplicate = duplicate with { Lines = [duplicate.Lines[0] with { Barcode = "7861234567898", BarcodeType = "EAN13" }] };
        var rejected = await ResolveAsync(duplicate);
        rejected.Applied.Should().BeFalse();
        rejected.Errors.Should().Contain(e => e.Message.Contains("Vincular"));
        await using var scope = _services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ErpDbContext>().Items
            .AnyAsync(i => i.Code.SKU == "EAN-DUP-000")).Should().BeFalse();
    }

    [Fact]
    public async Task Database_collision_after_precheck_rolls_back_and_regenerates_internal_barcode()
    {
        var command = await AutomaticBatchAsync("RACE-", ["SUP-RACE"]);
        string? occupied = null;
        _barcodeRace.BeforeInternalInsert = async code =>
        {
            _barcodeRace.BeforeInternalInsert = null;
            occupied = code;
            await SeedItemWithBarcodeAsync(code, "Internal");
        };
        var result = await ResolveAsync(command);
        result.Applied.Should().BeTrue(string.Join(" | ", result.Errors.Select(e => e.Message)));
        occupied.Should().NotBeNull();
        var created = await LoadCreatedAsync("RACE-000");
        ActiveCode(created, "Internal").Should().NotBe(occupied);
        created.SupplierCodes.Should().ContainSingle();
        result.ItemsCreated.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_batches_create_distinct_internal_codes_in_the_same_company()
    {
        var first = await AutomaticBatchAsync("PAR-A-", ["0", "PAR-A"]);
        var second = await AutomaticBatchAsync("PAR-B-", ["", "PAR-B"]);
        var reached = 0;
        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _barcodeRace.BeforeInternalInsert = async _ =>
        {
            if (Interlocked.Increment(ref reached) == 2) bothReady.TrySetResult();
            await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };
        var results = await Task.WhenAll(ResolveAsync(first), ResolveAsync(second));
        results.Should().OnlyContain(r => r.Applied && r.ItemsCreated == 2);
        var codes = new List<string>();
        foreach (var sku in new[] { "PAR-A-000", "PAR-A-001", "PAR-B-000", "PAR-B-001" })
            codes.Add(ActiveCode(await LoadCreatedAsync(sku), "Internal"));
        codes.Should().OnlyHaveUniqueItems();
    }
}
