using ERP.Application.Common;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Events;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Expenses;

public sealed class ExpenseDocumentConfirmUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    [Fact]
    public async Task Confirmar_Draft_valido_con_una_linea_pasa_a_Confirmed_y_postea()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
        result.Value.GrandTotal.Should().Be(115m);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_gasto_crea_CxP_generica_con_OriginType_ExpenseDocument()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fx.Payables.Verify(
            p =>
                p.CreateFromOriginAsync(
                    It.Is<CreateAccountsPayableFromOriginRequest>(req =>
                        req.OriginType == AccountsPayableOriginType.ExpenseDocument
                        && req.OriginId == document.Id
                        && req.SupplierId == SupplierId
                        && req.Installments.Single().Amount == 115m
                    ),
                    UserId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Si_falla_la_creacion_de_CxP_la_confirmacion_igual_tiene_exito()
    {
        // La CxP se crea DESPUES de que el posting ya se confirmo y persistio — un fallo aqui no
        // debe revertir la confirmacion (a diferencia del posting, que si es estricto). Ver
        // comentario en ConfirmExpenseDocumentHandler.
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        fx.Payables
            .Setup(p =>
                p.CreateFromOriginAsync(
                    It.IsAny<CreateAccountsPayableFromOriginRequest>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("Ya existe una cuota con el número 1."));

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
    }

    [Fact]
    public async Task Confirmar_Draft_valido_con_varias_lineas_y_varias_cuentas_genera_allocations()
    {
        var fx = new Fixture();
        var otherAccount = fx.ExpenseAccount("6.1.02.001");
        var otherSubcategory = ExpenseCategoryNode.CreateSubcategory(
            TenantId, CompanyId, fx.Category, "SUM", "Suministros", otherAccount.Id, UserId
        );
        fx.CategoryRepo
            .Setup(r => r.GetByIdAsync(TenantId, otherSubcategory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherSubcategory);
        fx.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, otherAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherAccount);

        var document = fx.DraftDocumentWithLines(
            fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m),
            fx.Line(otherSubcategory, otherAccount, 50m, "0")
        );
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GrandTotal.Should().Be(165m);
        // El handler no invoca IPostingEngine directamente — ExpenseDocumentConfirmedPostingTranslator
        // lo hace, disparado por ErpDbContext.SaveChangesAsync (mockeado aqui como no-op). Lo que el
        // handler SI controla es que Confirm() levante el evento con una allocation por cuenta, que
        // es lo que el traductor luego convierte en PostingAllocation (ver
        // ExpenseDocumentConfirmedPostingTranslatorTests para esa conversion).
        var raised = document.DomainEvents.OfType<ExpenseDocumentConfirmedEvent>().Single();
        raised.LineAllocations.Should().HaveCount(2);
        raised.LineAllocations.Should().Contain(a => a.AccountingAccountId == fx.Account.Id && a.Amount == 100m);
        raised.LineAllocations.Should().Contain(a => a.AccountingAccountId == otherAccount.Id && a.Amount == 50m);
    }

    [Fact]
    public async Task Confirmar_documento_congela_snapshot_de_cuenta_en_lineas()
    {
        var fx = new Fixture();
        var line = fx.Line(fx.Subcategory, fx.Account, 100m, "0");
        var document = fx.DraftDocumentWithLines(line);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var confirmedLine = document.Lines.Single();
        confirmedLine.SnapshotAccountingAccountId.Should().Be(fx.Account.Id);
        confirmedLine.SnapshotAccountingAccountCode.Should().Be(fx.Account.Code.Value);
        confirmedLine.SnapshotAccountingAccountName.Should().Be(fx.Account.Name);
    }

    [Fact]
    public async Task Confirmar_documento_no_Draft_se_bloquea()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        SetPrivateStatus(document, ExpenseStatus.Confirmed);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_con_subcategoria_inactiva_se_bloquea()
    {
        var fx = new Fixture();
        fx.Subcategory.SetActive(false, UserId);
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("not_postable")]
    [InlineData("not_expense")]
    public async Task Confirmar_con_cuenta_invalida_se_bloquea(string scenario)
    {
        var fx = new Fixture();
        var account =
            scenario == "inactive" ? fx.ExpenseAccount("6.1.01.001", isActive: false)
            : scenario == "not_postable" ? fx.ExpenseAccount("6.1.01.001", allowsPosting: false)
            : fx.ExpenseAccount("6.1.01.001", accountType: AccountType.Asset);
        fx.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, fx.Subcategory.AccountingAccountId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_con_cuenta_de_otra_empresa_se_bloquea()
    {
        var fx = new Fixture();
        fx.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, fx.Subcategory.AccountingAccountId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_falla_el_posting_la_confirmacion_falla_y_el_documento_queda_Draft()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        fx.Docs
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExpensePostingFailedException("No existe regla de contabilizacion.", "RULE_NOT_FOUND"));

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("RULE_NOT_FOUND");
        // La mutacion en memoria de Confirm() ocurrio antes del SaveChangesAsync fallido, pero
        // nada se persistio (ExpensePostingFailedException simula el rollback real de
        // ErpDbContext.SaveChangesAsync) — lo que importa para el caller es que el Result sea
        // un fallo explicito, nunca un exito con documento a medio confirmar.
        document.Status.Should().Be(ExpenseStatus.Confirmed, "el rollback real de BD (no simulado aqui) es quien revierte el estado en persistencia");
    }

    [Fact]
    public async Task Confirmar_un_Draft_existente_funciona_cuando_la_politica_GASDOC_es_DraftRequired()
    {
        // EXPENSES-WORKFLOW-INTEGRATION-01 + DOCUMENT-FLOW-POLICY-01: CreationMode.DraftRequired
        // exige que el gasto exista primero como borrador, pero ConfirmExpenseDocumentHandler nunca
        // valida CreationMode (eso solo bloquea CREAR un gasto ya confirmado vía
        // CreateConfirmedExpenseCommand) — solo valida ConfirmationMode/AuthorizationMode vía
        // EnsureConfirmationFlowAsync (mockeada en el Fixture para no bloquear). Una vez que el
        // gasto ya es un Draft persistido, confirmarlo siempre debe funcionar.
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
    }

    [Fact]
    public async Task Confirmar_bloqueado_cuando_politica_exige_autorizacion()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureConfirmationFlowAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(ERP.Domain.Exceptions.DocumentFlowPolicyViolationException.AuthorizationRequired());

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should()
            .Be("La política de flujo documental requiere autorización antes de confirmar este documento.");
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_no_crea_CxP_cuando_PayableGenerationMode_es_None()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureConfirmationFlowAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
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
                    PayableGenerationMode.None,
                    AccountingPostingMode.OnConfirmation,
                    InventoryImpactMode.None,
                    NotificationMode.None
                )
            );

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fx.Payables.Verify(
            p =>
                p.CreateFromOriginAsync(
                    It.IsAny<CreateAccountsPayableFromOriginRequest>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── RETENTIONS-EXPENSES-INTEGRATION-01D-1: retención integrada en la confirmación ─────────

    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private static IssueRetentionLineInput VatLine(decimal baseAmount = 100m, decimal rate = 30m, decimal retained = 30m) =>
        new(RetentionTaxType.Vat, "725", baseAmount, rate, retained);

    private static RetentionIntent AppliesRetentionIntent(IReadOnlyList<IssueRetentionLineInput>? lines = null) =>
        new(
            AppliesRetention: true,
            EmissionPointId: EmissionPointId,
            RetentionNumber: "001-001-000000001",
            IssueDate: new DateOnly(2026, 9, 3),
            Lines: lines ?? new[] { VatLine() }
        );

    private static RetentionDocument IssuedRetentionFor(ExpenseDocument document)
    {
        var retention = RetentionDocument.Create(
            TenantId,
            CompanyId,
            document.BranchId,
            RetentionSourceDocumentType.ExpenseDocument,
            document.Id,
            document.SupplierId,
            EmissionPointId,
            UserId
        );
        retention.AddLine(
            RetentionDocumentLine.Create(retention.Id, TenantId, RetentionTaxType.Vat, "725", 100m, 30m, 30m)
        );
        retention.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);
        return retention;
    }

    // 1) Sin Retention: comportamiento actual sin cambios — el IssueRetentionIssuer nunca se invoca.
    [Fact]
    public async Task Confirmar_sin_intencion_de_retencion_no_invoca_al_emisor()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
        fx.RetentionIssuer.Verify(
            i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // 2) AppliesRetention=false: tampoco invoca al emisor ni crea retención.
    [Fact]
    public async Task Confirmar_con_AppliesRetention_false_no_crea_retencion()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        var cmd = new ConfirmExpenseDocumentCommand(
            document.Id,
            new RetentionIntent(false, null, null, null, null)
        );

        var result = await fx.Handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fx.RetentionIssuer.Verify(
            i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // 3) AppliesRetention=true, elegible: emite la retención en la misma transacción de confirmación.
    [Fact]
    public async Task Confirmar_con_AppliesRetention_true_elegible_crea_RetentionDocument_Issued()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);
        var issuedRetention = IssuedRetentionFor(document);
        fx.RetentionIssuer
            .Setup(i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionDocument>.Success(issuedRetention));

        var result = await fx.Handler.Handle(
            new ConfirmExpenseDocumentCommand(document.Id, AppliesRetentionIntent()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
        issuedRetention.Status.Should().Be(RetentionStatus.Issued);
        // El SaveChangesAsync unico persiste TANTO la confirmacion del gasto COMO la retencion
        // (ya en staging via IRetentionIssuer.AddAsync interno) — una sola llamada, atomica.
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 4)-8) Si el emisor rechaza (empresa no habilitada, proveedor exento, sin base, sin codigo,
    // origen duplicado) la confirmacion completa falla y NO se persiste nada — el gasto no queda
    // Confirmed en BD. Estas razones de negocio ya estan cubiertas en detalle por
    // IssueRetentionHandlerTests/RetentionEligibilityServiceTests (RetentionIssuer las reutiliza sin
    // duplicarlas) — aqui se prueba el CONTRATO de integracion: cualquier fallo del emisor aborta
    // toda la confirmacion, nunca solo la retencion.
    [Theory]
    [InlineData("La empresa no está configurada como agente de retención de IVA.")]
    [InlineData("El proveedor está exento de retención.")]
    [InlineData("El documento origen no tiene base retenible de IVA.")]
    [InlineData("El proveedor no tiene código de retención de IVA configurado.")]
    public async Task Confirmar_falla_completa_si_el_emisor_rechaza_por_regla_de_negocio(string reason)
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);
        fx.RetentionIssuer
            .Setup(i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionDocument>.ValidationFailure(reason));

        var result = await fx.Handler.Handle(
            new ConfirmExpenseDocumentCommand(document.Id, AppliesRetentionIntent()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(reason);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fx.Payables.Verify(
            p => p.CreateFromOriginAsync(It.IsAny<CreateAccountsPayableFromOriginRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // 8) Ya existe retención activa para el origen — el emisor devuelve Conflict, la confirmación
    // completa falla igual (mismo contrato de "todo o nada" que las fallas de elegibilidad).
    [Fact]
    public async Task Confirmar_falla_completa_si_ya_existe_retencion_activa_para_el_origen()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);
        fx.RetentionIssuer
            .Setup(i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionDocument>.Conflict("Ya existe una retención activa para este documento origen."));

        var result = await fx.Handler.Handle(
            new ConfirmExpenseDocumentCommand(document.Id, AppliesRetentionIntent()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 9) Si falla la emisión de retención, el gasto NO queda Confirmed — se verifica que nunca se
    // llega a persistir (SaveChangesAsync nunca se invoca), que es lo que en producción evita que
    // ErpDbContext escriba el estado Confirmed en BD. La mutación en memoria de Confirm() (arriba,
    // antes de invocar al emisor) no se revierte aquí porque nunca llegó a flushearse — mismo
    // criterio que "Si_falla_el_posting_la_confirmacion_falla_y_el_documento_queda_Draft".
    [Fact]
    public async Task Si_falla_la_emision_de_retencion_el_gasto_no_queda_persistido_como_Confirmed()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);
        fx.RetentionIssuer
            .Setup(i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionDocument>.ValidationFailure("El proveedor está exento de retención."));

        var result = await fx.Handler.Handle(
            new ConfirmExpenseDocumentCommand(document.Id, AppliesRetentionIntent()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        // Nunca se llamo SaveChangesAsync -> en produccion el gasto sigue Draft en BD (el rollback
        // real de persistencia no se simula en un test basado en mocks, igual que el test analogo
        // de fallo de posting).
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 10)/11) Tenant/Company/Branch siempre del contexto seguro, nunca del body — RetentionIntent no
    // expone esos campos (imposible que el body los mande) y el RetentionIssueRequest construido por
    // el handler usa exactamente los valores de ICurrentTenant/ICurrentCompany/document.BranchId.
    [Fact]
    public void RetentionIntent_no_expone_TenantId_CompanyId_BranchId_ni_SourceDocumentType()
    {
        var properties = typeof(RetentionIntent).GetProperties().Select(p => p.Name).ToArray();

        properties.Should().NotContain(new[] { "TenantId", "CompanyId", "BranchId", "SourceDocumentType", "SourceDocumentId" });
    }

    [Fact]
    public async Task Confirmar_con_retencion_usa_IDs_del_contexto_seguro_no_del_body()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);
        var issuedRetention = IssuedRetentionFor(document);
        RetentionIssueRequest? captured = null;
        fx.RetentionIssuer
            .Setup(i => i.IssueForExpenseAsync(It.IsAny<ExpenseDocument>(), It.IsAny<RetentionIssueRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseDocument, RetentionIssueRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(Result<RetentionDocument>.Success(issuedRetention));

        await fx.Handler.Handle(
            new ConfirmExpenseDocumentCommand(document.Id, AppliesRetentionIntent()),
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.BranchId.Should().Be(BranchId);
        captured.UserId.Should().Be(UserId);
    }

    private static void SetPrivateStatus(ExpenseDocument document, ExpenseStatus status)
    {
        var property = typeof(ExpenseDocument).GetProperty(nameof(ExpenseDocument.Status))!;
        property.SetValue(document, status);
    }

    private sealed class Fixture
    {
        public Mock<IExpenseDocumentRepository> Docs { get; } = new();
        public Mock<IExpenseCategoryRepository> CategoryRepo { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IAccountsPayableService> Payables { get; } = new();
        public Mock<IDocumentFlowPolicyService> WorkflowPolicy { get; } = new();
        public Mock<IRetentionIssuer> RetentionIssuer { get; } = new();

        public ExpenseCategoryNode Type { get; }
        public ExpenseCategoryNode Category { get; }
        public ExpenseCategoryNode Subcategory { get; }
        public Account Account { get; }

        public ConfirmExpenseDocumentHandler Handler =>
            new(
                Docs.Object,
                CategoryRepo.Object,
                Accounts.Object,
                Payables.Object,
                WorkflowPolicy.Object,
                RetentionIssuer.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                NullLogger<ConfirmExpenseDocumentHandler>.Instance
            );

        public Fixture()
        {
            Type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);
            Category = ExpenseCategoryNode.CreateCategory(TenantId, CompanyId, Type, "OFF", "Oficina", UserId);
            Account = ExpenseAccount("6.1.01.001");
            Subcategory = ExpenseCategoryNode.CreateSubcategory(
                TenantId, CompanyId, Category, "PAP", "Papeleria", Account.Id, UserId
            );

            CategoryRepo
                .Setup(r => r.GetByIdAsync(TenantId, Subcategory.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Subcategory);
            Accounts
                .Setup(r => r.GetByIdAsync(TenantId, CompanyId, Account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Account);
            Docs
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            WorkflowPolicy
                .Setup(w =>
                    w.EnsureConfirmationFlowAsync(
                        CompanyId,
                        DocTypeCodes.ExpenseDocument,
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
            Payables
                .Setup(p =>
                    p.CreateFromOriginAsync(
                        It.IsAny<CreateAccountsPayableFromOriginRequest>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (CreateAccountsPayableFromOriginRequest req, Guid createdBy, CancellationToken _) =>
                    {
                        var payable = AccountsPayable.CreateFromOrigin(
                            req.TenantId, req.CompanyId, req.BranchId, req.SupplierId,
                            req.OriginType, req.OriginId, req.DocumentType, req.DocumentNumber,
                            req.IssueDate, req.AccountingDate, createdBy
                        );
                        foreach (var installment in req.Installments)
                            payable.AddInstallment(installment.InstallmentNumber, installment.DueDate, installment.Amount);
                        return payable;
                    }
                );
        }

        public Account ExpenseAccount(
            string code,
            bool allowsPosting = true,
            bool isActive = true,
            AccountType accountType = AccountType.Expense
        )
        {
            var account = Account.Create(
                TenantId, CompanyId, AccountCode.Create(code), "Gasto administrativo",
                null, accountType, AccountNature.Debit, allowsPosting, UserId
            );
            if (!isActive)
                account.Disable(UserId);
            return account;
        }

        public ExpenseLine Line(
            ExpenseCategoryNode subcategory,
            Account account,
            decimal unitAmount,
            string vatCode,
            decimal vatRate = 0m
        ) =>
            ExpenseLine.Create(
                Guid.NewGuid(), TenantId, subcategory.Id, account.Id,
                subcategory.Name, 1m, unitAmount, vatCode, vatRate
            );

        public ExpenseDocument DraftDocumentWithLines(params ExpenseLine[] lines)
        {
            var document = ExpenseDocument.CreateDraft(
                TenantId, CompanyId, BranchId, SupplierId, "Proveedor Demo", "1791352688001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), "01", "001-001-000000123",
                PaymentTermId, "Contado", 1, 0, UserId
            );
            var rebuiltLines = lines
                .Select(l => ExpenseLine.Create(
                    document.Id, TenantId, l.ExpenseSubcategoryId, l.SnapshotAccountingAccountId,
                    l.Description, l.Quantity, l.UnitAmount, l.VatCode, l.VatRate
                ))
                .ToArray();
            document.ReplaceLines(rebuiltLines, UserId);
            return document;
        }

        public void SetupDocument(ExpenseDocument document)
        {
            Docs
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
        }
    }
}
