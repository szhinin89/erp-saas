using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// ADR-033, Fase 4 — reglas de regeneración/bloqueo del cronograma en UpdateSalesDraftHandler.
/// </summary>
public sealed class UpdateSalesDraftScheduleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IPaymentTermDefaultResolver> PtResolver { get; } = new();
        public Mock<IPaymentMethodRepository> PmRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<ISriTaxResolver> Tax { get; } = new();
        public Mock<IPricingResolver> Pricing { get; } = new();
        public Mock<ICompanySpecialTaxResponsibilityRepository> CompanyTaxRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();

        public PaymentTerm DefaultPt { get; } = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            Preferences
                .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(DefaultPreferences());

            var bp = BusinessPartner.Create(TenantId, "05", "1710034065", 1, "Cliente Test", UserId);
            BpRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(bp);

            PtResolver
                .Setup(r => r.ResolveForSaleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PaymentTerm>.Success(DefaultPt));

            Tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

            CompanyTaxRepo
                .Setup(r => r.GetResponsibleSriTaxCategoryCodesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<string>());
        }

        private static OperationalPreferences DefaultPreferences() =>
            new(
                SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
                Cash: new CashPreferences(true, true, 0m, true, true, true),
                Purchases: new PurchasesPreferences(null, true, true, true, false),
                Inventory: new InventoryPreferences(false, true, false, 0m),
                Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
                Notifications: new NotificationsPreferences(true, false, "es")
            );

        public UpdateSalesDraftHandler BuildHandler() =>
            new(
                Repo.Object,
                BpRepo.Object,
                RoleRepo.Object,
                PtResolver.Object,
                PmRepo.Object,
                ItemRepo.Object,
                Tax.Object,
                Pricing.Object,
                CompanyTaxRepo.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object,
                Preferences.Object
            );

        /// <summary>Factura Draft existente con una línea de 100 (VAT 15% => GrandTotal 115).</summary>
        public SalesInvoice ExistingInvoice(bool manualSchedule = false, decimal unitPrice = 100m)
        {
            var pt = PaymentTermSnapshot.Create(DefaultPt.Id, DefaultPt.Name, DefaultPt.Installments, DefaultPt.DaysBetweenInstallments);
            var inv = SalesInvoice.CreateDraft(
                TenantId, CompanyId, BranchId, CustomerId,
                CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
                "DRAFT-TEST", new DateOnly(2026, 1, 1), UserId, pt,
                cashSessionId: Guid.NewGuid()
            );
            var line = SalesInvoiceDetail.Create(inv.Id, TenantId, "Producto Test", 1, unitPrice, "10", "UNIT");
            line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
            inv.ReplaceLines(new[] { line }, UserId);

            if (manualSchedule)
                inv.ReplacePaymentSchedule(
                    new List<(int, DateOnly, decimal, string?)> { (1, inv.IssueDate, inv.GrandTotal, null) }
                );
            else
                inv.GeneratePaymentSchedule();

            return inv;
        }

        public static UpdateSalesDraftCommand BaseCommand(
            SalesInvoice inv,
            decimal? newUnitPrice = null,
            Guid? paymentTermId = null,
            List<SalesScheduleInput>? schedule = null
        ) =>
            new(
                inv.Id,
                CustomerId,
                inv.IssueDate,
                new List<SalesLineInput> { new(null, "Producto Test", 1, newUnitPrice ?? 100m, "10") },
                PaymentTermId: paymentTermId,
                Schedule: schedule
            );
    }

    [Fact]
    public async Task CambioDeTotal_ScheduleAutomatico_Regenera()
    {
        var f = new Fixture();
        var inv = f.ExistingInvoice(manualSchedule: false, unitPrice: 100m); // GrandTotal 115
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var cmd = Fixture.BaseCommand(inv, newUnitPrice: 200m); // GrandTotal 230
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentSchedules.Sum(s => s.Amount).Should().Be(inv.GrandTotal);
        inv.IsPaymentScheduleManual.Should().BeFalse();
    }

    [Fact]
    public async Task CambioDeTotal_ScheduleManual_SinNuevoSchedule_Bloquea()
    {
        var f = new Fixture();
        var inv = f.ExistingInvoice(manualSchedule: true, unitPrice: 100m);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        var originalSchedule = inv.PaymentSchedules.ToList();

        var cmd = Fixture.BaseCommand(inv, newUnitPrice: 200m);
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("personalizado");
        inv.PaymentSchedules.Should().BeEquivalentTo(originalSchedule);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CambioDeTotal_ScheduleManual_ConNuevoScheduleValido_Permite()
    {
        var f = new Fixture();
        var inv = f.ExistingInvoice(manualSchedule: true, unitPrice: 100m);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var cmd = Fixture.BaseCommand(
            inv,
            newUnitPrice: 200m, // GrandTotal 230
            schedule: new List<SalesScheduleInput>
            {
                new(1, inv.IssueDate.AddDays(10), 130m),
                new(2, inv.IssueDate.AddDays(20), 100m),
            }
        );
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentSchedules.Should().HaveCount(2);
        inv.PaymentSchedules.Sum(s => s.Amount).Should().Be(230m);
        inv.IsPaymentScheduleManual.Should().BeTrue();
    }

    [Fact]
    public async Task CambioDePaymentTerm_Regenera_Automatico_Y_ManualFalse()
    {
        var f = new Fixture();
        var inv = f.ExistingInvoice(manualSchedule: true, unitPrice: 100m); // manual=true inicialmente

        var newPt = PaymentTerm.Create(TenantId, "30D", "30 días", 2, 15, UserId);
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(CustomerId, newPt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.Success(newPt));
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var cmd = Fixture.BaseCommand(inv, paymentTermId: newPt.Id);
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentSchedules.Should().HaveCount(2); // nueva condición: 2 cuotas
        inv.PaymentSchedules.Sum(s => s.Amount).Should().Be(inv.GrandTotal);
        inv.IsPaymentScheduleManual.Should().BeFalse();
    }

    [Fact]
    public async Task CambioDeCliente_ConDefaultValido_Regenera_Y_ManualFalse()
    {
        var f = new Fixture();
        var inv = f.ExistingInvoice(manualSchedule: true, unitPrice: 100m);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var newCustomerId = Guid.NewGuid();
        var newPt = PaymentTerm.Create(TenantId, "45D", "45 días", 3, 15, UserId);
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(newCustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.Success(newPt));

        var cmd = new UpdateSalesDraftCommand(
            inv.Id,
            newCustomerId,
            inv.IssueDate,
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }
        );
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentTerm.Id.Should().Be(newPt.Id);
        inv.PaymentSchedules.Should().HaveCount(3);
        inv.IsPaymentScheduleManual.Should().BeFalse();
    }

    [Fact]
    public async Task CambioDeCliente_SinDefaultValido_MantienePaymentTermYNoCorrompeScheduleManual()
    {
        // ADR-033, Fase 4, regla aprobada punto 7: sin default válido para el nuevo cliente, se
        // mantiene el PaymentTerm actual del borrador y el cronograma manual existente NO se toca.
        var f = new Fixture();
        var inv = f.ExistingInvoice(manualSchedule: true, unitPrice: 100m);
        var originalPtId = inv.PaymentTerm.Id;
        var originalSchedule = inv.PaymentSchedules.ToList();
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var newCustomerId = Guid.NewGuid();
        f.PtResolver
            .Setup(r => r.ResolveForSaleAsync(newCustomerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure(
                "Debe seleccionar una condición de pago; no hay una configurada para esta empresa."
            ));

        var cmd = new UpdateSalesDraftCommand(
            inv.Id,
            newCustomerId,
            inv.IssueDate,
            new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") } // mismo total
        );
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentTerm.Id.Should().Be(originalPtId);
        inv.PaymentSchedules.Should().BeEquivalentTo(originalSchedule);
        inv.IsPaymentScheduleManual.Should().BeTrue();
    }

    [Fact]
    public async Task BorradorLegacySinSchedule_SeGeneraTransparenteSinError()
    {
        // Simula un borrador persistido antes de Fase 4: sin filas de cronograma.
        var f = new Fixture();
        var pt = PaymentTermSnapshot.Create(f.DefaultPt.Id, f.DefaultPt.Name, f.DefaultPt.Installments, f.DefaultPt.DaysBetweenInstallments);
        var inv = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "DRAFT-TEST", new DateOnly(2026, 1, 1), UserId, pt,
            cashSessionId: Guid.NewGuid()
        );
        var line = SalesInvoiceDetail.Create(inv.Id, TenantId, "Producto Test", 1, 100m, "10", "UNIT");
        inv.ReplaceLines(new[] { line }, UserId);
        // Deliberadamente SIN GeneratePaymentSchedule() — simula el estado legacy.
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

        var cmd = Fixture.BaseCommand(inv);
        var result = await f.BuildHandler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentSchedules.Should().NotBeEmpty();
        inv.PaymentSchedules.Sum(s => s.Amount).Should().Be(inv.GrandTotal);
    }
}
