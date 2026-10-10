using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>IL-6A — CxP Inicial: Validate fiel, saldo neto pendiente al corte, sin compras ni proveedores nuevos.</summary>
public sealed class InitialPayableImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 10, 9);
    private static readonly DateOnly Cutoff = new(2026, 9, 30);
    private const string Ruc = "0990000000001";

    private readonly Mock<IInitialPayableImportSheetReader> _reader = new();
    private readonly Mock<IBusinessPartnerImportLookup> _partners = new();
    private readonly Mock<IInitialPayableLookup> _payables = new();
    private readonly Mock<IOpeningBalanceConstraintsReader> _openingBalance = new();
    private readonly Mock<ISriCatalogLookupRepository> _sriCatalog = new();
    private readonly Mock<ICompanyClock> _clock = new();
    private readonly Mock<ICurrentBranch> _branch = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IAccountsPayableRepository> _repo = new();
    private readonly List<AccountsPayable> _added = [];

    public InitialPayableImportProcessorTests()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        _ctx.SetupGet(x => x.CompanyId).Returns(CompanyId);
        _ctx.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _repo.Setup(x => x.AddAsync(It.IsAny<AccountsPayable>(), It.IsAny<CancellationToken>()))
            .Callback<AccountsPayable, CancellationToken>((p, _) => _added.Add(p))
            .Returns(Task.CompletedTask);
        _branch.SetupGet(x => x.BranchId).Returns(BranchId);
        _clock.Setup(x => x.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(CompanyToday);
        SetupSupplier(Ruc, Match());
        _payables.Setup(x => x.GetDocumentNumbersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Cutoff);
        // Activos del catálogo SRI (02 inactivo: no aparece).
        _sriCatalog.Setup(x => x.GetActiveDocTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "01", "03", "04", "05", "06", "07" }
                .Select(c => new SriDocType { Code = c, Name = c, ShortName = c }).ToList());
    }

    private InitialPayableImportProcessor Processor() =>
        new(_reader.Object, _partners.Object, _payables.Object, _openingBalance.Object, _sriCatalog.Object,
            _clock.Object, _branch.Object, _ctx.Object, _repo.Object);

    private static BusinessPartnerImportMatch Match(bool isActive = true, bool hasActiveRole = true,
        bool hasRevokedRole = false, bool isAmbiguous = false) =>
        new(SupplierId, isActive, Ruc, "Proveedor Uno S.A.", hasActiveRole, false, null, isAmbiguous, hasRevokedRole);

    private void SetupSupplier(string number, BusinessPartnerImportMatch? match) =>
        _partners.Setup(x => x.FindByIdentificationAsync("04", number, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

    private static Dictionary<string, string?> Row(string? type = "04", string? number = Ruc,
        string? docType = "01", string? document = "001-001-000001234", string? issue = "2026-08-15",
        string? due = "2026-10-15", string? balance = "150.75", string? currency = "USD",
        string? cutoff = "2026-09-30") => new()
    {
        [InitialPayableImportColumns.IdentificationType] = type,
        [InitialPayableImportColumns.IdentificationNumber] = number,
        [InitialPayableImportColumns.DocumentType] = docType,
        [InitialPayableImportColumns.DocumentNumber] = document,
        [InitialPayableImportColumns.IssueDate] = issue,
        [InitialPayableImportColumns.DueDate] = due,
        [InitialPayableImportColumns.Balance] = balance,
        [InitialPayableImportColumns.Currency] = currency,
        [InitialPayableImportColumns.CutoffDate] = cutoff,
    };

    private async Task<RowValidationResult> Validate(Dictionary<string, string?> row) =>
        await Processor().ValidateRowAsync(1, row, false, CancellationToken.None);

    private static ParsedInitialPayableRow Parsed(RowValidationResult r) =>
        JsonSerializer.Deserialize<ParsedInitialPayableRow>(r.ParsedDataJson)!;

    private static void ShouldHaveError(RowValidationResult r, string code, string field)
    {
        r.HasBlockingIssue.Should().BeTrue();
        r.Issues.Should().Contain(i => i.Severity == ImportSeverity.Error && i.Code == code && i.FieldName == field);
    }

    [Fact]
    public async Task Fila_valida_resuelve_proveedor_tipo_sucursal_y_saldo_sin_issues()
    {
        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        var parsed = Parsed(result);
        parsed.SupplierId.Should().Be(SupplierId);
        parsed.SupplierName.Should().Be("Proveedor Uno S.A.");
        parsed.DocumentType.Should().Be("01");
        parsed.DocumentNumber.Should().Be("001-001-000001234");
        parsed.DocumentKey.Should().Be("001001000001234");
        parsed.IssueDate.Should().Be(new DateOnly(2026, 8, 15));
        parsed.DueDate.Should().Be(new DateOnly(2026, 10, 15));
        parsed.Balance.Should().Be(150.75m);
        parsed.CurrencyCode.Should().Be("USD");
        parsed.CutoffDate.Should().Be(Cutoff);
        parsed.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public void ImportType_y_plantilla_son_propios_de_CxP_inicial()
    {
        var processor = Processor();
        processor.ImportType.Should().Be(ImportType.InitialPayables);
        processor.TemplateFileName.Should().Be("plantilla-cxp-inicial.xlsx");
    }

    [Theory]
    [InlineData(InitialPayableImportColumns.IdentificationType)]
    [InlineData(InitialPayableImportColumns.IdentificationNumber)]
    [InlineData(InitialPayableImportColumns.DocumentType)]
    [InlineData(InitialPayableImportColumns.DocumentNumber)]
    [InlineData(InitialPayableImportColumns.IssueDate)]
    [InlineData(InitialPayableImportColumns.DueDate)]
    [InlineData(InitialPayableImportColumns.Balance)]
    [InlineData(InitialPayableImportColumns.Currency)]
    [InlineData(InitialPayableImportColumns.CutoffDate)]
    public async Task Campos_obligatorios_vacios_son_error(string column)
    {
        var row = Row();
        row[column] = "  ";

        ShouldHaveError(await Validate(row), "MISSING_REQUIRED_FIELD", column);
    }

    // ── Proveedor ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Proveedor_inexistente_es_error_y_nunca_se_crea()
    {
        SetupSupplier("0990000000002", null);

        var result = await Validate(Row(number: "0990000000002"));

        ShouldHaveError(result, "SUPPLIER_NOT_FOUND", InitialPayableImportColumns.IdentificationNumber);
        Parsed(result).SupplierId.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true, false, false, "SUPPLIER_INACTIVE")]
    [InlineData(true, false, true, false, "SUPPLIER_ROLE_REVOKED")]
    [InlineData(true, false, false, false, "SUPPLIER_ROLE_MISSING")]
    [InlineData(true, true, false, true, "AMBIGUOUS_IDENTIFICATION")]
    public async Task Proveedor_no_habilitado_es_error(bool active, bool role, bool revoked, bool ambiguous, string code)
    {
        SetupSupplier(Ruc, Match(active, role, revoked, ambiguous));

        ShouldHaveError(await Validate(Row()), code, InitialPayableImportColumns.IdentificationNumber);
    }

    [Fact]
    public async Task Proveedor_se_busca_con_rol_Supplier_y_sin_exigir_configuracion_de_retencion()
    {
        // HasCompanySettings = false (sin defaults de retención/condición de pago): no bloquea (decisión 6).
        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        _partners.Verify(x => x.FindByIdentificationAsync("04", Ruc, RoleType.Supplier, It.IsAny<CancellationToken>()),
            Times.Once);
        _partners.Verify(x => x.FindByIdentificationAsync(It.IsAny<string>(), It.IsAny<string>(), RoleType.Customer,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consumidor_final_no_puede_ser_proveedor()
    {
        ShouldHaveError(await Validate(Row(type: "07", number: "9999999999999")), "CONSUMIDOR_FINAL_NOT_ALLOWED",
            InitialPayableImportColumns.IdentificationType);
    }

    [Fact]
    public async Task Tipo_identificacion_sin_cero_inicial_es_error()
    {
        ShouldHaveError(await Validate(Row(type: "4")), "LEADING_ZERO_LOST", InitialPayableImportColumns.IdentificationType);
    }

    // ── Tipo de documento ───────────────────────────────────────────────────

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("05")]
    public async Task Tipo_documento_SRI_real_y_activo_es_valido(string code)
    {
        var result = await Validate(Row(docType: code));

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).DocumentType.Should().Be(code);
    }

    [Theory]
    [InlineData("99", "DOCUMENT_TYPE_NOT_FOUND")]
    [InlineData("SI", "DOCUMENT_TYPE_NOT_FOUND")]
    [InlineData("001234", "DOCUMENT_TYPE_NOT_FOUND")]
    [InlineData("02", "DOCUMENT_TYPE_NOT_FOUND")]
    [InlineData("04", "DOCUMENT_TYPE_NOT_PAYABLE")]
    [InlineData("06", "DOCUMENT_TYPE_NOT_PAYABLE")]
    [InlineData("07", "DOCUMENT_TYPE_NOT_PAYABLE")]
    [InlineData("1", "LEADING_ZERO_LOST")]
    public async Task Tipo_documento_invalido_es_error(string code, string expected)
    {
        ShouldHaveError(await Validate(Row(docType: code)), expected, InitialPayableImportColumns.DocumentType);
    }

    [Fact]
    public async Task Catalogo_SRI_se_consulta_una_sola_vez_por_lote()
    {
        var processor = Processor();
        await processor.ValidateRowAsync(1, Row(), false, CancellationToken.None);
        await processor.ValidateRowAsync(2, Row(document: "002"), false, CancellationToken.None);

        _sriCatalog.Verify(x => x.GetActiveDocTypesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Documento ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Documento_existente_en_cualquier_CxP_del_proveedor_es_error_normalizado()
    {
        _payables.Setup(x => x.GetDocumentNumbersAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["001 001 000001234"]);

        ShouldHaveError(await Validate(Row(document: "001-001-000001234")), "DOCUMENT_ALREADY_EXISTS",
            InitialPayableImportColumns.DocumentNumber);
    }

    [Fact]
    public async Task Documento_demasiado_largo_o_sin_alfanumericos_es_error()
    {
        ShouldHaveError(await Validate(Row(document: new string('1', 31))), "DOCUMENT_NUMBER_TOO_LONG",
            InitialPayableImportColumns.DocumentNumber);
        ShouldHaveError(await Validate(Row(document: "--- ")), "INVALID_DOCUMENT_NUMBER",
            InitialPayableImportColumns.DocumentNumber);
    }

    [Fact]
    public async Task Mismo_documento_del_mismo_proveedor_en_el_archivo_es_error_en_ambas_filas()
    {
        var processor = Processor();
        var rows = new List<RowValidationResult>
        {
            await processor.ValidateRowAsync(1, Row(document: "001-001-1"), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row(document: "0010011"), false, CancellationToken.None),
            await processor.ValidateRowAsync(3, Row(document: "001-001-2"), false, CancellationToken.None),
        };

        var result = processor.ValidateBatch(rows);

        ShouldHaveError(result[0], "DUPLICATE_DOCUMENT_IN_FILE", InitialPayableImportColumns.DocumentNumber);
        ShouldHaveError(result[1], "DUPLICATE_DOCUMENT_IN_FILE", InitialPayableImportColumns.DocumentNumber);
        result[2].HasBlockingIssue.Should().BeFalse();
    }

    // ── Fechas ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Emision_posterior_al_corte_es_error()
    {
        ShouldHaveError(await Validate(Row(issue: "2026-10-01", due: "2026-10-30")), "ISSUE_DATE_AFTER_CUTOFF",
            InitialPayableImportColumns.IssueDate);
    }

    [Fact]
    public async Task Vencimiento_anterior_a_emision_es_error_y_vencida_al_corte_es_valida()
    {
        ShouldHaveError(await Validate(Row(due: "2026-08-14")), "DUE_DATE_BEFORE_ISSUE_DATE",
            InitialPayableImportColumns.DueDate);
        (await Validate(Row(due: "2026-09-01"))).HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Fecha_invalida_es_error()
    {
        ShouldHaveError(await Validate(Row(issue: "15-08-2026")), "INVALID_DATE", InitialPayableImportColumns.IssueDate);
    }

    [Fact]
    public async Task Corte_distinto_de_la_fecha_de_apertura_es_error()
    {
        ShouldHaveError(await Validate(Row(cutoff: "2026-09-29", issue: "2026-08-15")), "CUTOFF_DATE_MISMATCH",
            InitialPayableImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Sin_fecha_de_apertura_definida_es_error()
    {
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateOnly?)null);

        ShouldHaveError(await Validate(Row()), "OPENING_BALANCE_DATE_NOT_SET", InitialPayableImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Corte_futuro_es_error()
    {
        ShouldHaveError(await Validate(Row(cutoff: "2026-10-10")), "FUTURE_CUTOFF_DATE",
            InitialPayableImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Varias_fechas_de_corte_en_el_lote_es_error()
    {
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Cutoff);
        var processor = Processor();
        var rows = new List<RowValidationResult>
        {
            await processor.ValidateRowAsync(1, Row(document: "1"), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row(document: "2", cutoff: "2026-09-29", issue: "2026-08-01"),
                false, CancellationToken.None),
        };

        var result = processor.ValidateBatch(rows);

        ShouldHaveError(result[0], "MULTIPLE_CUTOFF_DATES", InitialPayableImportColumns.CutoffDate);
        ShouldHaveError(result[1], "MULTIPLE_CUTOFF_DATES", InitialPayableImportColumns.CutoffDate);
    }

    // ── Saldo y moneda ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("0", "NON_POSITIVE_NUMBER")]
    [InlineData("-5", "INVALID_NUMBER")]
    [InlineData("1,50", "INVALID_NUMBER")]
    [InlineData("1.234,50", "INVALID_NUMBER")]
    [InlineData("10.123", "PRECISION_EXCEEDED")]
    [InlineData("99999999999999999.00", "AMOUNT_TOO_LARGE")]
    public async Task Saldo_invalido_es_error_sin_redondeo(string balance, string code)
    {
        ShouldHaveError(await Validate(Row(balance: balance)), code, InitialPayableImportColumns.Balance);
    }

    [Theory]
    [InlineData("EUR", "CURRENCY_NOT_SUPPORTED")]
    [InlineData("", "MISSING_REQUIRED_FIELD")]
    public async Task Moneda_distinta_de_USD_o_vacia_es_error(string currency, string code)
    {
        ShouldHaveError(await Validate(Row(currency: currency)), code, InitialPayableImportColumns.Currency);
    }

    [Fact]
    public async Task Moneda_usd_en_minusculas_se_normaliza()
    {
        var result = await Validate(Row(currency: "usd"));

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).CurrencyCode.Should().Be("USD");
    }

    // ── Sucursal / confirmación ─────────────────────────────────────────────

    [Fact]
    public async Task Sin_sucursal_activa_es_error()
    {
        _branch.SetupGet(x => x.BranchId).Returns(Guid.Empty);

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "BRANCH_REQUIRED");
    }

    [Fact]
    public async Task Lote_de_otra_sucursal_no_pasa_la_guarda_de_alcance()
    {
        var json = (await Validate(Row())).ParsedDataJson;
        _branch.SetupGet(x => x.BranchId).Returns(Guid.NewGuid());

        (await Processor().CheckStagingScopeAsync([json], CancellationToken.None)).Should().NotBeNull();
    }

    // ── IL-6B: confirmación atómica ─────────────────────────────────────────

    private static readonly Guid BatchId = Guid.NewGuid();

    private async Task<(Guid, int, string)[]> StagedAsync(params Dictionary<string, string?>[] rows)
    {
        var processor = Processor();
        var results = new List<RowValidationResult>();
        for (var i = 0; i < rows.Length; i++)
            results.Add(await processor.ValidateRowAsync(i + 1, rows[i], false, CancellationToken.None));
        var validated = processor.ValidateBatch(results);
        validated.Should().OnlyContain(r => !r.HasBlockingIssue);
        return validated.Select((r, i) => (Guid.NewGuid(), i + 1, r.ParsedDataJson)).ToArray();
    }

    private Task<BatchConfirmResult> ConfirmAsync((Guid, int, string)[] staged) =>
        Processor().ConfirmBatchAsync(BatchId, staged, CancellationToken.None);

    [Fact]
    public async Task Confirma_una_CxP_InitialBalance_por_fila_con_una_cuota_y_la_fila_como_origen()
    {
        var staged = await StagedAsync(Row(document: "A-1", balance: "150.75", docType: "03"), Row(document: "A-2", balance: "20"));

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeTrue(result.Error);
        _added.Should().HaveCount(2);
        var first = _added[0];
        first.OriginType.Should().Be(AccountsPayableOriginType.InitialBalance);
        first.OriginId.Should().Be(staged[0].Item1, "OriginId = ImportBatchRow.Id");
        first.SupplierId.Should().Be(SupplierId);
        first.DocumentType.Should().Be("03");
        first.DocumentNumber.Should().Be("A-1");
        first.DocumentNumberNormalized.Should().Be("A1");
        first.IssueDate.Should().Be(new DateOnly(2026, 8, 15));
        first.AccountingDate.Should().Be(Cutoff, "AccountingDate = Company.OpeningBalanceDate");
        first.BranchId.Should().Be(BranchId);
        first.ImportBatchId.Should().Be(BatchId);
        first.TenantId.Should().Be(TenantId);
        first.CompanyId.Should().Be(CompanyId);
        first.Installments.Should().ContainSingle(i => i.DueDate == new DateOnly(2026, 10, 15) && i.Amount == 150.75m);
        first.OutstandingAmount.Should().Be(150.75m);
        result.CreatedIdsByRow.Should().BeEquivalentTo(new Dictionary<int, Guid> { [1] = _added[0].Id, [2] = _added[1].Id });
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Duplicado_aparecido_despues_del_preview_rechaza_todo_sin_escribir()
    {
        var staged = await StagedAsync(Row(document: "A-1"), Row(document: "A-2"));
        _payables.Setup(x => x.GetDocumentNumbersAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["a 2"]);

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya tiene una cuenta por pagar");
        _added.Should().BeEmpty();
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("no-role")]
    [InlineData("gone")]
    public async Task Proveedor_que_cambio_despues_del_preview_rechaza_todo(string change)
    {
        var staged = await StagedAsync(Row());
        SetupSupplier(Ruc, change switch
        {
            "inactive" => Match(isActive: false),
            "no-role" => Match(hasActiveRole: false),
            _ => null,
        });

        (await ConfirmAsync(staged)).IsSuccess.Should().BeFalse();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Tipo_de_documento_inactivado_despues_del_preview_rechaza_todo()
    {
        var staged = await StagedAsync(Row(docType: "03"));
        _sriCatalog.Setup(x => x.GetActiveDocTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "01" }.Select(c => new SriDocType { Code = c, Name = c, ShortName = c }).ToList());

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya no está activo");
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Fecha_de_apertura_cambiada_despues_del_preview_rechaza_todo()
    {
        var staged = await StagedAsync(Row());
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateOnly(2026, 8, 31));

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya no coincide");
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Otra_sucursal_activa_rechaza_todo()
    {
        var staged = await StagedAsync(Row());
        _branch.SetupGet(x => x.BranchId).Returns(Guid.NewGuid());

        (await ConfirmAsync(staged)).IsSuccess.Should().BeFalse();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_filas_de_origen_no_confirma()
    {
        var staged = (await StagedAsync(Row())).Select(r => (r.Item2, r.Item3)).ToArray();

        (await Processor().ConfirmBatchAsync(staged, CancellationToken.None)).IsSuccess.Should().BeFalse();
        (await Processor().ConfirmBatchAsync(BatchId, staged, CancellationToken.None)).IsSuccess.Should().BeFalse();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmacion_fila_por_fila_nunca_registra_saldos()
    {
        var result = await Processor().ConfirmRowAsync((await Validate(Row())).ParsedDataJson, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
