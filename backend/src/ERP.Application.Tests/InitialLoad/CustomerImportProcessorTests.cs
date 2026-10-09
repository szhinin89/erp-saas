using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>IL-2A — Validate fiel a Confirm y clasificación contra el maestro existente.</summary>
public sealed class CustomerImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string ValidRuc = "1791352688001";
    private const string ValidCedula = "0302126842";

    private readonly Mock<ICustomerImportSheetReader> _reader = new();
    private readonly Mock<ICustomerImportLookup> _lookup = new();
    private readonly Mock<IPaymentTermRepository> _paymentTermRepo = new();
    private readonly Mock<ILegalEntityTypeRepository> _legalEntityRepo = new();
    private readonly Mock<IIdentificationUsageValidator> _usage = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly PaymentTerm _contado = PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, Guid.NewGuid());
    private readonly PaymentTerm _credito = PaymentTerm.Create(TenantId, "CREDITO30", "Crédito 30", 1, 30, Guid.NewGuid());

    public CustomerImportProcessorTests()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        _paymentTermRepo.Setup(x => x.ListAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_contado, _credito]);
        _legalEntityRepo.Setup(x => x.ExistsActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _usage.Setup(x => x.IsAllowedAsync(It.IsIn("04", "05", "06", "07", "08", "09"),
                IdentificationUsageType.Customer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private CustomerImportProcessor Processor() => new(_reader.Object, _lookup.Object, _paymentTermRepo.Object,
        _legalEntityRepo.Object, _usage.Object, _ctx.Object, _mediator.Object);

    private static Dictionary<string, string?> Row(string type = "04", string number = ValidRuc) => new()
    {
        [CustomerImportColumns.IdentificationType] = type,
        [CustomerImportColumns.IdentificationNumber] = number,
        [CustomerImportColumns.LegalName] = "Cliente Válido S.A.",
        [CustomerImportColumns.Email] = "cliente@ejemplo.test",
        [CustomerImportColumns.Phone] = "0999999999",
        [CustomerImportColumns.PaymentTermCode] = "contado",
    };

    private void SetupExisting(CustomerImportMatch match) =>
        _lookup.Setup(x => x.FindByIdentificationAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(match);

    private static CustomerImportMatch Existing(bool isCustomer, Guid? termId = null, bool active = true) =>
        new(Guid.NewGuid(), active, ValidRuc, "Maestro S.A.", isCustomer, termId.HasValue, termId);

    private async Task<RowValidationResult> Validate(Dictionary<string, string?> row) =>
        await Processor().ValidateRowAsync(1, row, false, CancellationToken.None);

    private static ParsedCustomerRow Parsed(RowValidationResult r) =>
        JsonSerializer.Deserialize<ParsedCustomerRow>(r.ParsedDataJson)!;

    [Fact]
    public async Task Ruc_nuevo_valido_se_clasifica_como_creacion_sin_issues()
    {
        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        Parsed(result).Action.Should().Be(CustomerImportAction.CreateCustomer);
        Parsed(result).PaymentTermId.Should().Be(_contado.Id);
    }

    [Fact]
    public async Task Ruc_con_digito_verificador_invalido_se_bloquea_en_validacion()
    {
        var result = await Validate(Row(number: "1791352687001"));

        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_IDENTIFICATION")
            .Which.Message.Should().StartWith("RUC '").And.NotContain("Parameter");
    }

    [Fact]
    public async Task Cedula_sin_cero_inicial_reporta_perdida_de_cero()
    {
        var result = await Validate(Row("05", ValidCedula.TrimStart('0')));

        result.Issues.Should().ContainSingle(i => i.Code == "LEADING_ZERO_LOST");
    }

    [Fact]
    public async Task Tipo_identificacion_sin_cero_inicial_reporta_perdida_de_cero()
    {
        var result = await Validate(Row("4"));

        result.Issues.Should().Contain(i => i.Code == "LEADING_ZERO_LOST"
            && i.FieldName == CustomerImportColumns.IdentificationType);
    }

    [Fact]
    public async Task Pasaporte_sin_tipo_entidad_legal_se_bloquea_y_con_tipo_es_valido()
    {
        var sinTipo = await Validate(Row("06", "AB123456"));
        sinTipo.Issues.Should().ContainSingle(i => i.Code == "INVALID_LEGAL_ENTITY_TYPE")
            .Which.Message.Should().Be("El tipo de entidad legal es obligatorio para el tipo de identificación '06' "
                + "— no puede inferirse automáticamente.");

        var row = Row("06", "AB123456");
        row[CustomerImportColumns.LegalEntityTypeCode] = "1";
        var conTipo = await Validate(row);
        conTipo.HasBlockingIssue.Should().BeFalse();
        Parsed(conTipo).LegalEntityTypeCode.Should().Be(1);
    }

    [Fact]
    public async Task Tipo_entidad_legal_contradictorio_con_ruc_se_bloquea()
    {
        var row = Row();
        row[CustomerImportColumns.LegalEntityTypeCode] = "1";

        var result = await Validate(row);

        result.Issues.Should().Contain(i => i.Code == "INVALID_LEGAL_ENTITY_TYPE");
    }

    [Fact]
    public async Task Consumidor_final_no_se_importa()
    {
        var result = await Validate(Row("07", "9999999999999"));

        result.Issues.Should().ContainSingle(i => i.Code == "CONSUMIDOR_FINAL_NOT_IMPORTABLE");
    }

    [Fact]
    public async Task Tipo_no_permitido_para_clientes_se_bloquea()
    {
        var result = await Validate(Row("99", "ABC"));

        result.Issues.Should().Contain(i => i.Code == "INVALID_IDENTIFICATION_TYPE");
    }

    [Theory]
    [InlineData(null, "MISSING_REQUIRED_FIELD")]
    [InlineData("NOEXISTE", "INVALID_PAYMENT_TERM")]
    public async Task Condicion_de_pago_sin_default(string? code, string expected)
    {
        var row = Row();
        row[CustomerImportColumns.PaymentTermCode] = code;

        var result = await Validate(row);

        result.Issues.Should().Contain(i => i.Code == expected && i.FieldName == CustomerImportColumns.PaymentTermCode);
    }

    [Fact]
    public async Task Email_invalido_se_bloquea_en_validacion()
    {
        var row = Row();
        row[CustomerImportColumns.Email] = "no-es-email";

        var result = await Validate(row);

        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_CONTACT")
            .Which.Message.Should().Be("Formato de email inválido.");
    }

    [Fact]
    public async Task Bp_existente_sin_rol_cliente_reutiliza_el_bp()
    {
        var match = Existing(isCustomer: false);
        SetupExisting(match);

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "EXISTING_BUSINESS_PARTNER"
            && i.Severity == ImportSeverity.Warning);
        Parsed(result).Action.Should().Be(CustomerImportAction.AssignCustomerRole);
        Parsed(result).ExistingBusinessPartnerId.Should().Be(match.BusinessPartnerId);
    }

    [Fact]
    public async Task Cliente_existente_con_misma_condicion_es_idempotente()
    {
        SetupExisting(Existing(isCustomer: true, _contado.Id));

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).Action.Should().Be(CustomerImportAction.AlreadyCustomer);
    }

    [Fact]
    public async Task Cliente_existente_con_otra_condicion_no_se_sobrescribe()
    {
        SetupExisting(Existing(isCustomer: true, _credito.Id));

        var result = await Validate(Row());

        result.Issues.Should().Contain(i => i.Code == "PAYMENT_TERM_CONFLICT");
    }

    [Fact]
    public async Task Bp_existente_inactivo_se_bloquea()
    {
        SetupExisting(Existing(isCustomer: false, active: false));

        var result = await Validate(Row());

        result.Issues.Should().Contain(i => i.Code == "INACTIVE_BUSINESS_PARTNER");
    }

    [Fact]
    public async Task Identificacion_repetida_en_el_archivo_bloquea_todas_sus_filas()
    {
        var processor = Processor();
        var a = Row("06", "AB123456");
        a[CustomerImportColumns.LegalEntityTypeCode] = "1";
        var b = Row("06", "ab123456");
        b[CustomerImportColumns.LegalEntityTypeCode] = "1";
        var c = Row();
        var rows = new List<RowValidationResult>();
        foreach (var row in new[] { a, b, c })
            rows.Add(await processor.ValidateRowAsync(1, row, false, CancellationToken.None));

        var result = processor.ValidateBatch(rows);

        result[0].Issues.Should().Contain(i => i.Code == "DUPLICATE_IDENTIFICATION_IN_FILE");
        result[1].Issues.Should().Contain(i => i.Code == "DUPLICATE_IDENTIFICATION_IN_FILE");
        result[2].HasBlockingIssue.Should().BeFalse();
    }

    // ── ConfirmRowAsync (IL-2B): revalida contra el maestro y ejecuta la acción validada ─────

    private static string Json(CustomerImportAction action, Guid? existingId, Guid termId) =>
        JsonSerializer.Serialize(new ParsedCustomerRow("04", ValidRuc, null, "Cliente Válido S.A.", null, null,
            "cliente@ejemplo.test", null, termId, action, existingId));

    private void SetupTermLookup() =>
        _paymentTermRepo.Setup(x => x.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid id, CancellationToken _) => new[] { _contado, _credito }.FirstOrDefault(t => t.Id == id));

    private void SetupSettingsOk() =>
        _mediator.Setup(m => m.Send(It.IsAny<UpsertCompanyBpSalesSettingsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyBpSalesSettingsDto>.Success(null!));

    private void SetupRoleOk() =>
        _mediator.Setup(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BusinessPartnerRoleDto>.Success(null!));

    private static CustomerImportMatch MatchFor(Guid bpId, bool isCustomer, Guid? termId = null) =>
        new(bpId, true, ValidRuc, "Maestro S.A.", isCustomer, termId.HasValue, termId);

    private void VerifyNoWrites()
    {
        _mediator.Verify(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<UpsertCompanyBpSalesSettingsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_cliente_existente_sin_condicion_solo_registra_condicion_por_empresa()
    {
        var bpId = Guid.NewGuid();
        SetupTermLookup();
        SetupExisting(MatchFor(bpId, isCustomer: true));
        SetupSettingsOk();

        var result = await Processor().ConfirmRowAsync(
            Json(CustomerImportAction.AlreadyCustomer, bpId, _contado.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.BusinessPartnerId.Should().Be(bpId);
        _mediator.Verify(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.Is<UpsertCompanyBpSalesSettingsCommand>(c =>
            c.BusinessPartnerId == bpId && c.PaymentTermId == _contado.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_cliente_existente_con_misma_condicion_no_escribe_nada()
    {
        var bpId = Guid.NewGuid();
        SetupTermLookup();
        SetupExisting(MatchFor(bpId, isCustomer: true, _contado.Id));

        var result = await Processor().ConfirmRowAsync(
            Json(CustomerImportAction.AlreadyCustomer, bpId, _contado.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.BusinessPartnerId.Should().Be(bpId);
        VerifyNoWrites();
    }

    [Fact]
    public async Task Confirmar_bp_existente_sin_rol_asigna_rol_sin_crear_bp()
    {
        var bpId = Guid.NewGuid();
        SetupTermLookup();
        SetupExisting(MatchFor(bpId, isCustomer: false));
        SetupRoleOk();
        SetupSettingsOk();

        var result = await Processor().ConfirmRowAsync(
            Json(CustomerImportAction.AssignCustomerRole, bpId, _contado.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mediator.Verify(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.Is<AssignBusinessPartnerRoleCommand>(c => c.BusinessPartnerId == bpId
            && c.CustomerConfig == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_cliente_nuevo_con_contacto_fallido_falla_la_fila()
    {
        var bpId = Guid.NewGuid();
        SetupTermLookup();
        _mediator.Setup(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BusinessPartnerSummaryDto>.Success(new BusinessPartnerSummaryDto(
                bpId, "04", ValidRuc, "Cliente Válido S.A.", null, 2, null, true, DateTime.UtcNow)));
        SetupRoleOk();
        _mediator.Setup(m => m.Send(It.IsAny<CreateBpContactCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BpContactDto>.ValidationFailure("Formato de email inválido."));

        var result = await Processor().ConfirmRowAsync(
            Json(CustomerImportAction.CreateCustomer, null, _contado.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contacto inválido");
        _mediator.Verify(m => m.Send(It.IsAny<UpsertCompanyBpSalesSettingsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<string, CustomerImportAction, bool, bool, bool> StaleCases => new()
    {
        // caso, acción validada, existe ahora, es cliente ahora, otra condición ahora
        { "nuevo pero ya existe", CustomerImportAction.CreateCustomer, true, false, false },
        { "asignar rol pero ya es cliente", CustomerImportAction.AssignCustomerRole, true, true, false },
        { "asignar rol pero desapareció", CustomerImportAction.AssignCustomerRole, false, false, false },
        { "ya cliente pero perdió el rol", CustomerImportAction.AlreadyCustomer, true, false, false },
        { "ya cliente con otra condición", CustomerImportAction.AlreadyCustomer, true, true, true },
    };

    [Theory]
    [MemberData(nameof(StaleCases))]
    public async Task Maestro_cambiado_desde_la_validacion_falla_sin_escribir(
        string caso, CustomerImportAction action, bool existsNow, bool isCustomerNow, bool otherTermNow)
    {
        var bpId = Guid.NewGuid();
        SetupTermLookup();
        if (existsNow)
            SetupExisting(MatchFor(bpId, isCustomerNow, otherTermNow ? _credito.Id : null));

        var result = await Processor().ConfirmRowAsync(
            Json(action, action == CustomerImportAction.CreateCustomer ? null : bpId, _contado.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse(caso);
        result.Error.Should().Contain("Vuelva a validar");
        VerifyNoWrites();
    }

    [Fact]
    public async Task Condicion_de_pago_eliminada_desde_la_validacion_falla_sin_escribir()
    {
        SetupTermLookup();

        var result = await Processor().ConfirmRowAsync(
            Json(CustomerImportAction.CreateCustomer, null, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("condición de pago");
        VerifyNoWrites();
    }
}
