using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// ADR-048 / SEC-SUSP-01 — empresa suspendida o deshabilitada: el <see cref="ElectronicDocumentIssuer"/> pausa la
/// TRANSMISIÓN NUEVA (comprobante que nunca salió al SRI: Draft, Failed, DeadLetter desde ellos) sin escrituras ni
/// DeadLetter por exhaución, y deja pasar la consulta de autorización de todo envío previo o incierto
/// (Signed, Received, DeadLetter desde ellos). Mismo código <c>ELECTRONIC_DOCUMENT_EMISSION_DISABLED</c>: los
/// consumidores existentes lo tratan como pendiente. El interruptor SRI conserva su precedencia.
/// </summary>
public sealed class ElectronicDocumentIssuerCompanyPauseTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SourceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string AccessKeyValue = new('1', 49);

    private sealed class Harness
    {
        public Mock<IElectronicDocumentRepository> Repository { get; } = new();
        public Mock<IElectronicDocumentXmlSupplierResolver> Suppliers { get; } = new();
        public Mock<IElectronicDocumentSchemaValidatorResolver> Validators { get; } = new();
        public Mock<IElectronicDocumentSigningService> Signing { get; } = new();
        public Mock<IElectronicDocumentXmlStorageService> Storage { get; } = new();
        public Mock<IElectronicDocumentReceptionService> Reception { get; } = new(MockBehavior.Strict);
        public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public RetentionElectronicTestDoubles.FixedEmissionSwitch Emission { get; }
        public ElectronicDocument? Added { get; private set; }

        public Harness(ElectronicEmissionState emission, IElectronicDocumentSourceLifecycleGuard? guard = null)
        {
            Emission = (RetentionElectronicTestDoubles.FixedEmissionSwitch)RetentionElectronicTestDoubles.Emission();
            Emission.State = emission;
            Repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Repository
                .Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
                .Callback<ElectronicDocument, CancellationToken>((d, _) => Added = d)
                .Returns(Task.CompletedTask);
            Authorization
                .Setup(a => a.CheckAsync(CompanyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriAuthorizationResult>.Failure("SRI no disponible (test)."));

            var dbEx = new Mock<IDatabaseExceptionTranslator>();
            DatabaseUniqueViolationInfo? none = null;
            dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

            Issuer = new ElectronicDocumentIssuer(
                Repository.Object,
                Suppliers.Object,
                Validators.Object,
                Signing.Object,
                Storage.Object,
                Reception.Object,
                Authorization.Object,
                dbEx.Object,
                guard is null
                    ? RetentionElectronicTestDoubles.NoGuards()
                    : new ElectronicDocumentSourceLifecycleGuardResolver([guard]),
                Emission,
                UnitOfWork.Object,
                NullLogger<ElectronicDocumentIssuer>.Instance
            );
        }

        public ElectronicDocumentIssuer Issuer { get; }

        public void Returns(ElectronicDocument document) =>
            Repository
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

        /// <summary>Pipeline previo al envío completo (XML válido, firmado y almacenado).</summary>
        public void PipelineReady()
        {
            var xml = new ElectronicDocumentXml("<factura/>", "UTF-8", "1.1.0", ElectronicDocumentType.Invoice,
                "1", AccessKeyValue, DateTime.UtcNow);
            var supplier = new Mock<IElectronicDocumentXmlSupplier>();
            supplier
                .Setup(s => s.BuildXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ElectronicDocumentXml>.Success(xml));
            Suppliers.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(supplier.Object);
            var validator = new Mock<IElectronicDocumentSchemaValidator>();
            validator
                .Setup(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ElectronicDocumentSchemaValidationResult(true, [], [], "1.1.0",
                    ElectronicDocumentType.Invoice));
            Validators.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(validator.Object);
            Signing
                .Setup(s => s.SignAsync(TenantId, CompanyId, It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignedElectronicDocumentXml>.Success(new SignedElectronicDocumentXml(
                    "<factura><ds:Signature/></factura>", "UTF-8", "1.1.0", ElectronicDocumentType.Invoice,
                    AccessKeyValue, DateTime.UtcNow)));
            Storage
                .Setup(s => s.StoreAsync(TenantId, ElectronicDocumentType.Invoice, It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ElectronicDocumentStoredXmlPaths>.Success(
                    new ElectronicDocumentStoredXmlPaths("draft.xml", "signed.xml")));
        }
    }

    private static ElectronicEmissionState Enabled(DateTime? since = null) =>
        new(true, since ?? DateTime.MinValue);

    private static RegisterElectronicDocumentRequest Register(Guid userId) =>
        new(TenantId, CompanyId, ElectronicDocumentType.Invoice, "Sales", SourceId, userId);

    private static ElectronicDocument NewDraft() =>
        ElectronicDocument.Create(TenantId, CompanyId, ElectronicDocumentType.Invoice, "Sales", SourceId, UserId);

    private static ElectronicDocument NewFailed(int failures)
    {
        var document = NewDraft();
        for (var i = 0; i < failures; i++)
            document.MarkFailed("fallo previo (test)", UserId);
        return document;
    }

    private static ElectronicDocument NewSigned()
    {
        var document = NewDraft();
        document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("signed.xml", AccessKey.Create(AccessKeyValue), UserId);
        return document;
    }

    private static ElectronicDocument NewReceived()
    {
        var document = NewSigned();
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        return document;
    }


    private static ElectronicEmissionState Paused(CompanyPauseReason reason) =>
        Enabled() with { CompanyPause = reason };

    public static TheoryData<CompanyPauseReason> PauseReasons =>
        new() { CompanyPauseReason.Suspended, CompanyPauseReason.Inactive, CompanyPauseReason.Suspended | CompanyPauseReason.Inactive };

    [Fact]
    public void Predicado_pausa_solo_suspendida_o_deshabilitada_y_no_PendingSetup()
    {
        CompanyAutomaticProcessingPause.ReasonOf(true, CompanyOperationalStatus.Operational).Should().Be(CompanyPauseReason.None);
        CompanyAutomaticProcessingPause.ReasonOf(true, CompanyOperationalStatus.PendingSetup).Should().Be(CompanyPauseReason.None);
        CompanyAutomaticProcessingPause.ReasonOf(true, CompanyOperationalStatus.Suspended).Should().Be(CompanyPauseReason.Suspended);
        CompanyAutomaticProcessingPause.ReasonOf(false, CompanyOperationalStatus.Operational).Should().Be(CompanyPauseReason.Inactive);
        CompanyAutomaticProcessingPause.ReasonOf(false, CompanyOperationalStatus.Suspended)
            .Should().Be(CompanyPauseReason.Suspended | CompanyPauseReason.Inactive);
    }

    [Theory]
    [MemberData(nameof(PauseReasons))]
    public async Task Registrar_crea_Draft_pero_no_genera_firma_ni_transmite(CompanyPauseReason reason)
    {
        var h = new Harness(Paused(reason));

        var result = await h.Issuer.RegisterAsync(Register(UserId));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        result.Error.Should().Contain("suspendida o deshabilitada");
        h.Added!.CurrentState.Should().Be(ElectronicDocumentState.Draft);
        h.Added.RetryCount.Should().Be(0);
        h.Suppliers.Verify(r => r.Resolve(It.IsAny<ElectronicDocumentType>()), Times.Never);
        h.Signing.VerifyNoOtherCalls();
        h.Reception.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(PauseReasons))]
    public async Task Reintento_de_Failed_no_escribe_no_consume_intentos_ni_pasa_a_DeadLetter(CompanyPauseReason reason)
    {
        var document = NewFailed(failures: 10);
        var h = new Harness(Paused(reason));
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.CurrentState.Should().Be(ElectronicDocumentState.Failed);
        document.RetryCount.Should().Be(10);
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Reception.VerifyNoOtherCalls();
        h.Authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeadLetter_desde_Failed_no_se_reactiva_mientras_la_empresa_esta_pausada()
    {
        var document = NewFailed(failures: 1);
        document.MarkDeadLetter("agotado (test)", UserId);
        var h = new Harness(Paused(CompanyPauseReason.Suspended));
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(CompanyPauseReason.Suspended)]
    [InlineData(CompanyPauseReason.Inactive)]
    public async Task Signed_incierto_se_sigue_consultando_y_registra_el_intento(CompanyPauseReason reason)
    {
        var document = NewSigned();
        var retries = document.RetryCount;
        var h = new Harness(Paused(reason));
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        result.IsSuccess.Should().BeTrue("la consulta de un envío incierto no se pausa");
        h.Authorization.Verify(a => a.CheckAsync(CompanyId, AccessKeyValue, It.IsAny<CancellationToken>()), Times.Once);
        document.RetryCount.Should().Be(retries + 1);
        document.CurrentState.Should().Be(ElectronicDocumentState.Signed, "una consulta no concluyente conserva el estado");
        h.Reception.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(CompanyPauseReason.Suspended)]
    [InlineData(CompanyPauseReason.Inactive)]
    public async Task Received_se_sigue_consultando(CompanyPauseReason reason)
    {
        var document = NewReceived();
        var h = new Harness(Paused(reason));
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        result.IsSuccess.Should().BeTrue();
        h.Authorization.Verify(a => a.CheckAsync(CompanyId, AccessKeyValue, It.IsAny<CancellationToken>()), Times.Once);
        h.Reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeadLetter_desde_Received_se_reactiva_y_se_consulta()
    {
        var document = NewReceived();
        document.MarkDeadLetter("plazo agotado (test)", UserId);
        var h = new Harness(Paused(CompanyPauseReason.Suspended));
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue();
        h.Authorization.Verify(a => a.CheckAsync(CompanyId, AccessKeyValue, It.IsAny<CancellationToken>()), Times.Once);
        h.Reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_interruptor_deshabilitado_conserva_su_precedencia_y_mensaje()
    {
        var document = NewSigned();
        var h = new Harness(ElectronicEmissionState.Disabled with { CompanyPause = CompanyPauseReason.Suspended });
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        result.Error.Should().Contain("no se envía ni se consulta", "pausa total del interruptor, no de la empresa");
        h.Authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Al_reactivar_la_empresa_el_mismo_comprobante_reanuda_con_sus_intentos_intactos()
    {
        var document = NewFailed(failures: 3);
        var h = new Harness(Paused(CompanyPauseReason.Suspended));
        h.Returns(document);

        var paused = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);
        paused.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.RetryCount.Should().Be(3);

        h.Emission.State = Enabled(); // ambos estados restablecidos
        var resumed = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        resumed.Code.Should().NotBe(ApiResponseCodes.ElectronicDocuments.EmissionDisabled, "el pipeline vuelve a ejecutarse");
        h.Suppliers.Verify(r => r.Resolve(ElectronicDocumentType.Invoice), Times.Once);
    }
}
