using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>IL-5A — CxC Inicial: Validate fiel, saldo pendiente al corte, sin ventas ni clientes nuevos.</summary>
public sealed class InitialReceivableImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 10, 9);
    private static readonly DateOnly Cutoff = new(2026, 9, 30);
    private const string Ruc = "0990000000001";

    private readonly Mock<IInitialReceivableImportSheetReader> _reader = new();
    private readonly Mock<IBusinessPartnerImportLookup> _partners = new();
    private readonly Mock<IInitialReceivableLookup> _receivables = new();
    private readonly Mock<IOpeningBalanceConstraintsReader> _openingBalance = new();
    private readonly Mock<ICompanyClock> _clock = new();
    private readonly Mock<ICurrentBranch> _branch = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<ISalesReceivableRepository> _repo = new();
    private readonly List<SalesReceivable> _added = [];

    public InitialReceivableImportProcessorTests()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        _ctx.SetupGet(x => x.CompanyId).Returns(CompanyId);
        _branch.SetupGet(x => x.BranchId).Returns(BranchId);
        _clock.Setup(x => x.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(CompanyToday);
        SetupCustomer(Ruc, Match());
        _receivables.Setup(x => x.GetDocumentNumbersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Cutoff);
        _ctx.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _repo.Setup(x => x.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()))
            .Callback<SalesReceivable, CancellationToken>((r, _) => _added.Add(r))
            .Returns(Task.CompletedTask);
    }

    private InitialReceivableImportProcessor Processor() =>
        new(_reader.Object, _partners.Object, _receivables.Object, _openingBalance.Object, _clock.Object,
            _branch.Object, _ctx.Object, _repo.Object);

    private static BusinessPartnerImportMatch Match(bool isActive = true, bool hasActiveRole = true,
        bool hasRevokedRole = false, bool isAmbiguous = false) =>
        new(CustomerId, isActive, Ruc, "Cliente Uno S.A.", hasActiveRole, true, null, isAmbiguous, hasRevokedRole);

    private void SetupCustomer(string number, BusinessPartnerImportMatch? match) =>
        _partners.Setup(x => x.FindByIdentificationAsync("04", number, RoleType.Customer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

    private static Dictionary<string, string?> Row(string? type = "04", string? number = Ruc,
        string? document = "001-001-000001234", string? issue = "2026-08-15", string? due = "2026-10-15",
        string? balance = "150.75", string? currency = "USD", string? cutoff = "2026-09-30") => new()
    {
        [InitialReceivableImportColumns.IdentificationType] = type,
        [InitialReceivableImportColumns.IdentificationNumber] = number,
        [InitialReceivableImportColumns.DocumentNumber] = document,
        [InitialReceivableImportColumns.IssueDate] = issue,
        [InitialReceivableImportColumns.DueDate] = due,
        [InitialReceivableImportColumns.Balance] = balance,
        [InitialReceivableImportColumns.Currency] = currency,
        [InitialReceivableImportColumns.CutoffDate] = cutoff,
    };

    private async Task<RowValidationResult> Validate(Dictionary<string, string?> row) =>
        await Processor().ValidateRowAsync(1, row, false, CancellationToken.None);

    private static ParsedInitialReceivableRow Parsed(RowValidationResult r) =>
        JsonSerializer.Deserialize<ParsedInitialReceivableRow>(r.ParsedDataJson)!;

    private static void ShouldHaveError(RowValidationResult r, string code, string field)
    {
        r.HasBlockingIssue.Should().BeTrue();
        r.Issues.Should().Contain(i => i.Severity == ImportSeverity.Error && i.Code == code && i.FieldName == field);
    }

    [Fact]
    public async Task Fila_valida_resuelve_cliente_sucursal_y_saldo_sin_issues()
    {
        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        var parsed = Parsed(result);
        parsed.CustomerId.Should().Be(CustomerId);
        parsed.CustomerName.Should().Be("Cliente Uno S.A.");
        parsed.DocumentNumber.Should().Be("001-001-000001234");
        parsed.DocumentKey.Should().Be("001001000001234");
        parsed.IssueDate.Should().Be(new DateOnly(2026, 8, 15));
        parsed.DueDate.Should().Be(new DateOnly(2026, 10, 15));
        parsed.Balance.Should().Be(150.75m);
        parsed.CurrencyCode.Should().Be("USD");
        parsed.CutoffDate.Should().Be(Cutoff);
        parsed.BranchId.Should().Be(BranchId);
    }

    [Theory]
    [InlineData(InitialReceivableImportColumns.IdentificationType)]
    [InlineData(InitialReceivableImportColumns.IdentificationNumber)]
    [InlineData(InitialReceivableImportColumns.DocumentNumber)]
    [InlineData(InitialReceivableImportColumns.IssueDate)]
    [InlineData(InitialReceivableImportColumns.DueDate)]
    [InlineData(InitialReceivableImportColumns.Balance)]
    [InlineData(InitialReceivableImportColumns.Currency)]
    [InlineData(InitialReceivableImportColumns.CutoffDate)]
    public async Task Campos_obligatorios_vacios_son_error(string column)
    {
        var row = Row();
        row[column] = "  ";

        ShouldHaveError(await Validate(row), "MISSING_REQUIRED_FIELD", column);
    }

    // ── Cliente ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cliente_inexistente_es_error_y_nunca_se_crea()
    {
        SetupCustomer("0990000000002", null);

        var result = await Validate(Row(number: "0990000000002"));

        ShouldHaveError(result, "CUSTOMER_NOT_FOUND", InitialReceivableImportColumns.IdentificationNumber);
        Parsed(result).CustomerId.Should().BeEmpty();
    }

    [Fact]
    public async Task Cliente_inactivo_es_error()
    {
        SetupCustomer(Ruc, Match(isActive: false));

        ShouldHaveError(await Validate(Row()), "CUSTOMER_INACTIVE", InitialReceivableImportColumns.IdentificationNumber);
    }

    [Fact]
    public async Task Tercero_sin_rol_Cliente_es_error()
    {
        SetupCustomer(Ruc, Match(hasActiveRole: false));

        ShouldHaveError(await Validate(Row()), "CUSTOMER_ROLE_MISSING", InitialReceivableImportColumns.IdentificationNumber);
    }

    [Fact]
    public async Task Rol_Cliente_revocado_es_error()
    {
        SetupCustomer(Ruc, Match(hasActiveRole: false, hasRevokedRole: true));

        ShouldHaveError(await Validate(Row()), "CUSTOMER_ROLE_REVOKED", InitialReceivableImportColumns.IdentificationNumber);
    }

    [Fact]
    public async Task Identificacion_ambigua_es_error()
    {
        SetupCustomer(Ruc, Match(isAmbiguous: true));

        ShouldHaveError(await Validate(Row()), "AMBIGUOUS_IDENTIFICATION", InitialReceivableImportColumns.IdentificationNumber);
    }

    [Fact]
    public async Task Consumidor_final_no_admite_cxc()
    {
        ShouldHaveError(await Validate(Row(type: "07", number: "9999999999999")), "CONSUMIDOR_FINAL_NOT_ALLOWED",
            InitialReceivableImportColumns.IdentificationType);
    }

    [Fact]
    public async Task Tipo_de_identificacion_sin_cero_inicial_es_error_explicito()
    {
        ShouldHaveError(await Validate(Row(type: "4")), "LEADING_ZERO_LOST", InitialReceivableImportColumns.IdentificationType);
    }

    [Fact]
    public async Task El_cliente_se_busca_una_sola_vez_por_identificacion_en_el_lote()
    {
        var processor = Processor();

        await processor.ValidateRowAsync(1, Row(document: "A-1"), false, CancellationToken.None);
        await processor.ValidateRowAsync(2, Row(document: "A-2"), false, CancellationToken.None);

        _partners.Verify(x => x.FindByIdentificationAsync("04", Ruc, RoleType.Customer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Documento / duplicados ──────────────────────────────────────────────

    [Theory]
    [InlineData("001-001-000001234")]
    [InlineData("001001000001234")]
    [InlineData(" 001 001 000001234 ")]
    public async Task Documento_ya_existente_del_cliente_es_error_comparando_normalizado(string fileNumber)
    {
        _receivables.Setup(x => x.GetDocumentNumbersAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["001-001-000001234"]);

        ShouldHaveError(await Validate(Row(document: fileNumber)), "DOCUMENT_ALREADY_EXISTS",
            InitialReceivableImportColumns.DocumentNumber);
    }

    [Fact]
    public async Task Documento_de_otro_numero_del_mismo_cliente_es_valido()
    {
        _receivables.Setup(x => x.GetDocumentNumbersAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["001-001-000000001"]);

        (await Validate(Row())).HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Documento_demasiado_largo_es_error()
    {
        ShouldHaveError(await Validate(Row(document: new string('9', 51))), "DOCUMENT_NUMBER_TOO_LONG",
            InitialReceivableImportColumns.DocumentNumber);
    }

    [Fact]
    public async Task Documento_sin_letras_ni_digitos_es_error()
    {
        ShouldHaveError(await Validate(Row(document: "--/--")), "INVALID_DOCUMENT_NUMBER",
            InitialReceivableImportColumns.DocumentNumber);
    }

    [Fact]
    public async Task Mismo_documento_del_mismo_cliente_repetido_en_el_archivo_es_error_en_ambas_filas()
    {
        var processor = Processor();
        var rows = new[]
        {
            await processor.ValidateRowAsync(1, Row(document: "001-001-000000010"), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row(document: "001001000000010"), false, CancellationToken.None),
            await processor.ValidateRowAsync(3, Row(document: "001-001-000000011"), false, CancellationToken.None),
        };

        var result = processor.ValidateBatch(rows);

        result[0].Issues.Should().Contain(i => i.Code == "DUPLICATE_DOCUMENT_IN_FILE");
        result[1].Issues.Should().Contain(i => i.Code == "DUPLICATE_DOCUMENT_IN_FILE");
        result[2].HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Mismo_numero_para_clientes_distintos_no_es_duplicado()
    {
        var otherCustomer = Guid.NewGuid();
        SetupCustomer("0990000000002", Match() with { BusinessPartnerId = otherCustomer, IdentificationNumber = "0990000000002" });
        var processor = Processor();
        var rows = new[]
        {
            await processor.ValidateRowAsync(1, Row(document: "FAC-1"), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row(number: "0990000000002", document: "FAC-1"), false, CancellationToken.None),
        };

        processor.ValidateBatch(rows).Should().OnlyContain(r => !r.HasBlockingIssue);
    }

    // ── Fechas ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fecha_invalida_es_error()
    {
        ShouldHaveError(await Validate(Row(issue: "15-08-2026")), "INVALID_DATE", InitialReceivableImportColumns.IssueDate);
    }

    [Fact]
    public async Task Formato_dd_MM_yyyy_se_acepta()
    {
        var result = await Validate(Row(issue: "15/08/2026"));

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).IssueDate.Should().Be(new DateOnly(2026, 8, 15));
    }

    [Fact]
    public async Task Emision_posterior_al_corte_es_error()
    {
        ShouldHaveError(await Validate(Row(issue: "2026-10-01", due: "2026-11-01")), "ISSUE_DATE_AFTER_CUTOFF",
            InitialReceivableImportColumns.IssueDate);
    }

    [Fact]
    public async Task Emision_igual_al_corte_es_valida()
    {
        (await Validate(Row(issue: "2026-09-30"))).HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Vencimiento_anterior_a_la_emision_es_error()
    {
        ShouldHaveError(await Validate(Row(issue: "2026-08-15", due: "2026-08-14")), "DUE_DATE_BEFORE_ISSUE_DATE",
            InitialReceivableImportColumns.DueDate);
    }

    [Fact]
    public async Task Vencimiento_anterior_al_corte_es_deuda_vencida_valida()
    {
        (await Validate(Row(issue: "2026-06-01", due: "2026-07-01"))).HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Fecha_de_corte_futura_es_error()
    {
        ShouldHaveError(await Validate(Row(cutoff: "2026-10-10")), "FUTURE_CUTOFF_DATE",
            InitialReceivableImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Fecha_de_corte_distinta_de_la_fecha_de_apertura_de_la_empresa_es_error()
    {
        ShouldHaveError(await Validate(Row(issue: "2026-08-01", cutoff: "2026-08-31")), "CUTOFF_DATE_MISMATCH",
            InitialReceivableImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Sin_fecha_de_apertura_definida_en_la_empresa_es_error()
    {
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateOnly?)null);

        ShouldHaveError(await Validate(Row()), "OPENING_BALANCE_DATE_NOT_SET", InitialReceivableImportColumns.CutoffDate);
    }

    [Fact]
    public async Task La_fecha_de_apertura_se_lee_una_sola_vez_por_lote()
    {
        var processor = Processor();

        await processor.ValidateRowAsync(1, Row(document: "A-1"), false, CancellationToken.None);
        await processor.ValidateRowAsync(2, Row(document: "A-2"), false, CancellationToken.None);

        _openingBalance.Verify(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Varias_fechas_de_corte_en_el_lote_son_error()
    {
        var processor = Processor();
        var rows = new[]
        {
            await processor.ValidateRowAsync(1, Row(document: "A-1", cutoff: "2026-09-30"), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row(document: "A-2", issue: "2026-08-01", cutoff: "2026-08-31"), false,
                CancellationToken.None),
        };

        processor.ValidateBatch(rows).Should().OnlyContain(r => r.Issues.Any(i => i.Code == "MULTIPLE_CUTOFF_DATES"));
    }

    // ── Saldo / moneda ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("150.75", 150.75)]
    [InlineData("0.01", 0.01)]
    [InlineData("1200", 1200)]
    public async Task Saldo_con_punto_es_invariante(string raw, double expected)
    {
        var result = await Validate(Row(balance: raw));

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).Balance.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("150,75")]
    [InlineData("1,200.50")]
    [InlineData("-5")]
    [InlineData("abc")]
    public async Task Saldo_con_coma_miles_signo_o_texto_es_error(string raw)
    {
        ShouldHaveError(await Validate(Row(balance: raw)), "INVALID_NUMBER", InitialReceivableImportColumns.Balance);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.00")]
    public async Task Saldo_cero_es_error(string raw)
    {
        ShouldHaveError(await Validate(Row(balance: raw)), "NON_POSITIVE_NUMBER", InitialReceivableImportColumns.Balance);
    }

    [Fact]
    public async Task Saldo_con_mas_de_dos_decimales_es_error_sin_redondear()
    {
        ShouldHaveError(await Validate(Row(balance: "10.555")), "PRECISION_EXCEEDED", InitialReceivableImportColumns.Balance);
    }

    [Fact]
    public async Task Saldo_que_excede_numeric_18_2_es_error()
    {
        ShouldHaveError(await Validate(Row(balance: "10000000000000000")), "AMOUNT_TOO_LARGE",
            InitialReceivableImportColumns.Balance);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("usd")]
    public async Task Moneda_USD_se_acepta(string currency)
    {
        var result = await Validate(Row(currency: currency));

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task Moneda_vacia_es_error_sin_default_silencioso()
    {
        var result = await Validate(Row(currency: null));

        ShouldHaveError(result, "MISSING_REQUIRED_FIELD", InitialReceivableImportColumns.Currency);
        Parsed(result).CurrencyCode.Should().BeEmpty();
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("COP")]
    public async Task Otra_moneda_es_error(string currency)
    {
        ShouldHaveError(await Validate(Row(currency: currency)), "CURRENCY_NOT_SUPPORTED",
            InitialReceivableImportColumns.Currency);
    }

    // ── Sucursal / alcance / confirmación ───────────────────────────────────

    [Fact]
    public async Task Sin_sucursal_activa_es_error()
    {
        _branch.SetupGet(x => x.BranchId).Returns(Guid.Empty);

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "BRANCH_REQUIRED");
    }

    [Fact]
    public async Task El_lote_queda_atado_a_la_sucursal_con_la_que_se_valido()
    {
        var staged = new[] { (await Validate(Row())).ParsedDataJson };

        (await Processor().CheckStagingScopeAsync(staged, CancellationToken.None)).Should().BeNull();

        _branch.SetupGet(x => x.BranchId).Returns(Guid.NewGuid());
        (await Processor().CheckStagingScopeAsync(staged, CancellationToken.None)).Should().Contain("otra sucursal");
    }

    [Fact]
    public async Task La_confirmacion_fila_por_fila_no_esta_disponible()
    {
        var result = await Processor().ConfirmRowAsync((await Validate(Row())).ParsedDataJson, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Tipo_y_plantilla_propios()
    {
        var processor = Processor();

        processor.ImportType.Should().Be(ImportType.InitialReceivables);
        processor.TemplateFileName.Should().Be("plantilla-cxc-inicial.xlsx");
    }

    // ── IL-5B: confirmación atómica ─────────────────────────────────────────

    private static readonly Guid BatchId = Guid.NewGuid();

    private async Task<(int, string)[]> StagedAsync(params Dictionary<string, string?>[] rows)
    {
        var processor = Processor();
        var results = new List<RowValidationResult>();
        for (var i = 0; i < rows.Length; i++)
            results.Add(await processor.ValidateRowAsync(i + 1, rows[i], false, CancellationToken.None));
        var validated = processor.ValidateBatch(results);
        validated.Should().OnlyContain(r => !r.HasBlockingIssue);
        return validated.Select((r, i) => (i + 1, r.ParsedDataJson)).ToArray();
    }

    private Task<BatchConfirmResult> ConfirmAsync((int, string)[] staged) =>
        Processor().ConfirmBatchAsync(BatchId, staged, CancellationToken.None);

    [Fact]
    public async Task Confirma_una_CxC_InitialBalance_por_fila_con_una_cuota()
    {
        var staged = await StagedAsync(Row(document: "A-1", balance: "150.75"), Row(document: "A-2", balance: "20"));

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeTrue(result.Error);
        _added.Should().HaveCount(2);
        var first = _added[0];
        first.Origin.Should().Be(SalesReceivableOrigin.InitialBalance);
        first.InvoiceId.Should().BeNull();
        first.CustomerId.Should().Be(CustomerId);
        first.DocumentNumber.Should().Be("A-1");
        first.DocumentNumberNormalized.Should().Be("A1");
        first.IssueDate.Should().Be(new DateOnly(2026, 8, 15));
        first.BranchId.Should().Be(BranchId);
        first.ImportBatchId.Should().Be(BatchId);
        first.OriginalAmount.Should().Be(150.75m);
        first.Installments.Should().ContainSingle(i => i.DueDate == new DateOnly(2026, 10, 15) && i.Amount == 150.75m);
        result.CreatedIdsByRow.Should().BeEquivalentTo(new Dictionary<int, Guid> { [1] = _added[0].Id, [2] = _added[1].Id });
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Duplicado_aparecido_despues_del_preview_rechaza_todo_sin_escribir()
    {
        var staged = await StagedAsync(Row(document: "A-1"), Row(document: "A-2"));
        _receivables.Setup(x => x.GetDocumentNumbersAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["a 2"]);

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya tiene una cuenta por cobrar");
        _added.Should().BeEmpty();
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("no-role")]
    [InlineData("gone")]
    public async Task Cliente_que_cambio_despues_del_preview_rechaza_todo(string change)
    {
        var staged = await StagedAsync(Row());
        SetupCustomer(Ruc, change switch
        {
            "inactive" => Match(isActive: false),
            "no-role" => Match(hasActiveRole: false),
            _ => null,
        });

        var result = await ConfirmAsync(staged);

        result.IsSuccess.Should().BeFalse();
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
    public async Task Sin_lote_de_origen_no_confirma()
    {
        var staged = await StagedAsync(Row());

        var result = await Processor().ConfirmBatchAsync(staged, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _added.Should().BeEmpty();
    }
}
