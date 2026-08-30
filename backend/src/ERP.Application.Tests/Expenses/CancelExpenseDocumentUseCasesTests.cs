using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Expenses;

/// <summary>
/// DOCUMENT-FLOW-POLICY-01 — separación permiso/política, cubierta en dos capas independientes:
/// (1) el permiso <c>expenses.documents.cancel</c> se exige en <c>ExpensesController.Cancel</c> vía
/// <c>[Authorize(Policy = "perm:...")]</c> — middleware de ASP.NET que corre ANTES de que el
/// request llegue a <see cref="CancelExpenseDocumentHandler"/> (ver
/// <c>ExpensesControllerTests.Cada_endpoint_expone_su_permiso_propio</c>, que verifica ese
/// atributo existe para <c>Cancel</c>). Un usuario sin ese permiso nunca llega a este handler, sin
/// importar lo que diga la política — "usuario sin permiso no puede anular aunque la política lo
/// permita" se cumple por construcción de la tubería HTTP, no por lógica del handler.
/// (2) <see cref="CancelExpenseDocumentHandler"/> en sí mismo no conoce permisos — no recibe ni
/// consulta ningún concepto de autorización de acciones; solo consulta
/// <c>IDocumentFlowPolicyService</c>. Los tests de este archivo que bloquean por
/// <c>CancellationMode.NotAllowed</c> o motivo faltante demuestran que la política bloquea
/// igual con un <c>ICurrentUser</c> mockeado arbitrario — "usuario con permiso no puede anular si
/// la política no lo permite" se cumple porque el handler nunca deja pasar una política que lo
/// prohíbe, sin importar el usuario.
/// </summary>
public sealed class CancelExpenseDocumentUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ExpenseSubcategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();

    [Fact]
    public async Task Cancelar_gasto_confirmado_sin_CxP_pasa_a_Cancelled()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        fx.SetupNoPayable(document.Id);

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Documento duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Cancelled);
        result.Value.CancelReason.Should().Be("Documento duplicado");
        fx.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        fx.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        fx.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_gasto_confirmado_con_CxP_sin_pagos_anula_tambien_la_CxP()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        var payable = fx.SetupPayable(document.Id, document.GrandTotal);

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Documento duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        payable.Status.Should().Be(AccountsPayableStatus.Cancelled);
    }

    [Fact]
    public async Task Cancelar_gasto_con_CxP_con_pagos_aplicados_se_bloquea_con_422_claro()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        var payable = fx.SetupPayable(document.Id, document.GrandTotal);
        payable.RegisterPayment(50m, UserId);

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Documento duplicado"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("pagos registrados");
        document.Status.Should().Be(ExpenseStatus.Confirmed, "el gasto no debe anularse si la CxP no pudo anularse");
        fx.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fx.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_gasto_en_Draft_se_bloquea()
    {
        var fx = new Fixture();
        var document = fx.DraftDocument();
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("confirmados");
        fx.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_gasto_ya_Cancelled_se_bloquea()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        document.Cancel("Primera anulación", UserId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Segunda anulación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Cancelar_gasto_inexistente_devuelve_NotFound()
    {
        var fx = new Fixture();
        fx.Docs
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpenseDocument?)null);

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(Guid.NewGuid(), "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public void Validator_exige_motivo_no_vacio()
    {
        var result = new CancelExpenseDocumentValidator().Validate(
            new CancelExpenseDocumentCommand(Guid.NewGuid(), "")
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelExpenseDocumentCommand.Reason));
    }

    [Fact]
    public async Task Si_falla_el_reverso_contable_la_anulacion_falla_y_hace_rollback()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        fx.SetupNoPayable(document.Id);
        fx.Docs
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExpensePostingFailedException("No se encontró el asiento a reversar.", "JOURNAL_ENTRY_NOT_FOUND"));

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("JOURNAL_ENTRY_NOT_FOUND");
        fx.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fx.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_bloqueado_cuando_politica_CancellationMode_es_NotAllowed()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureCancellationFlowAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(DocumentFlowPolicyViolationException.CancellationNotAllowed());

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La política de flujo documental no permite anular este tipo de documento.");
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_exige_motivo_cuando_politica_RequiresCancellationReason_es_true()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureCancellationFlowAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(DocumentFlowPolicyViolationException.CancellationReasonRequired());

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("El motivo de anulación es obligatorio según la política de flujo documental.");
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_no_reversa_CxP_cuando_politica_CancellationMode_no_es_AllowedAfterConfirmationWithReversal()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument();
        fx.SetupDocument(document);
        var payable = fx.SetupPayable(document.Id, document.GrandTotal);
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureCancellationFlowAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new DocumentFlowPolicyResult(
                    DocTypeCodes.ExpenseDocument,
                    IsActive: true,
                    CreationMode.DraftRequired,
                    ConfirmationMode.ManualConfirmation,
                    AuthorizationMode.None,
                    PendingDocumentMode.None,
                    CancellationMode.AllowedBeforeConfirmation,
                    RequiresCancellationReason: true,
                    RequiresAttachment: false,
                    RequiresSupplier: true,
                    RequiresDueDate: true,
                    PayableGenerationMode.OnConfirmation,
                    AccountingPostingMode.OnConfirmation,
                    InventoryImpactMode.None,
                    NotificationMode.None
                )
            );

        var result = await fx.Handler.Handle(
            new CancelExpenseDocumentCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        payable.Status.Should().NotBe(ERP.Domain.Modules.Payables.Enums.AccountsPayableStatus.Cancelled);
    }

    private sealed class Fixture
    {
        public Mock<IExpenseDocumentRepository> Docs { get; } = new();
        public Mock<IAccountsPayableRepository> PayableRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IDocumentFlowPolicyService> WorkflowPolicy { get; } = new();

        public CancelExpenseDocumentHandler Handler =>
            new(
                Docs.Object,
                PayableRepo.Object,
                Uow.Object,
                WorkflowPolicy.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                NullLogger<CancelExpenseDocumentHandler>.Instance
            );

        public Fixture()
        {
            Docs.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            WorkflowPolicy
                .Setup(w =>
                    w.EnsureCancellationFlowAsync(
                        CompanyId,
                        DocTypeCodes.ExpenseDocument,
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    new DocumentFlowPolicyResult(
                        DocTypeCodes.ExpenseDocument,
                        IsActive: true,
                        CreationMode.DraftRequired,
                        ConfirmationMode.ManualConfirmation,
                        AuthorizationMode.None,
                        PendingDocumentMode.None,
                        CancellationMode.AllowedAfterConfirmationWithReversal,
                        RequiresCancellationReason: true,
                        RequiresAttachment: false,
                        RequiresSupplier: true,
                        RequiresDueDate: true,
                        PayableGenerationMode.OnConfirmation,
                        AccountingPostingMode.OnConfirmation,
                        InventoryImpactMode.None,
                        NotificationMode.None
                    )
                );
        }

        public ExpenseDocument DraftDocument() =>
            ExpenseDocument.CreateDraft(
                TenantId, CompanyId, BranchId, SupplierId, "Proveedor Demo", "1791352688001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), "01", "001-001-000000123",
                Guid.NewGuid(), "Contado", 1, 0, UserId
            );

        public ExpenseDocument ConfirmedDocument()
        {
            var document = DraftDocument();
            var line = ExpenseLine.Create(
                document.Id, TenantId, ExpenseSubcategoryId, ExpenseAccountId,
                "Internet", 1m, 100m, "0"
            );
            document.ReplaceLines([line], UserId);
            document.Confirm(
                new Dictionary<Guid, (Guid, string?, string?)>
                {
                    [line.Id] = (ExpenseAccountId, "6.1.01", "Internet"),
                },
                UserId
            );
            return document;
        }

        public void SetupDocument(ExpenseDocument document) =>
            Docs
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

        public void SetupNoPayable(Guid expenseDocumentId) =>
            PayableRepo
                .Setup(r =>
                    r.GetByOriginAsync(
                        TenantId,
                        CompanyId,
                        AccountsPayableOriginType.ExpenseDocument,
                        expenseDocumentId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((AccountsPayable?)null);

        public AccountsPayable SetupPayable(Guid expenseDocumentId, decimal grandTotal)
        {
            var payable = AccountsPayable.CreateFromOrigin(
                TenantId, CompanyId, BranchId, SupplierId,
                AccountsPayableOriginType.ExpenseDocument, expenseDocumentId,
                "01", "001-001-000000123",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
            );
            payable.AddInstallment(1, new DateOnly(2026, 8, 27), grandTotal);

            PayableRepo
                .Setup(r =>
                    r.GetByOriginAsync(
                        TenantId,
                        CompanyId,
                        AccountsPayableOriginType.ExpenseDocument,
                        expenseDocumentId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(payable);

            return payable;
        }
    }
}
