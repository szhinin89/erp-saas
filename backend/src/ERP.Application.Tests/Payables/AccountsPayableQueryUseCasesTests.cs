using ERP.Application.Common;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

/// <summary>
/// PAYABLES-READ-API-11 — cubre la API de lectura genérica de CxP: listado (con todos sus
/// filtros) y detalle. AccountsPayable/AccountsPayableInstallment son la única fuente de saldo
/// desde PAYABLES-PURCHASE-MIGRATION-10 (PurchasePayable fue eliminado) — estos tests verifican
/// explícitamente que los montos expuestos en los DTOs se derivan de las cuotas, nunca de un
/// acumulador de cabecera ni de PurchaseInvoice/ExpenseDocument.
/// </summary>
public sealed class AccountsPayableQueryUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AccountsPayable BuildPayable(
        AccountsPayableOriginType originType,
        Guid supplierId,
        decimal amount,
        string documentNumber = "001-001-000000001"
    )
    {
        var issueDate = new DateOnly(2026, 8, 1);
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId,
            CompanyId,
            BranchId,
            supplierId,
            originType,
            Guid.NewGuid(),
            "01",
            documentNumber,
            issueDate,
            issueDate,
            UserId
        );
        payable.AddInstallment(1, issueDate.AddDays(30), amount);
        return payable;
    }

    private sealed class Mocks
    {
        public Mock<IAccountsPayableRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerRepository> Partners { get; } = new();
        public FixedCurrentTenant Tenant { get; } = new(TenantId);
        public FixedCurrentCompany Company { get; } = new(CompanyId);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId { get; } = companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    // ── Listar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_CxP_de_compras()
    {
        var supplierId = Guid.NewGuid();
        var payable = BuildPayable(AccountsPayableOriginType.PurchaseInvoice, supplierId, 100m);
        var m = new Mocks();
        m.Repo
            .Setup(r =>
                r.SearchAsync(
                    TenantId, CompanyId, null, null, null, null, null, null, 1, 25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<AccountsPayable> { payable }, 1));
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [supplierId] = "Proveedor Uno" });

        var handler = new GetAccountsPayablesListHandler(m.Repo.Object, m.Partners.Object, m.Tenant, m.Company);
        var result = await handler.Handle(new GetAccountsPayablesListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(i => i.OriginType == "PurchaseInvoice");
        result.Value.Items[0].SupplierName.Should().Be("Proveedor Uno");
    }

    [Fact]
    public async Task Listar_CxP_de_gastos()
    {
        var supplierId = Guid.NewGuid();
        var payable = BuildPayable(AccountsPayableOriginType.ExpenseDocument, supplierId, 55m);
        var m = new Mocks();
        m.Repo
            .Setup(r =>
                r.SearchAsync(
                    TenantId, CompanyId, null, null, null, null, null, null, 1, 25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<AccountsPayable> { payable }, 1));
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [supplierId] = "Proveedor Dos" });

        var handler = new GetAccountsPayablesListHandler(m.Repo.Object, m.Partners.Object, m.Tenant, m.Company);
        var result = await handler.Handle(new GetAccountsPayablesListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(i => i.OriginType == "ExpenseDocument");
    }

    [Fact]
    public async Task Filtrar_por_OriginType_PurchaseInvoice_traduce_el_string_al_enum_correcto()
    {
        var m = new Mocks();
        AccountsPayableOriginType? capturedOriginType = null;
        m.Repo
            .Setup(r =>
                r.SearchAsync(
                    TenantId, CompanyId, It.IsAny<AccountsPayableOriginType?>(), null, null, null, null, null, 1, 25,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                Guid, Guid, AccountsPayableOriginType?, AccountsPayableStatus?, Guid?, DateOnly?, DateOnly?,
                string?, int, int, CancellationToken
            >((_, _, originType, _, _, _, _, _, _, _, _) => capturedOriginType = originType)
            .ReturnsAsync((new List<AccountsPayable>(), 0));
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var handler = new GetAccountsPayablesListHandler(m.Repo.Object, m.Partners.Object, m.Tenant, m.Company);
        await handler.Handle(
            new GetAccountsPayablesListQuery(OriginType: "PurchaseInvoice"),
            CancellationToken.None
        );

        capturedOriginType.Should().Be(AccountsPayableOriginType.PurchaseInvoice);
    }

    [Fact]
    public async Task Filtrar_por_OriginType_ExpenseDocument_traduce_el_string_al_enum_correcto()
    {
        var m = new Mocks();
        AccountsPayableOriginType? capturedOriginType = null;
        m.Repo
            .Setup(r =>
                r.SearchAsync(
                    TenantId, CompanyId, It.IsAny<AccountsPayableOriginType?>(), null, null, null, null, null, 1, 25,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                Guid, Guid, AccountsPayableOriginType?, AccountsPayableStatus?, Guid?, DateOnly?, DateOnly?,
                string?, int, int, CancellationToken
            >((_, _, originType, _, _, _, _, _, _, _, _) => capturedOriginType = originType)
            .ReturnsAsync((new List<AccountsPayable>(), 0));
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var handler = new GetAccountsPayablesListHandler(m.Repo.Object, m.Partners.Object, m.Tenant, m.Company);
        await handler.Handle(
            new GetAccountsPayablesListQuery(OriginType: "ExpenseDocument"),
            CancellationToken.None
        );

        capturedOriginType.Should().Be(AccountsPayableOriginType.ExpenseDocument);
    }

    [Fact]
    public async Task Filtrar_por_Status_traduce_el_string_al_enum_correcto()
    {
        var m = new Mocks();
        AccountsPayableStatus? capturedStatus = null;
        m.Repo
            .Setup(r =>
                r.SearchAsync(
                    TenantId, CompanyId, null, It.IsAny<AccountsPayableStatus?>(), null, null, null, null, 1, 25,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                Guid, Guid, AccountsPayableOriginType?, AccountsPayableStatus?, Guid?, DateOnly?, DateOnly?,
                string?, int, int, CancellationToken
            >((_, _, _, status, _, _, _, _, _, _, _) => capturedStatus = status)
            .ReturnsAsync((new List<AccountsPayable>(), 0));
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var handler = new GetAccountsPayablesListHandler(m.Repo.Object, m.Partners.Object, m.Tenant, m.Company);
        await handler.Handle(new GetAccountsPayablesListQuery(Status: "PartiallyPaid"), CancellationToken.None);

        capturedStatus.Should().Be(AccountsPayableStatus.PartiallyPaid);
    }

    // ── Detalle ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Obtener_detalle_con_cuotas()
    {
        var supplierId = Guid.NewGuid();
        var payable = BuildPayable(AccountsPayableOriginType.PurchaseInvoice, supplierId, 200m);
        payable.RegisterPayment(50m, UserId);

        var m = new Mocks();
        m.Repo.Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payable);
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [supplierId] = "Proveedor Tres" });

        var handler = new GetAccountsPayableByIdHandler(m.Repo.Object, m.Partners.Object, m.Tenant);
        var result = await handler.Handle(new GetAccountsPayableByIdQuery(payable.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Installments.Should().ContainSingle();
        result.Value.Installments[0].Amount.Should().Be(200m);
        result.Value.Installments[0].PaidAmount.Should().Be(50m);
        result.Value.Installments[0].OutstandingAmount.Should().Be(150m);
        result.Value.SupplierName.Should().Be("Proveedor Tres");
    }

    [Fact]
    public async Task Detalle_de_CxP_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.Repo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountsPayable?)null);

        var handler = new GetAccountsPayableByIdHandler(m.Repo.Object, m.Partners.Object, m.Tenant);
        var result = await handler.Handle(new GetAccountsPayableByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    // ── Saldos derivados de Installments ─────────────────────────────────────

    [Fact]
    public async Task Verificar_que_los_saldos_del_listado_y_el_detalle_vienen_de_Installments()
    {
        var supplierId = Guid.NewGuid();
        var payable = BuildPayable(AccountsPayableOriginType.PurchaseInvoice, supplierId, 300m);
        payable.RegisterPayment(120m, UserId);

        // El header (TotalAmount/PaidAmount/OutstandingAmount) no tiene columnas propias — son
        // getters calculados sumando Installments (ver AccountsPayable.cs). Este test lo prueba
        // extremo a extremo desde los DTOs: si algún día alguien reintrodujera un acumulador de
        // cabecera desincronizado, el mismatch entre cuota y DTO lo detectaría aquí.
        var m = new Mocks();
        m.Repo
            .Setup(r =>
                r.SearchAsync(
                    TenantId, CompanyId, null, null, null, null, null, null, 1, 25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<AccountsPayable> { payable }, 1));
        m.Repo.Setup(r => r.GetByIdAsync(TenantId, payable.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payable);
        m.Partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [supplierId] = "Proveedor Cuatro" });

        var listHandler = new GetAccountsPayablesListHandler(m.Repo.Object, m.Partners.Object, m.Tenant, m.Company);
        var listResult = await listHandler.Handle(new GetAccountsPayablesListQuery(), CancellationToken.None);

        var expectedOutstanding = payable.Installments.Sum(i => i.OutstandingAmount);
        listResult.Value!.Items[0].PaidAmount.Should().Be(payable.Installments.Sum(i => i.PaidAmount));
        listResult.Value.Items[0].OutstandingAmount.Should().Be(expectedOutstanding);
        listResult.Value.Items[0].OutstandingAmount.Should().Be(180m);

        var detailHandler = new GetAccountsPayableByIdHandler(m.Repo.Object, m.Partners.Object, m.Tenant);
        var detailResult = await detailHandler.Handle(
            new GetAccountsPayableByIdQuery(payable.Id),
            CancellationToken.None
        );

        detailResult.Value!.OutstandingAmount.Should().Be(expectedOutstanding);
        detailResult.Value.Installments[0].OutstandingAmount.Should().Be(expectedOutstanding);
    }

    // ── Sin referencias a PurchasePayable ─────────────────────────────────────

    [Fact]
    public void No_existen_referencias_al_tipo_PurchasePayable_eliminado()
    {
        // PAYABLES-PURCHASE-MIGRATION-10 eliminó físicamente PurchasePayable/PurchasePayableInstallment.
        // Guard de regresión: si alguien los reintrodujera en ERP.Domain, este test fallaría —
        // la API de lectura genérica (y todo Compras/Gastos) debe seguir dependiendo únicamente
        // de AccountsPayable/AccountsPayableInstallment.
        var domainAssembly = typeof(AccountsPayable).Assembly;
        var offendingTypes = domainAssembly
            .GetTypes()
            .Where(t => t.Name is "PurchasePayable" or "PurchasePayableInstallment")
            .ToList();

        offendingTypes.Should().BeEmpty();
    }

    [Fact]
    public void No_existen_referencias_al_repositorio_legacy_IPurchasePayableRepository()
    {
        // PAYABLES-LEGACY-CLEANUP-13 — IPurchasePayableRepository/PurchasePayableRepository y el
        // use case legacy PurchasePayableUseCases (con sus DTOs PurchasePayableDto/
        // GetPayablesListQuery/etc.) fueron eliminados por completo: AccountsPayableRepository/
        // IAccountsPayableRepository son la única fuente de acceso a datos de CxP.
        var forbiddenNames = new[]
        {
            "IPurchasePayableRepository",
            "PurchasePayableRepository",
            "PurchasePayableUseCases",
            "PurchasePayableDto",
            "PurchasePayableInstallmentDto",
            "GetPayableByIdQuery",
            "GetPayablesListQuery",
            "PayablesListResponse",
        };

        var assemblies = new[]
        {
            typeof(AccountsPayable).Assembly, // ERP.Domain
            typeof(GetAccountsPayableByIdQuery).Assembly, // ERP.Application
            typeof(ERP.Infrastructure.Persistence.Repositories.Payables.AccountsPayableRepository).Assembly, // ERP.Infrastructure
        };

        var offending = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => forbiddenNames.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        offending.Should().BeEmpty();
    }
}
