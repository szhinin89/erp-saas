using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>IL-3A — Proveedores: Validate fiel a Confirm, catálogo SRI de uso y datos fiscales explícitos.</summary>
public sealed class SupplierImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string ValidRuc = "1791352688001";

    private readonly Mock<ISupplierImportSheetReader> _reader = new();
    private readonly Mock<IBusinessPartnerImportLookup> _lookup = new();
    private readonly Mock<IPaymentTermRepository> _paymentTermRepo = new();
    private readonly Mock<ILegalEntityTypeRepository> _legalEntityRepo = new();
    private readonly Mock<IIdentificationUsageValidator> _usage = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly PaymentTerm _contado = PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, Guid.NewGuid());
    private readonly PaymentTerm _credito = PaymentTerm.Create(TenantId, "CREDITO30", "Crédito 30", 1, 30, Guid.NewGuid());

    public SupplierImportProcessorTests()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        _paymentTermRepo.Setup(x => x.ListAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_contado, _credito]);
        _paymentTermRepo.Setup(x => x.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid id, CancellationToken _) => new[] { _contado, _credito }.FirstOrDefault(t => t.Id == id));
        _legalEntityRepo.Setup(x => x.ExistsActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // Catálogo real sri_id_type_usage: Proveedor solo admite 04 y 08.
        _usage.Setup(x => x.IsAllowedAsync(It.IsIn("04", "08"), IdentificationUsageType.Supplier,
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private SupplierImportProcessor Processor() => new(_reader.Object, _lookup.Object, _paymentTermRepo.Object,
        _legalEntityRepo.Object, _usage.Object, _ctx.Object, _mediator.Object);

    private static Dictionary<string, string?> Row(string type = "04", string number = ValidRuc) => new()
    {
        [SupplierImportColumns.IdentificationType] = type,
        [SupplierImportColumns.IdentificationNumber] = number,
        [SupplierImportColumns.LegalName] = "Proveedor Válido S.A.",
        [SupplierImportColumns.Email] = "contacto@proveedor.test",
        [SupplierImportColumns.Phone] = "0999999999",
        [SupplierImportColumns.PaymentTermCode] = "CONTADO",
        [SupplierImportColumns.IsRequiredToKeepAccounting] = "SI",
        [SupplierImportColumns.IsRetentionExempt] = "NO",
    };

    private void SetupExisting(BusinessPartnerImportMatch match) =>
        _lookup.Setup(x => x.FindByIdentificationAsync(It.IsAny<string>(), It.IsAny<string>(),
            RoleType.Supplier, It.IsAny<CancellationToken>())).ReturnsAsync(match);

    private static BusinessPartnerImportMatch Existing(Guid bpId, bool isSupplier, Guid? termId = null, bool active = true) =>
        new(bpId, active, ValidRuc, "Maestro S.A.", isSupplier, termId.HasValue, termId);

    private async Task<RowValidationResult> Validate(Dictionary<string, string?> row) =>
        await Processor().ValidateRowAsync(1, row, false, CancellationToken.None);

    private static ParsedSupplierRow Parsed(RowValidationResult r) =>
        JsonSerializer.Deserialize<ParsedSupplierRow>(r.ParsedDataJson)!;

    // ── Validación ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fila_valida_con_contacto_no_genera_issues()
    {
        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        var parsed = Parsed(result);
        parsed.Action.Should().Be(PartnerImportAction.Create);
        parsed.PaymentTermId.Should().Be(_contado.Id);
        parsed.IsRequiredToKeepAccounting.Should().BeTrue();
        parsed.IsRetentionExempt.Should().BeFalse();
    }

    [Fact]
    public async Task Fila_sin_razon_social_es_error_bloqueante()
    {
        var row = Row();
        row[SupplierImportColumns.LegalName] = null;

        var result = await Validate(row);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD");
    }

    [Theory]
    [InlineData(null, "MISSING_REQUIRED_FIELD")]
    [InlineData("NOEXISTE", "INVALID_PAYMENT_TERM")]
    public async Task Condicion_de_pago_sin_default(string? code, string expected)
    {
        var row = Row();
        row[SupplierImportColumns.PaymentTermCode] = code;

        var result = await Validate(row);

        result.Issues.Should().Contain(i => i.Code == expected && i.FieldName == SupplierImportColumns.PaymentTermCode);
    }

    [Theory]
    [InlineData("05", "0302126842")]
    [InlineData("06", "AB123456")]
    [InlineData("07", "9999999999999")]
    public async Task Tipos_no_permitidos_por_el_catalogo_para_proveedor_se_bloquean(string type, string number)
    {
        var result = await Validate(Row(type, number));

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.FieldName == SupplierImportColumns.IdentificationType
            && (i.Code == "INVALID_IDENTIFICATION_TYPE" || i.Code == "CONSUMIDOR_FINAL_NOT_IMPORTABLE"));
    }

    [Fact]
    public async Task Ruc_invalido_se_bloquea_en_validacion_sin_sufijo_tecnico()
    {
        var result = await Validate(Row(number: "1791352687001"));

        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_IDENTIFICATION")
            .Which.Message.Should().NotContain("Parameter");
    }

    [Fact]
    public async Task Ruc_sin_cero_inicial_reporta_perdida_de_cero()
    {
        var result = await Validate(Row(number: "302126842001"));

        result.Issues.Should().ContainSingle(i => i.Code == "LEADING_ZERO_LOST");
    }

    [Fact]
    public async Task Exterior_exige_tipo_entidad_legal()
    {
        var sinTipo = await Validate(Row("08", "EXT-778899"));
        sinTipo.Issues.Should().Contain(i => i.Code == "INVALID_LEGAL_ENTITY_TYPE");

        var row = Row("08", "EXT-778899");
        row[SupplierImportColumns.LegalEntityTypeCode] = "2";
        var conTipo = await Validate(row);
        conTipo.HasBlockingIssue.Should().BeFalse();
        Parsed(conTipo).LegalEntityTypeCode.Should().Be(2);
    }

    [Theory]
    [InlineData(SupplierImportColumns.IsRequiredToKeepAccounting, null, "MISSING_REQUIRED_FIELD")]
    [InlineData(SupplierImportColumns.IsRequiredToKeepAccounting, "TAL VEZ", "INVALID_YES_NO")]
    [InlineData(SupplierImportColumns.IsRetentionExempt, null, "MISSING_REQUIRED_FIELD")]
    [InlineData(SupplierImportColumns.IsRetentionExempt, "1", "INVALID_YES_NO")]
    public async Task Datos_fiscales_son_obligatorios_y_solo_SI_NO(string column, string? value, string expected)
    {
        var row = Row();
        row[column] = value;

        var result = await Validate(row);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == expected && i.FieldName == column);
    }

    [Fact]
    public async Task Sin_email_ni_telefono_genera_warning_no_bloqueante()
    {
        var row = Row();
        row[SupplierImportColumns.Email] = null;
        row[SupplierImportColumns.Phone] = null;

        var result = await Validate(row);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_CONTACT_INFO" && i.Severity == ImportSeverity.Warning);
    }

    [Fact]
    public async Task Bp_existente_sin_rol_proveedor_se_reutiliza()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Existing(bpId, isSupplier: false));

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).Action.Should().Be(PartnerImportAction.AssignRole);
        Parsed(result).ExistingBusinessPartnerId.Should().Be(bpId);
        result.Issues.Should().ContainSingle(i => i.Code == "EXISTING_BUSINESS_PARTNER");
    }

    [Fact]
    public async Task Proveedor_existente_es_idempotente_y_conserva_sus_datos_fiscales()
    {
        SetupExisting(Existing(Guid.NewGuid(), isSupplier: true, _contado.Id));

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).Action.Should().Be(PartnerImportAction.AlreadyHasRole);
        result.Issues.Should().Contain(i => i.Code == "EXISTING_SUPPLIER_FISCAL_DATA_KEPT"
            && i.Severity == ImportSeverity.Warning);
    }

    [Fact]
    public async Task Proveedor_existente_con_otra_condicion_en_la_empresa_es_error()
    {
        SetupExisting(Existing(Guid.NewGuid(), isSupplier: true, _credito.Id));

        var result = await Validate(Row());

        result.Issues.Should().ContainSingle(i => i.Code == "PAYMENT_TERM_CONFLICT")
            .Which.Message.Should().StartWith("El proveedor ya tiene otra condición de pago");
    }

    [Fact]
    public async Task Bp_existente_inactivo_se_bloquea()
    {
        SetupExisting(Existing(Guid.NewGuid(), isSupplier: false, active: false));

        var result = await Validate(Row());

        result.Issues.Should().Contain(i => i.Code == "INACTIVE_BUSINESS_PARTNER");
    }

    [Fact]
    public async Task Identificacion_repetida_en_el_archivo_bloquea_todas_sus_filas()
    {
        var processor = Processor();
        var rows = new List<RowValidationResult>
        {
            await processor.ValidateRowAsync(1, Row("08", "EXT-1"), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row("08", "ext-1"), false, CancellationToken.None),
            await processor.ValidateRowAsync(3, Row(), false, CancellationToken.None),
        };

        var result = processor.ValidateBatch(rows);

        result[0].Issues.Should().Contain(i => i.Code == "DUPLICATE_IDENTIFICATION_IN_FILE");
        result[1].Issues.Should().Contain(i => i.Code == "DUPLICATE_IDENTIFICATION_IN_FILE");
        result[2].Issues.Should().NotContain(i => i.Code == "DUPLICATE_IDENTIFICATION_IN_FILE");
    }

    // ── Rol Proveedor revocado: se distingue de "sin rol" ──────────────────────────────────

    private static BusinessPartnerImportMatch Revoked(Guid bpId, bool hasFiscalData, Guid? termId = null) =>
        new(bpId, true, ValidRuc, "Ex Proveedor S.A.", false, termId.HasValue, termId,
            HasRevokedRole: true, RevokedRoleHasFiscalData: hasFiscalData);

    [Fact]
    public async Task Proveedor_revocado_con_datos_fiscales_se_reactiva_y_advierte_que_el_SI_NO_no_se_aplica()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Revoked(bpId, hasFiscalData: true));

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).Action.Should().Be(PartnerImportAction.ReactivateRole);
        Parsed(result).ExistingBusinessPartnerId.Should().Be(bpId);
        result.Issues.Should().ContainSingle(i => i.Code == "REVOKED_SUPPLIER_REACTIVATED"
            && i.Severity == ImportSeverity.Warning).Which.Message.Should().Contain("NO se aplican");
    }

    [Fact]
    public async Task Proveedor_revocado_sin_datos_fiscales_es_error_sin_defaults()
    {
        SetupExisting(Revoked(Guid.NewGuid(), hasFiscalData: false));

        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "REVOKED_SUPPLIER_FISCAL_DATA_MISSING");
        result.Issues.Should().NotContain(i => i.Code == "REVOKED_SUPPLIER_REACTIVATED");
    }

    [Fact]
    public async Task Proveedor_revocado_con_otra_condicion_en_la_empresa_es_error()
    {
        SetupExisting(Revoked(Guid.NewGuid(), hasFiscalData: true, _credito.Id));

        var result = await Validate(Row());

        result.Issues.Should().Contain(i => i.Code == "PAYMENT_TERM_CONFLICT");
    }

    [Fact]
    public async Task ConfirmRowAsync_reactiva_el_rol_sin_enviar_datos_fiscales()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Revoked(bpId, hasFiscalData: true));
        SetupRoleOk();
        SetupSettings(Result<CompanyBpPurchaseSettingsDto>.Success(null!));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.ReactivateRole, bpId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        _mediator.Verify(m => m.Send(It.Is<AssignBusinessPartnerRoleCommand>(c => c.BusinessPartnerId == bpId
            && c.RoleType == RoleType.Supplier && c.SupplierConfig == null), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmRowAsync_revocado_que_perdio_sus_datos_fiscales_falla_sin_escribir()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Revoked(bpId, hasFiscalData: false));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.ReactivateRole, bpId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Vuelva a validar");
        _mediator.Verify(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmRowAsync_asignar_rol_pero_ahora_esta_revocado_falla_sin_escribir()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Revoked(bpId, hasFiscalData: true));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.AssignRole, bpId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("revocado").And.Contain("Vuelva a validar");
        _mediator.Verify(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Confirmación: ejecuta exactamente la acción validada ───────────────────────────────

    private string Json(PartnerImportAction action, Guid? existingId, Guid? termId = null) =>
        JsonSerializer.Serialize(new ParsedSupplierRow("04", ValidRuc, null, "Proveedor Válido S.A.", null, null,
            "contacto@proveedor.test", null, termId ?? _contado.Id, true, false, action, existingId));

    private void SetupCreateOk(Guid bpId) =>
        _mediator.Setup(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BusinessPartnerSummaryDto>.Success(new BusinessPartnerSummaryDto(
                bpId, "04", ValidRuc, "Proveedor Válido S.A.", null, 2, null, true, DateTime.UtcNow)));

    private void SetupRoleOk() =>
        _mediator.Setup(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BusinessPartnerRoleDto>.Success(null!));

    private void SetupContactOk() =>
        _mediator.Setup(m => m.Send(It.IsAny<CreateBpContactCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BpContactDto>.Success(null!));

    private void SetupSettings(Result<CompanyBpPurchaseSettingsDto> result) =>
        _mediator.Setup(m => m.Send(It.IsAny<UpsertCompanyBpPurchaseSettingsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task ConfirmRowAsync_guarda_la_condicion_de_pago_en_CompanyBpPurchaseSettings()
    {
        var bpId = Guid.NewGuid();
        SetupCreateOk(bpId);
        SetupRoleOk();
        SetupContactOk();
        SetupSettings(Result<CompanyBpPurchaseSettingsDto>.Success(null!));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.Create, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mediator.Verify(m => m.Send(It.Is<AssignBusinessPartnerRoleCommand>(c => c.RoleType == RoleType.Supplier
            && c.SupplierConfig!.IsRequiredToKeepAccounting && !c.SupplierConfig.IsRetentionExempt),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<UpsertCompanyBpPurchaseSettingsCommand>(c =>
            c.BusinessPartnerId == bpId && c.PaymentTermId == _contado.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmRowAsync_falla_si_no_se_pudo_guardar_la_condicion_de_pago()
    {
        SetupCreateOk(Guid.NewGuid());
        SetupRoleOk();
        SetupContactOk();
        SetupSettings(Result<CompanyBpPurchaseSettingsDto>.ValidationFailure("La condición de pago se encuentra inactiva."));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.Create, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Condición de pago");
    }

    [Fact]
    public async Task ConfirmRowAsync_con_contacto_fallido_falla_la_fila()
    {
        SetupCreateOk(Guid.NewGuid());
        SetupRoleOk();
        _mediator.Setup(m => m.Send(It.IsAny<CreateBpContactCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BpContactDto>.ValidationFailure("Formato de email inválido."));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.Create, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contacto inválido");
    }

    [Fact]
    public async Task ConfirmRowAsync_bp_sin_rol_asigna_rol_proveedor_sin_crear_bp()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Existing(bpId, isSupplier: false));
        SetupRoleOk();
        SetupSettings(Result<CompanyBpPurchaseSettingsDto>.Success(null!));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.AssignRole, bpId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mediator.Verify(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<CreateBpContactCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.Is<AssignBusinessPartnerRoleCommand>(c => c.BusinessPartnerId == bpId
            && c.RoleType == RoleType.Supplier), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmRowAsync_proveedor_existente_con_misma_condicion_no_escribe()
    {
        var bpId = Guid.NewGuid();
        SetupExisting(Existing(bpId, isSupplier: true, _contado.Id));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.AlreadyHasRole, bpId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mediator.Verify(m => m.Send(It.IsAny<IRequest<Result<BusinessPartnerRoleDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<UpsertCompanyBpPurchaseSettingsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmRowAsync_maestro_cambiado_desde_la_validacion_falla_sin_escribir()
    {
        SetupExisting(Existing(Guid.NewGuid(), isSupplier: false));

        var result = await Processor().ConfirmRowAsync(Json(PartnerImportAction.Create, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Vuelva a validar");
        _mediator.Verify(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
