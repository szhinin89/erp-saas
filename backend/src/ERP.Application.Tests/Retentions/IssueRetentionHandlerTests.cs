using ERP.Application.Common;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-APPLICATION-01C — cubre <see cref="IssueRetentionHandler"/>/<see cref="IssueRetentionValidator"/>.
/// Esta fase emite un <see cref="RetentionDocument"/> de forma AISLADA: no toca
/// <c>AccountsPayable</c>/<c>JournalEntry</c>/<c>ExpenseDocument</c> (más allá de leerlo). Los
/// tests de "no toca X" verifican que ningún método de escritura de esos módulos se invoca, porque
/// el handler ni siquiera depende de esas interfaces (imposible tocarlas por construcción) — se
/// verifica adicionalmente que <see cref="IExpenseDocumentRepository.SaveChangesAsync"/> nunca se
/// llama.
/// </summary>
public sealed class IssueRetentionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid ExpenseSubcategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();

    private static readonly RetentionEligibilityResult FullyEligible = new(
        CanRetainVat: true,
        CanRetainIncome: true,
        IsSupplierExempt: false,
        HasRetainableBase: true,
        MissingRetentionCode: false,
        IsSupplierRequiredToKeepAccounting: false,
        SuggestedVatRetentionCode: "725",
        SuggestedIncomeRetentionCode: "303",
        Reasons: Array.Empty<string>()
    );

    private static IssueRetentionLineInput VatLine(decimal baseAmount = 100m, decimal rate = 30m, decimal retained = 30m) =>
        new(RetentionTaxType.Vat, "725", baseAmount, rate, retained);

    // ── 1) Emite para ExpenseDocument elegible ──────────────────────────

    [Fact]
    public async Task Emite_retencion_para_ExpenseDocument_elegible()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RetentionStatus.Issued);
        result.Value.RetentionNumber.Should().Be("001-001-000000001");
    }

    // ── 2/3) Validator ───────────────────────────────────────────────────

    [Fact]
    public void Rechaza_SourceDocumentId_vacio()
    {
        var cmd = new IssueRetentionCommand(
            RetentionSourceDocumentType.ExpenseDocument,
            Guid.Empty,
            EmissionPointId,
            "001-001-000000001",
            new DateOnly(2026, 9, 3),
            new[] { VatLine() }
        );

        var result = new IssueRetentionValidator().Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(IssueRetentionCommand.SourceDocumentId));
    }

    [Fact]
    public void Rechaza_SourceDocumentType_invalido()
    {
        var cmd = new IssueRetentionCommand(
            (RetentionSourceDocumentType)999,
            Guid.NewGuid(),
            EmissionPointId,
            "001-001-000000001",
            new DateOnly(2026, 9, 3),
            new[] { VatLine() }
        );

        var result = new IssueRetentionValidator().Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(IssueRetentionCommand.SourceDocumentType));
    }

    // ── 4/5) PurchaseInvoice/Manual: no soportado ───────────────────────

    [Fact]
    public async Task Rechaza_PurchaseInvoice_con_no_soportado_distinguible_de_no_elegible()
    {
        var fx = new Fixture();

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.PurchaseInvoice,
                Guid.NewGuid(),
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NotSupportedInThisPhase");
        fx.EligibilityService.Verify(
            s => s.EvaluateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task Rechaza_Manual_con_no_soportado()
    {
        var fx = new Fixture();

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.Manual,
                Guid.NewGuid(),
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NotSupportedInThisPhase");
    }

    // ── 6) Company no retiene IVA ────────────────────────────────────────

    [Fact]
    public async Task Rechaza_si_empresa_no_retiene_IVA()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(
            document.SupplierId,
            FullyEligible with { CanRetainVat = false, Reasons = new[] { "La empresa no está configurada como agente de retención de IVA." } }
        );

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("agente de retención");
    }

    // ── 7) Proveedor exento ───────────────────────────────────────────────

    [Fact]
    public async Task Rechaza_si_proveedor_exento()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(
            document.SupplierId,
            FullyEligible with
            {
                CanRetainVat = false,
                IsSupplierExempt = true,
                Reasons = new[] { "El proveedor está exento de retención." },
            }
        );

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("exento");
    }

    // ── 8) Sin base retenible ─────────────────────────────────────────────

    [Fact]
    public async Task Rechaza_si_no_hay_base_retenible()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(
            document.SupplierId,
            FullyEligible with
            {
                CanRetainVat = false,
                HasRetainableBase = false,
                Reasons = new[] { "El documento origen no tiene base retenible de IVA." },
            }
        );

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("base retenible");
    }

    // ── 9) Falta código activo ────────────────────────────────────────────

    [Fact]
    public async Task Rechaza_si_falta_codigo_de_retencion_activo()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(
            document.SupplierId,
            FullyEligible with
            {
                CanRetainVat = false,
                MissingRetentionCode = true,
                Reasons = new[] { "El proveedor no tiene código de retención de IVA configurado." },
            }
        );

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("código de retención");
    }

    // ── 10) Ya existe retención activa para el origen ─────────────────────

    [Fact]
    public async Task Rechaza_si_ya_existe_retencion_activa_para_el_origen()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.RetentionRepo
            .Setup(r => r.ExistsActiveBySourceAsync(
                TenantId, CompanyId, RetentionSourceDocumentType.ExpenseDocument, document.Id,
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(true);

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        fx.RetentionRepo.Verify(r => r.AddAsync(It.IsAny<RetentionDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── 11) El command no acepta Tenant/Company/Branch ────────────────────

    [Fact]
    public void Command_no_expone_propiedades_de_Tenant_Company_Branch()
    {
        var properties = typeof(IssueRetentionCommand).GetProperties().Select(p => p.Name).ToArray();

        properties.Should().NotContain(new[] { "TenantId", "CompanyId", "BranchId" });
    }

    // ── 12) Crea documento con IDs del contexto seguro ────────────────────

    [Fact]
    public async Task Crea_documento_con_IDs_del_contexto_seguro()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        RetentionDocument? captured = null;
        fx.RetentionRepo
            .Setup(r => r.AddAsync(It.IsAny<RetentionDocument>(), It.IsAny<CancellationToken>()))
            .Callback<RetentionDocument, CancellationToken>((d, _) => captured = d)
            .Returns(Task.CompletedTask);

        await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.BranchId.Should().Be(BranchId);
        captured.SubjectBusinessPartnerId.Should().Be(document.SupplierId);
    }

    // ── 13) Persiste líneas y totales ─────────────────────────────────────

    [Fact]
    public async Task Persiste_lineas_y_totales()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine(baseAmount: 100m, rate: 30m, retained: 30m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(1);
        result.Value.TotalRetainedVat.Should().Be(30m);
        result.Value.TotalRetained.Should().Be(30m);
        fx.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 14) Levanta evento Issued ──────────────────────────────────────────

    [Fact]
    public async Task Levanta_evento_Issued_en_el_documento_persistido()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        RetentionDocument? captured = null;
        fx.RetentionRepo
            .Setup(r => r.AddAsync(It.IsAny<RetentionDocument>(), It.IsAny<CancellationToken>()))
            .Callback<RetentionDocument, CancellationToken>((d, _) => captured = d)
            .Returns(Task.CompletedTask);

        await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is RetentionDocumentIssuedEvent);
    }

    // ── 15) No toca ExpenseDocument/CxP/JournalEntry ──────────────────────

    [Fact]
    public async Task No_toca_ExpenseDocument_ni_persiste_cambios_en_su_repositorio()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        // El handler no depende de IAccountsPayableRepository/IJournalEntry* — imposible tocar CxP
        // o contabilidad por construcción. Solo se verifica que el repo de ExpenseDocument nunca
        // se usa para escritura (únicamente se lee vía GetByIdAsync).
        fx.ExpenseRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fx.ExpenseRepo.Verify(r => r.AddAsync(It.IsAny<ExpenseDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── RETENTIONS-TAX-COMPONENT-MODEL-02B: snapshot del documento sustento ──

    [Fact]
    public async Task Snapshot_del_documento_sustento_se_resuelve_desde_el_ExpenseDocument_real()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        RetentionDocument? captured = null;
        fx.RetentionRepo
            .Setup(r => r.AddAsync(It.IsAny<RetentionDocument>(), It.IsAny<CancellationToken>()))
            .Callback<RetentionDocument, CancellationToken>((d, _) => captured = d)
            .Returns(Task.CompletedTask);

        await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        // Los valores vienen del ExpenseDocument YA CARGADO — nunca resueltos por el propio
        // agregado RetentionDocument (que no conoce ExpenseDocument, ver comentario de tipo de
        // RetentionDocument.SourceDocumentSnapshot).
        captured!.SourceDocumentSriTypeCode.Should().Be(document.DocumentType);
        captured.SourceDocumentNumber.Should().Be(document.DocumentNumber);
        captured.SourceDocumentIssueDate.Should().Be(document.IssueDate);
        captured.SourceDocumentAuthorizationNumber.Should().Be(document.AuthorizationNumber);
        captured.SourceDocumentSubtotal.Should().Be(document.Subtotal);
        captured.SourceDocumentTotal.Should().Be(document.GrandTotal);
        // codSustento: gap conocido, documentado — ExpenseDocument no lo captura hoy.
        captured.SourceDocumentTaxSupportCode.Should().BeNull();
        // Periodo fiscal derivado de la IssueDate DE LA RETENCIÓN (parámetro del command), no de
        // la del documento sustento — son fechas distintas por diseño.
        captured.FiscalPeriod.Should().Be("09/2026");
    }

    [Fact]
    public async Task Snapshot_del_documento_sustento_permanece_congelado_aunque_el_ExpenseDocument_original_ya_no_este_disponible()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(document.SupplierId, FullyEligible);
        fx.SetupNotExisting();

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue();

        // Valores del snapshot capturados en el momento de emitir.
        var originalNumber = result.Value!.SourceDocumentNumber;
        var originalTotal = result.Value.SourceDocumentTotal;

        // ExpenseDocument YA NO TIENE ninguna vía pública para mutar DocumentNumber/GrandTotal una
        // vez Confirmed (EnsureDraft bloquea UpdateDraft/ReplaceLines) — la única forma de que
        // "cambiara" sería que el repositorio devolviera una instancia distinta en una consulta
        // futura (p. ej. tras una corrección manual en BD fuera del dominio). Simulamos justo eso:
        // el mismo Id, pero una instancia de ExpenseDocument con datos distintos — la retención ya
        // emitida (y su snapshot ya capturado) es completamente ajena a esa instancia nueva, porque
        // RetentionDocument nunca guardó una referencia al agregado origen, solo copias de sus
        // valores primitivos.
        var laterDocument = fx.ConfirmedDocument(BranchId, documentNumber: "001-001-999999999");
        laterDocument.Should().NotBeSameAs(document);

        result.Value.SourceDocumentNumber.Should().Be(originalNumber);
        result.Value.SourceDocumentTotal.Should().Be(originalTotal);
        result.Value.SourceDocumentNumber.Should().NotBe(laterDocument.DocumentNumber);
    }

    // ── Casos ya cubiertos indirectamente: NotFound / branch distinto / estado no confirmado ──

    [Fact]
    public async Task ExpenseDocument_de_otra_sucursal_falla_cerrado_con_NotFound()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(OtherBranchId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task ExpenseDocument_en_Draft_se_bloquea_con_error_de_validacion()
    {
        var fx = new Fixture();
        var document = fx.DraftDocument(BranchId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new IssueRetentionCommand(
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                EmissionPointId,
                "001-001-000000001",
                new DateOnly(2026, 9, 3),
                new[] { VatLine() }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    private sealed class Fixture
    {
        public Mock<IExpenseDocumentRepository> ExpenseRepo { get; } = new();
        public Mock<IRetentionDocumentRepository> RetentionRepo { get; } = new();
        public Mock<IRetentionEligibilityService> EligibilityService { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();

        public IssueRetentionHandler Handler =>
            new(
                ExpenseRepo.Object,
                // RETENTIONS-EXPENSES-INTEGRATION-01D-1: IssueRetentionHandler ya no habla
                // directamente con RetentionRepo/EligibilityService — delega en RetentionIssuer
                // (mismo servicio que usa ConfirmExpenseDocumentHandler). Se construye con los
                // mismos mocks que antes, así las aserciones existentes sobre RetentionRepo/
                // EligibilityService siguen siendo válidas sin cambiar ningún [Fact].
                new RetentionIssuer(RetentionRepo.Object, EligibilityService.Object),
                Uow.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId)
            );

        public void SetupDocument(ExpenseDocument document) =>
            ExpenseRepo
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

        public void SetupEligibility(Guid supplierId, RetentionEligibilityResult result) =>
            EligibilityService
                .Setup(s => s.EvaluateAsync(
                    TenantId, CompanyId, supplierId,
                    It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(result);

        public void SetupNotExisting() =>
            RetentionRepo
                .Setup(r => r.ExistsActiveBySourceAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RetentionSourceDocumentType>(), It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(false);

        public ExpenseDocument DraftDocument(Guid branchId, string documentNumber = "001-001-000000123") =>
            ExpenseDocument.CreateDraft(
                TenantId, CompanyId, branchId, SupplierId, "Proveedor Demo", "1791352688001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), "01", documentNumber,
                Guid.NewGuid(), "Contado", 1, 0, UserId
            );

        public ExpenseDocument ConfirmedDocument(Guid branchId, string documentNumber = "001-001-000000123")
        {
            var document = DraftDocument(branchId, documentNumber);
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
    }
}
