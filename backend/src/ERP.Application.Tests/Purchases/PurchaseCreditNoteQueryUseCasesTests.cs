using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C.2 — <c>GetPurchaseCreditNoteByIdHandler</c>/<c>GetPurchaseCreditNoteListHandler</c>:
/// ambos resuelven exclusivamente con el <c>TenantId</c> de <c>ICurrentTenant</c> (nunca uno recibido
/// del cliente) — mismo criterio fail-closed multi-tenant que el resto del módulo Purchases.
/// </summary>
public sealed class PurchaseCreditNoteQueryUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static ICurrentTenant FixedTenant(Guid tenantId)
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(x => x.TenantId).Returns(tenantId);
        return m.Object;
    }

    private static ICurrentCompany FixedCompany()
    {
        var m = new Mock<ICurrentCompany>();
        m.SetupGet(x => x.CompanyId).Returns(CompanyId);
        return m.Object;
    }

    private static PurchaseCreditNote SampleCreditNote() =>
        PurchaseCreditNote.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            Guid.NewGuid(),
            null,
            PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m) },
            Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash"
        );

    [Fact]
    public async Task GetById_consulta_el_repositorio_con_el_TenantId_del_contexto_autenticado()
    {
        var creditNote = SampleCreditNote();
        var repo = new Mock<IPurchaseCreditNoteRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        var receptionRepo = new Mock<IPurchaseReceptionDocumentRepository>();

        var handler = new GetPurchaseCreditNoteByIdHandler(
            repo.Object,
            invoiceRepo.Object,
            Mock.Of<IAccountsPayableRepository>(),
            receptionRepo.Object,
            FixedTenant(TenantId),
            FixedCompany()
        );

        var result = await handler.Handle(
            new GetPurchaseCreditNoteByIdQuery(creditNote.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        repo.Verify(
            r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetById_nunca_consulta_con_un_TenantId_distinto_al_del_contexto()
    {
        var creditNote = SampleCreditNote();
        var otroTenantId = Guid.NewGuid();
        var repo = new Mock<IPurchaseCreditNoteRepository>();
        // Solo configurado para el tenant "correcto" — cualquier otra clave retorna null por
        // defecto de Moq, simulando el aislamiento fail-closed real del query filter de EF.
        repo.Setup(r => r.GetByIdAsync(TenantId, creditNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNote);
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        var receptionRepo = new Mock<IPurchaseReceptionDocumentRepository>();

        var handler = new GetPurchaseCreditNoteByIdHandler(
            repo.Object,
            invoiceRepo.Object,
            Mock.Of<IAccountsPayableRepository>(),
            receptionRepo.Object,
            FixedTenant(otroTenantId),
            FixedCompany()
        );

        var result = await handler.Handle(
            new GetPurchaseCreditNoteByIdQuery(creditNote.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task GetList_pagina_y_consulta_el_repositorio_con_el_TenantId_del_contexto()
    {
        var repo = new Mock<IPurchaseCreditNoteRepository>();
        repo.Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    20,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<PurchaseCreditNote> { SampleCreditNote() }, 1));

        var handler = new GetPurchaseCreditNoteListHandler(repo.Object, FixedTenant(TenantId));

        var result = await handler.Handle(new GetPurchaseCreditNoteListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        repo.Verify(
            r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    20,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
