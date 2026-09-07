using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

public sealed class SupplierImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private readonly Mock<ISupplierImportSheetReader> _reader = new();
    private readonly Mock<IBusinessPartnerRepository> _bpRepo = new();
    private readonly Mock<IPaymentTermRepository> _paymentTermRepo = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IMediator> _mediator = new();

    private SupplierImportProcessor BuildProcessor()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        return new SupplierImportProcessor(
            _reader.Object,
            _bpRepo.Object,
            _paymentTermRepo.Object,
            _ctx.Object,
            _mediator.Object
        );
    }

    private void SetupPaymentTerms(params PaymentTerm[] terms) =>
        _paymentTermRepo
            .Setup(x => x.ListAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(terms);

    private void SetupDuplicate(bool exists) =>
        _bpRepo
            .Setup(x =>
                x.ExistsByIdentificationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(exists);

    private static PaymentTerm ActivePaymentTerm() =>
        PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, Guid.NewGuid());

    private static Dictionary<string, string?> ValidRow() =>
        new()
        {
            [SupplierImportColumns.IdentificationType] = "04",
            [SupplierImportColumns.IdentificationNumber] = "1790012345001",
            [SupplierImportColumns.LegalName] = "Proveedor Válido",
            [SupplierImportColumns.PaymentTermCode] = "CONTADO",
            [SupplierImportColumns.Email] = "contacto@proveedor.test",
            [SupplierImportColumns.Phone] = "0999999999",
        };

    [Fact]
    public async Task Fila_valida_con_contacto_no_genera_issues()
    {
        SetupPaymentTerms(ActivePaymentTerm());
        SetupDuplicate(false);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Fila_sin_razon_social_es_error_bloqueante()
    {
        SetupPaymentTerms(ActivePaymentTerm());
        SetupDuplicate(false);
        var processor = BuildProcessor();
        var row = ValidRow();
        row[SupplierImportColumns.LegalName] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD");
    }

    [Fact]
    public async Task Condicion_de_pago_faltante_es_error_bloqueante()
    {
        SetupPaymentTerms(ActivePaymentTerm());
        SetupDuplicate(false);
        var processor = BuildProcessor();
        var row = ValidRow();
        row[SupplierImportColumns.PaymentTermCode] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == SupplierImportColumns.PaymentTermCode);
    }

    [Fact]
    public async Task Condicion_de_pago_inexistente_es_error_bloqueante()
    {
        SetupPaymentTerms(); // catálogo vacío — el código de la fila no existe
        SetupDuplicate(false);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_PAYMENT_TERM");
    }

    [Fact]
    public async Task Identificacion_duplicada_es_error_bloqueante()
    {
        SetupPaymentTerms(ActivePaymentTerm());
        SetupDuplicate(true);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "DUPLICATE_IDENTIFICATION");
    }

    [Fact]
    public async Task Sin_email_ni_telefono_genera_warning_no_bloqueante()
    {
        SetupPaymentTerms(ActivePaymentTerm());
        SetupDuplicate(false);
        var processor = BuildProcessor();
        var row = ValidRow();
        row[SupplierImportColumns.Email] = null;
        row[SupplierImportColumns.Phone] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "MISSING_CONTACT_INFO" && i.Severity == ImportSeverity.Warning
        );
    }

    // ── ConfirmRowAsync — ADR-033: el destino de la condición de pago es
    // CompanyBpPurchaseSettings (company-scoped), no SupplierRoleConfig ─────────────────────

    private static string ValidParsedRowJson(Guid businessPartnerRoleTermId) =>
        System.Text.Json.JsonSerializer.Serialize(
            new ParsedSupplierRow(
                "04",
                "1790012345001",
                "Proveedor Válido",
                null,
                null,
                "contacto@proveedor.test",
                "0999999999",
                businessPartnerRoleTermId
            )
        );

    [Fact]
    public async Task ConfirmRowAsync_guarda_la_condicion_de_pago_en_CompanyBpPurchaseSettings()
    {
        var businessPartnerId = Guid.NewGuid();
        var processor = BuildProcessor();

        _mediator
            .Setup(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BusinessPartnerSummaryDto>.Success(
                    new BusinessPartnerSummaryDto(
                        businessPartnerId,
                        "04",
                        "1790012345001",
                        "Proveedor Válido",
                        null,
                        1,
                        null,
                        true,
                        DateTime.UtcNow
                    )
                )
            );
        _mediator
            .Setup(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BusinessPartnerRoleDto>.Success(
                    new BusinessPartnerRoleDto(
                        Guid.NewGuid(),
                        "Supplier",
                        "Proveedor",
                        true,
                        null,
                        DateTime.UtcNow,
                        null,
                        null,
                        null,
                        null
                    )
                )
            );
        UpsertCompanyBpPurchaseSettingsCommand? capturedCommand = null;
        _mediator
            .Setup(m =>
                m.Send(It.IsAny<UpsertCompanyBpPurchaseSettingsCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback<object, CancellationToken>(
                (cmd, _) => capturedCommand = (UpsertCompanyBpPurchaseSettingsCommand)cmd
            )
            .ReturnsAsync(
                Result<CompanyBpPurchaseSettingsDto>.Success(
                    new CompanyBpPurchaseSettingsDto(Guid.NewGuid(), businessPartnerId, PaymentTermId, true)
                )
            );

        var result = await processor.ConfirmRowAsync(ValidParsedRowJson(PaymentTermId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedCommand.Should().NotBeNull();
        capturedCommand!.BusinessPartnerId.Should().Be(businessPartnerId);
        capturedCommand.PaymentTermId.Should().Be(PaymentTermId);
    }

    [Fact]
    public async Task ConfirmRowAsync_falla_si_no_se_pudo_guardar_la_condicion_de_pago()
    {
        var businessPartnerId = Guid.NewGuid();
        var processor = BuildProcessor();

        _mediator
            .Setup(m => m.Send(It.IsAny<CreateBusinessPartnerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BusinessPartnerSummaryDto>.Success(
                    new BusinessPartnerSummaryDto(
                        businessPartnerId,
                        "04",
                        "1790012345001",
                        "Proveedor Válido",
                        null,
                        1,
                        null,
                        true,
                        DateTime.UtcNow
                    )
                )
            );
        _mediator
            .Setup(m => m.Send(It.IsAny<AssignBusinessPartnerRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BusinessPartnerRoleDto>.Success(
                    new BusinessPartnerRoleDto(
                        Guid.NewGuid(),
                        "Supplier",
                        "Proveedor",
                        true,
                        null,
                        DateTime.UtcNow,
                        null,
                        null,
                        null,
                        null
                    )
                )
            );
        _mediator
            .Setup(m =>
                m.Send(It.IsAny<UpsertCompanyBpPurchaseSettingsCommand>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyBpPurchaseSettingsDto>.ValidationFailure(
                    "La condición de pago seleccionada no existe."
                )
            );

        var result = await processor.ConfirmRowAsync(ValidParsedRowJson(PaymentTermId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("condición de pago");
    }
}
