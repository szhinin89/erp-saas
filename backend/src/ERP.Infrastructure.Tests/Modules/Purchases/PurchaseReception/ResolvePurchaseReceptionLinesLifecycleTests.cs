using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

public sealed partial class ResolvePurchaseReceptionLinesIntegrationTests
{
    private PaymentTerm _paymentTerm = null!;

    // Real XML/draft/confirmation handlers and PostgreSQL repositories, including Stock and AP.
    // Accounting posting, operational preferences and price-list selection are fixture boundaries;
    // this is a backend integration test, not a browser or accounting end-to-end test.
    private void ConfigurePurchaseServices(IServiceCollection services)
    {
        services.AddSingleton<ICompanyPrecisionPolicyProvider>(
            StandardPrecisionPolicyProvider.Instance
        );
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IPaymentTermRepository, PaymentTermRepository>();
        services.AddScoped<IExpenseDocumentRepository, ExpenseDocumentRepository>();
        services.AddScoped<IBusinessPartnerRoleRepository, BusinessPartnerRoleRepository>();
        services.AddScoped<IAccountsPayableRepository, AccountsPayableRepository>();
        services.AddScoped<IAccountsPayableService, AccountsPayableService>();
        services.AddScoped<ISriDocTypeCatalogResolver, SriDocTypeCatalogResolver>();
        services.AddScoped<
            ERP.Application.Modules.Purchases.Services.ISriTaxResolver,
            SriTaxResolver
        >();
        services.AddScoped<IPurchaseXmlConfirmationGuard, PurchaseXmlConfirmationGuard>();
        // ZH-PURCHASE-RETENTION-CONFIRM-01: esta confirmación no lleva RetentionIntent — el emisor
        // es otro límite del fixture (la emisión integrada se cubre en PurchaseRetentionConfirmIntegrationTests).
        services.AddScoped(_ =>
            Mock.Of<ERP.Application.Modules.Retentions.Services.IRetentionIssuer>()
        );
        services.AddScoped(_ => Mock.Of<IPurchaseReceptionDetailProcessor>());
        services.AddScoped(_ =>
            Mock.Of<IPaymentTermDefaultResolver>(r =>
                r.ResolveForPurchaseAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ) == Task.FromResult(Result<PaymentTerm>.Success(_paymentTerm))
            )
        );
        services.AddScoped(_ =>
            Mock.Of<IPostingEngine>(p =>
                p.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>())
                == Task.FromResult(
                    Result<PostingOutcomeDto>.Success(
                        new(Guid.NewGuid(), PostingOutcomeStatus.Created)
                    )
                )
            )
        );
        services.AddScoped<IPricingResolver>(sp =>
        {
            var pricing = new Mock<IPricingResolver>();
            pricing
                .Setup(p =>
                    p.ResolveAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(
                    async (Guid itemId, Guid? _, CancellationToken ct) =>
                    {
                        var item = (
                            await sp.GetRequiredService<IItemRepository>()
                                .GetByIdLightAsync(itemId, _tenantId, ct)
                        )!;
                        var price = item.BaseSalePrice ?? 0;
                        return Result<PricingResult>.Success(
                            new(itemId, null, "", "", "USD", price, null, price)
                        );
                    }
                );
            return pricing.Object;
        });
        var preferences = new OperationalPreferences(
            new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            new CashPreferences(true, true, 0m, true, true, true),
            new PurchasesPreferences(null, false, true, true, false),
            new InventoryPreferences(false, true, false, 0m),
            new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            new ElectronicDocumentsPreferences(true, 3, true, true),
            new NotificationsPreferences(true, false, "es")
        );
        services.AddScoped(_ =>
            Mock.Of<IOperationalPreferencesResolver>(p =>
                p.ResolveAsync(It.IsAny<CancellationToken>()) == Task.FromResult(preferences)
            )
        );
    }

    [Fact]
    public async Task Resolved_XML_saves_and_confirms_with_correct_base_stock_cost_and_Kardex()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var resolution = await ResolveAsync(BuildBatch(document, "FLOW-"));
        resolution.Applied.Should().BeTrue();
        Guid warehouseId;
        await using (var seed = _services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ErpDbContext>();
            _paymentTerm = PaymentTerm.Create(_tenantId, "CONT", "Contado", 1, 0, _userId);
            var warehouse = Warehouse.Create(
                _tenantId,
                _branchId,
                "Principal",
                "B01",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                _userId,
                _companyId,
                isMain: true
            );
            warehouseId = warehouse.Id;
            db.Set<PaymentTerm>().Add(_paymentTerm);
            db.Warehouses.Add(warehouse);
            db.Set<BusinessPartnerRole>()
                .Add(
                    BusinessPartnerRole.Create(
                        _tenantId,
                        _supplierId,
                        RoleType.Supplier,
                        _userId,
                        supplierConfig: SupplierRoleConfig.Create()
                    )
                );
            await db.SaveChangesAsync();
        }

        Guid purchaseId;
        await using (var save = _services.CreateAsyncScope())
        {
            var mediator = save.ServiceProvider.GetRequiredService<IMediator>();
            var loaded = await mediator.Send(new CreatePurchaseReceptionDraftCommand(document.Id));
            loaded.IsSuccess.Should().BeTrue(loaded.Error);
            var draft = loaded.Value!;
            draft
                .Lines.Should()
                .HaveCount(100)
                .And.OnlyContain(l => l.ItemId != null && l.PackagingLevelId != null);
            var created = await mediator.Send(
                new CreatePurchaseDraftCommand(
                    _supplierId,
                    "01",
                    draft.InvoiceNumber,
                    draft.IssueDate,
                    draft
                        .Lines.Select(l => new PurchaseLineInput(
                            l.ItemId,
                            l.Description,
                            l.Quantity,
                            l.UnitPrice,
                            l.VatCode,
                            WarehouseId: warehouseId,
                            DiscountPct: l.DiscountPct,
                            PurchaseReceptionLineId: l.PurchaseReceptionLineId,
                            PackagingLevelId: l.PackagingLevelId
                        ))
                        .ToList(),
                    AccessKey: draft.AccessKey,
                    GlobalWarehouseId: warehouseId,
                    PaymentTermId: _paymentTerm.Id
                )
            );
            created.IsSuccess.Should().BeTrue(created.Error);
            purchaseId = created.Value!.Id;
        }
        await using (var confirm = _services.CreateAsyncScope())
        {
            var result = await confirm
                .ServiceProvider.GetRequiredService<IMediator>()
                .Send(new ConfirmPurchaseCommand(purchaseId));
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        await using (var verify = _services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
            var movements = await db.Set<StockMovement>()
                .Where(m => m.SourceDocId == purchaseId)
                .ToListAsync();
            movements
                .Should()
                .HaveCount(100)
                .And.OnlyContain(m => m.MovementType == StockMovementType.PurchaseEntry);
            var itemId = resolution
                .Lines.Single(l => l.ItemSku == "FLOW-000" && l.ConversionFactor == 1m)
                .ItemId;
            var stock = await db.Set<CurrentStock>()
                .SingleAsync(s => s.ProductId == itemId && s.WarehouseId == warehouseId);
            stock.Quantity.Should().Be(16m); // 4 units + one box of 12
            stock.TotalStockValue.Should().Be(15.40m); // 4 * 1 + 1 * 11.40
            stock.AverageCost.Should().Be(0.9625m);
            movements
                .Where(m => m.ProductId == itemId)
                .Select(m => m.Quantity)
                .Should()
                .BeEquivalentTo([4m, 12m]);
        }
        var next = await LoadAsync(await ReceiveInvoiceAsync());
        next.Lines.Should().OnlyContain(l => l.ItemId != null);
    }
}
