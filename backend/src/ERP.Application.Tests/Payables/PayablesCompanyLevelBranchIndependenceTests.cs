using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Application.Modules.Payables.UseCases;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

/// <summary>
/// PAYABLES-BRANCH-SCOPE-DECISION-01 — decisión de negocio: CxP, cuentas por pagar y pagos a
/// proveedor son company-level, no branch-level. Estos tests documentan y prueban ambas mitades de
/// esa regla con un repositorio fake (filtrado real por Tenant+Company, nunca por Branch — a
/// diferencia de un mock configurado ad-hoc, esto reproduce el comportamiento real de filtrado):
/// (1) la empresa activa ve CxP/pagos propios sin importar desde qué sucursal se originaron, y
/// (2) nunca ve datos de otra empresa, aunque comparta tenant.
/// </summary>
public sealed class PayablesCompanyLevelBranchIndependenceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid CompanyBId = Guid.NewGuid();
    private static readonly Guid BranchAId = Guid.NewGuid();
    private static readonly Guid BranchBId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // ── Fakes (filtrado real por Tenant+Company; Branch nunca interviene) ────

    private sealed class FakeAccountsPayableRepository : IAccountsPayableRepository
    {
        public readonly List<AccountsPayable> Store = new();

        public Task<AccountsPayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));

        public Task<AccountsPayable?> GetByOriginAsync(
            Guid tenantId, Guid companyId, AccountsPayableOriginType originType, Guid originId,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<Guid?> GetOriginIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<(IReadOnlyList<AccountsPayable> Items, int Total)> SearchAsync(
            Guid tenantId, Guid companyId, AccountsPayableOriginType? originType, AccountsPayableStatus? status,
            Guid? supplierId, DateOnly? dueDateFrom, DateOnly? dueDateTo, string? search, int page, int pageSize,
            CancellationToken ct = default
        )
        {
            var items = Store.Where(p => p.TenantId == tenantId && p.CompanyId == companyId).ToList();
            return Task.FromResult(((IReadOnlyList<AccountsPayable>)items, items.Count));
        }

        public Task<AccountsPayable?> GetByInstallmentIdAsync(
            Guid tenantId, Guid installmentId, CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task AddAsync(AccountsPayable payable, CancellationToken ct = default)
        {
            Store.Add(payable);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSupplierPaymentRepository : ISupplierPaymentRepository
    {
        public readonly List<SupplierPayment> Store = new();

        public Task<SupplierPayment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));

        public Task<(IReadOnlyList<SupplierPayment> Items, int Total)> SearchAsync(
            Guid tenantId, Guid companyId, Guid? supplierId, SupplierPaymentStatus? status, int page, int pageSize,
            CancellationToken ct = default
        )
        {
            var items = Store.Where(p => p.TenantId == tenantId && p.CompanyId == companyId).ToList();
            return Task.FromResult(((IReadOnlyList<SupplierPayment> Items, int Total))(items, items.Count));
        }

        public Task<bool> ExistsByReceiptNumberAsync(
            Guid tenantId, Guid companyId, Guid supplierId, string receiptNumber, CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task AddAsync(SupplierPayment payment, CancellationToken ct = default)
        {
            Store.Add(payment);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AccountsPayable BuildPayable(Guid companyId, Guid branchId, decimal amount)
    {
        var issueDate = new DateOnly(2026, 8, 1);
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, companyId, branchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, Guid.NewGuid(), "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}", issueDate, issueDate, UserId
        );
        payable.AddInstallment(1, issueDate.AddDays(30), amount);
        return payable;
    }

    private static SupplierPayment BuildPayment(Guid companyId, Guid branchId) =>
        SupplierPayment.Create(
            TenantId, companyId, branchId, SupplierId, new DateOnly(2026, 8, 28), 100m,
            Random.Shared.Next(10000000, 99999999).ToString(System.Globalization.CultureInfo.InvariantCulture), null,
            new[] { new SupplierPaymentMethodLineInput(Guid.NewGuid(), Guid.NewGuid(), 100m) },
            new[] { new SupplierPaymentApplicationLineInput(Guid.NewGuid(), 100m) },
            new[] { new SupplierPaymentAllocationInput(0, 0, 100m) },
            UserId
        );

    private static Mock<IBusinessPartnerRepository> NamesMock() =>
        new(); // GetNamesByIdsAsync no configurado: Moq devuelve Task<null> por defecto → GetValueOrDefault en el mapper cubre el caso.

    // ── AccountsPayable: branch-independent (regla 1) ────────────────────

    [Fact]
    public async Task Listado_CxP_ve_documentos_de_ambas_sucursales_de_la_misma_empresa()
    {
        var repo = new FakeAccountsPayableRepository();
        repo.Store.Add(BuildPayable(CompanyAId, BranchAId, 100m));
        repo.Store.Add(BuildPayable(CompanyAId, BranchBId, 200m));

        var partners = NamesMock();
        partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [SupplierId] = "Proveedor" });

        var handler = new GetAccountsPayablesListHandler(
            repo,
            partners.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyAId)
        );

        var result = await handler.Handle(new GetAccountsPayablesListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Total.Should().Be(2, "CxP es company-level: ambas sucursales de la Empresa A deben verse juntas");
    }

    [Fact]
    public async Task Detalle_CxP_de_sucursal_B_es_visible_sin_exigir_sucursal_activa()
    {
        var repo = new FakeAccountsPayableRepository();
        var payable = BuildPayable(CompanyAId, BranchBId, 150m);
        repo.Store.Add(payable);

        var partners = NamesMock();
        partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [SupplierId] = "Proveedor" });

        // Notar: GetAccountsPayableByIdHandler no recibe ICurrentBranch en su constructor —
        // prueba en tiempo de compilación de que la query ya no exige sucursal activa
        // (ICompanyScopedRequest, no IBranchScopedRequest).
        var handler = new GetAccountsPayableByIdHandler(
            repo,
            partners.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId)
        );

        var result = await handler.Handle(new GetAccountsPayableByIdQuery(payable.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    // ── AccountsPayable: company isolation (regla 2) ─────────────────────

    [Fact]
    public async Task Listado_CxP_nunca_incluye_documentos_de_otra_empresa()
    {
        var repo = new FakeAccountsPayableRepository();
        repo.Store.Add(BuildPayable(CompanyAId, BranchAId, 100m));
        repo.Store.Add(BuildPayable(CompanyBId, BranchAId, 999m)); // misma sucursal-id, otra empresa

        var partners = NamesMock();
        partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [SupplierId] = "Proveedor" });

        var handler = new GetAccountsPayablesListHandler(
            repo,
            partners.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyAId)
        );

        var result = await handler.Handle(new GetAccountsPayablesListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Total.Should().Be(1);
        result.Value.Items.Should().OnlyContain(i => i.TotalAmount == 100m);
    }

    // ── SupplierPayment: branch-independent + company isolation ──────────

    [Fact]
    public async Task Listado_de_pagos_ve_pagos_de_ambas_sucursales_pero_no_de_otra_empresa()
    {
        var repo = new FakeSupplierPaymentRepository();
        repo.Store.Add(BuildPayment(CompanyAId, BranchAId));
        repo.Store.Add(BuildPayment(CompanyAId, BranchBId));
        repo.Store.Add(BuildPayment(CompanyBId, BranchAId));

        var partners = NamesMock();
        partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [SupplierId] = "Proveedor" });

        var handler = new GetSupplierPaymentsListHandler(
            repo,
            partners.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyAId)
        );

        var result = await handler.Handle(new GetSupplierPaymentsListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Total.Should().Be(
            2,
            "pagos de la Empresa A en ambas sucursales deben verse; el de la Empresa B nunca"
        );
    }

    [Fact]
    public async Task Detalle_de_pago_de_sucursal_B_es_visible_sin_exigir_sucursal_activa()
    {
        var repo = new FakeSupplierPaymentRepository();
        var payment = BuildPayment(CompanyAId, BranchBId);
        repo.Store.Add(payment);

        // GetSupplierPaymentByIdHandler no recibe ICurrentBranch — misma prueba en tiempo de
        // compilación que en AccountsPayable.
        var handler = new GetSupplierPaymentByIdHandler(
            repo,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId)
        );

        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }
}
