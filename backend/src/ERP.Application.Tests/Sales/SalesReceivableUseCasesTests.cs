using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// FINANCE-RECEIVABLES-LIST-ENTERPRISE-01 — la grilla de Cuentas por Cobrar mostraba
/// <c>CustomerId</c> crudo (GUID) y un badge de estado con literal genérico "Estado" porque
/// <c>SalesReceivableDto</c> solo traía columnas propias de <c>sales_receivables</c>, sin
/// resolver la factura/cliente/sucursal/usuario de origen. Estos tests cubren el mapeo
/// enriquecido (<see cref="SalesReceivableDtoMapper"/>) y el wiring del handler de listado.
/// </summary>
public sealed class SalesReceivableUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CreatedByUserId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static SalesReceivable CreateReceivable(decimal originalAmount = 100m) =>
        SalesReceivable.Create(TenantId, CompanyId, InvoiceId, CustomerId, originalAmount, ActorId);

    private static (
        string InvoiceNumber,
        string CustomerName,
        string CustomerTaxId,
        string CustomerIdentificationType,
        Guid BranchId,
        Guid CreatedBy,
        DateOnly IssueDate,
        DateTime CreatedAt
    ) CreateSummary() =>
        (
            "001-001-000000014",
            "Zhinin",
            "0302126842001",
            "04",
            BranchId,
            CreatedByUserId,
            new DateOnly(2026, 8, 17),
            new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc)
        );

    // ── SalesReceivableDtoMapper — mapeo puro ──────────────────────────────

    [Fact]
    public void Build_resuelve_nombre_de_cliente_y_numero_de_factura_en_vez_de_GUID()
    {
        var r = CreateReceivable();
        var dto = SalesReceivableDtoMapper.Build(
            r,
            CreateSummary(),
            "Sucursal Principal",
            "Admin ZH",
            new DateOnly(2026, 8, 17)
        );

        dto.CustomerName.Should().Be("Zhinin");
        dto.CustomerName.Should().NotBe(CustomerId.ToString());
        dto.InvoiceNumber.Should().Be("001-001-000000014");
        dto.CustomerIdentification.Should().Contain("0302126842001");
        dto.BranchName.Should().Be("Sucursal Principal");
        dto.CreatedByName.Should().Be("Admin ZH");
    }

    [Fact]
    public void Build_sin_resumen_de_factura_no_muestra_GUID_usa_fallback_textual()
    {
        var r = CreateReceivable();
        var dto = SalesReceivableDtoMapper.Build(
            r,
            null,
            null,
            null,
            new DateOnly(2026, 8, 17)
        );

        dto.CustomerName.Should().NotBe(CustomerId.ToString());
        dto.CustomerName.Should().Be("Cliente no disponible");
        dto.InvoiceNumber.Should().Be("—");
    }

    [Fact]
    public void Build_pendiente_sin_pagos_y_sin_mora_es_Pendiente()
    {
        var r = CreateReceivable(100m);
        r.GenerateInstallments(new DateOnly(2026, 8, 1), creditTermDays: 30, installmentCount: 1);

        var dto = SalesReceivableDtoMapper.Build(
            r,
            CreateSummary(),
            "Sucursal Principal",
            "Admin ZH",
            new DateOnly(2026, 8, 17) // antes del vencimiento (2026-08-31)
        );

        dto.StatusLabel.Should().Be("Pendiente");
        dto.OverdueDays.Should().BeNull();
    }

    [Fact]
    public void Build_con_pago_parcial_es_Parcial()
    {
        var r = CreateReceivable(100m);
        r.GenerateInstallments(new DateOnly(2026, 8, 1), creditTermDays: 30, installmentCount: 1);
        r.RegisterCollection(40m, ActorId);

        var dto = SalesReceivableDtoMapper.Build(
            r,
            CreateSummary(),
            "Sucursal Principal",
            "Admin ZH",
            new DateOnly(2026, 8, 17)
        );

        dto.StatusLabel.Should().Be("Parcial");
        dto.BalanceDue.Should().Be(60m);
    }

    [Fact]
    public void Build_saldo_en_cero_es_Pagada_aunque_status_persistido_siga_pending()
    {
        // SalesReceivable.Status nunca transiciona a "paid" (RegisterCollection solo acumula
        // PaidAmount) — la causa raíz del bug original: el saldo en cero es la única señal real.
        var r = CreateReceivable(100m);
        r.GenerateInstallments(new DateOnly(2026, 8, 1), creditTermDays: 30, installmentCount: 1);
        r.RegisterCollection(100m, ActorId);

        r.Status.Should().Be("pending", "el status crudo nunca cambia a 'paid'");

        var dto = SalesReceivableDtoMapper.Build(
            r,
            CreateSummary(),
            "Sucursal Principal",
            "Admin ZH",
            new DateOnly(2026, 8, 17)
        );

        dto.StatusLabel.Should().Be("Pagada");
        dto.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void Build_con_cuota_vencida_es_Vencida_y_calcula_OverdueDays()
    {
        var r = CreateReceivable(100m);
        r.GenerateInstallments(new DateOnly(2026, 7, 1), creditTermDays: 15, installmentCount: 1);
        // Vence 2026-07-16; "hoy" = 2026-08-17 → 32 días de mora.

        var dto = SalesReceivableDtoMapper.Build(
            r,
            CreateSummary(),
            "Sucursal Principal",
            "Admin ZH",
            new DateOnly(2026, 8, 17)
        );

        dto.StatusLabel.Should().Be("Vencida");
        dto.OverdueDays.Should().Be(32);
        dto.DueDate.Should().Be(new DateOnly(2026, 7, 16));
    }

    [Fact]
    public void Build_cancelada_es_Anulada_sin_importar_mora()
    {
        var r = CreateReceivable(100m);
        r.Cancel(ActorId);

        var dto = SalesReceivableDtoMapper.Build(
            r,
            CreateSummary(),
            "Sucursal Principal",
            "Admin ZH",
            new DateOnly(2026, 8, 17)
        );

        dto.StatusLabel.Should().Be("Anulada");
    }

    // ── GetReceivablesListHandler — wiring de resolución de nombres ────────

    private static GetReceivablesListHandler BuildHandler(
        IReadOnlyList<SalesReceivable> items,
        int total
    )
    {
        var repo = new Mock<ISalesReceivableRepository>();
        repo.Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((items, total));

        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r =>
                r.GetReceivableSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<
                    Guid,
                    (
                        string InvoiceNumber,
                        string CustomerName,
                        string CustomerTaxId,
                        string CustomerIdentificationType,
                        Guid BranchId,
                        Guid CreatedBy,
                        DateOnly IssueDate,
                        DateTime CreatedAt
                    )
                >
                {
                    [InvoiceId] = CreateSummary(),
                }
            );

        var branch = Branch.Create(
            tenantId: TenantId,
            name: "Sucursal Principal",
            address: "Av. Test",
            code: "001",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: false,
            createdBy: ActorId,
            companyId: CompanyId
        );
        typeof(Branch).GetProperty("Id")!.SetValue(branch, BranchId);
        var branchRepo = new Mock<IBranchRepository>();
        branchRepo
            .Setup(r =>
                r.GetAsync(TenantId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<Branch> { branch });

        var user = IdentityUser.Create(
            "admin.zh",
            "Admin",
            "ZH",
            "admin@zh.com",
            "hash",
            ActorId
        );
        typeof(IdentityUser).GetProperty("Id")!.SetValue(user, CreatedByUserId);
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetUsersByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<IdentityUser> { user });

        var clock = new Mock<ICompanyClock>();
        clock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateOnly(2026, 8, 17));

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);

        return new GetReceivablesListHandler(
            repo.Object,
            invoiceRepo.Object,
            branchRepo.Object,
            accessRepo.Object,
            clock.Object,
            tenant.Object,
            company.Object
        );
    }

    [Fact]
    public async Task Handle_resuelve_sucursal_y_usuario_por_lote_no_por_fila()
    {
        var r = CreateReceivable(100m);
        r.GenerateInstallments(new DateOnly(2026, 8, 1), creditTermDays: 30, installmentCount: 1);
        var handler = BuildHandler(new[] { r }, 1);

        var result = await handler.Handle(new GetReceivablesListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!.Items.Single();
        dto.CustomerName.Should().Be("Zhinin");
        dto.BranchName.Should().Be("Sucursal Principal");
        dto.CreatedByName.Should().Be("Admin ZH");
    }
}
