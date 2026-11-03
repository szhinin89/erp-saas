using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// SRI-TOGGLE-02/06 — control de emisión por empresa en <see cref="ElectronicDocumentIssuer"/>: sin emisión
/// habilitada no hay XML, firma, transmisión NI consulta al SRI (pausa total, sin escrituras sobre el
/// comprobante); un proceso automático no reanuda pendientes anteriores a la habilitación; el reclamo
/// revalida con FOR SHARE dentro de su transacción y la consulta de autorización se revalida justo antes.
/// </summary>
public sealed class ElectronicDocumentIssuerEmissionSwitchTests
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

    [Fact]
    public async Task Deshabilitada_registrar_crea_Draft_sin_XML_firma_ni_transmision()
    {
        var h = new Harness(ElectronicEmissionState.Disabled);

        var result = await h.Issuer.RegisterAsync(Register(UserId));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Added!.CurrentState.Should().Be(ElectronicDocumentState.Draft, "visible en el Monitor como pendiente");
        h.Added.RetryCount.Should().Be(0);
        h.Added.AccessKey.Should().BeNull();
        h.Suppliers.Verify(r => r.Resolve(It.IsAny<ElectronicDocumentType>()), Times.Never);
        h.Signing.VerifyNoOtherCalls();
        h.Reception.VerifyNoOtherCalls();
        h.Emission.Calls.Should().ContainSingle().Which.Should().Be((TenantId, CompanyId, false));
    }

    [Fact]
    public async Task Deshabilitada_reintento_de_Failed_no_escribe_ni_pasa_a_DeadLetter()
    {
        var document = NewFailed(failures: 10);
        var h = new Harness(ElectronicEmissionState.Disabled);
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.CurrentState.Should().Be(ElectronicDocumentState.Failed);
        document.RetryCount.Should().Be(10);
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Deshabilitada_reactivar_DeadLetter_hacia_Failed_se_bloquea_antes_de_escribir()
    {
        var document = NewFailed(failures: 1);
        document.MarkDeadLetter("agotado (test)", UserId);
        var h = new Harness(ElectronicEmissionState.Disabled);
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SRI_TOGGLE_06_deshabilitada_un_Signed_no_se_consulta_ni_registra_intento(bool automatic)
    {
        var document = NewSigned();
        var retries = document.RetryCount;
        var h = new Harness(ElectronicEmissionState.Disabled);
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, automatic ? Guid.Empty : UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        result.Error.Should().Contain("no se envía ni se consulta");
        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        document.RetryCount.Should().Be(retries);
        h.Authorization.VerifyNoOtherCalls();
        h.Reception.VerifyNoOtherCalls();
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SRI_TOGGLE_06_deshabilitada_un_Received_con_dispatch_protocol_no_se_consulta()
    {
        var guard = new Mock<IElectronicDocumentSourceLifecycleGuard>();
        guard.SetupGet(g => g.SourceModule).Returns("Sales");
        guard.SetupGet(g => g.UsesDispatchProtocol).Returns(true);
        guard
            .Setup(g => g.EvaluateAsync(TenantId, CompanyId, SourceId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElectronicDocumentSourceLifecycle.Allowed);
        var document = NewReceived();
        var h = new Harness(ElectronicEmissionState.Disabled, guard.Object);
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        h.Authorization.VerifyNoOtherCalls();
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SRI_TOGGLE_06_deshabilitada_reactivar_DeadLetter_de_consulta_se_bloquea_antes_de_escribir()
    {
        var document = NewReceived();
        document.MarkDeadLetter("agotado (test)", UserId);
        var h = new Harness(ElectronicEmissionState.Disabled);
        h.Returns(document);

        var result = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        h.Authorization.VerifyNoOtherCalls();
        h.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SRI_TOGGLE_06_habilitada_consulta_automatica_de_un_Received_anterior_requiere_accion_explicita()
    {
        var document = NewReceived();
        var h = new Harness(Enabled(since: DateTime.UtcNow.AddMinutes(1)));
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        automatic.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        automatic.Error.Should().Contain("reanudación explícita");
        h.Authorization.VerifyNoOtherCalls();

        var manual = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        manual.IsSuccess.Should().BeTrue();
        h.Authorization.Verify(a => a.CheckAsync(CompanyId, AccessKeyValue, It.IsAny<CancellationToken>()), Times.Once);
        h.Reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SRI_TOGGLE_06_habilitada_consulta_automatica_de_actividad_posterior_continua()
    {
        var document = NewReceived();
        var h = new Harness(Enabled(since: DateTime.UtcNow.AddHours(-1)));
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        automatic.IsSuccess.Should().BeTrue();
        h.Authorization.Verify(a => a.CheckAsync(CompanyId, AccessKeyValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SRI_TOGGLE_06_apagado_confirmado_tras_el_control_omite_la_consulta_de_autorizacion()
    {
        var document = NewSigned();
        var h = new Harness(Enabled());
        h.Returns(document);
        // El apagado se confirma después del control inicial (al persistir el intento).
        h.Repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => h.Emission.State = ElectronicEmissionState.Disabled)
            .Returns(Task.CompletedTask);

        await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        h.Authorization.VerifyNoOtherCalls();
        h.Reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SRI_TOGGLE_06_apagado_durante_el_intento_no_cierra_el_pendiente_por_exhaucion()
    {
        var document = NewFailed(failures: 4);
        var h = new Harness(Enabled());
        h.Returns(document);
        // Sin generador de XML el intento falla (5.º fallo = máximo por defecto); el apagado llega durante él.
        h.Suppliers.Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Callback(() => h.Emission.State = ElectronicEmissionState.Disabled)
            .Returns((IElectronicDocumentXmlSupplier?)null);

        await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Failed, "la pausa conserva los estados");
        document.RetryCount.Should().Be(5);
    }

    [Fact]
    public async Task SRI_TOGGLE_06_habilitada_el_mismo_fallo_si_cierra_por_exhaucion()
    {
        var document = NewFailed(failures: 4);
        var h = new Harness(Enabled());
        h.Returns(document);
        h.Suppliers.Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns((IElectronicDocumentXmlSupplier?)null);

        await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
    }

    [Fact]
    public async Task Proceso_automatico_no_reanuda_un_pendiente_anterior_a_la_habilitacion()
    {
        var document = NewFailed(failures: 1);
        var h = new Harness(Enabled(since: DateTime.UtcNow.AddMinutes(1)));
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        automatic.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        automatic.Error.Should().Contain("reanudación explícita");
        h.Suppliers.Verify(r => r.Resolve(It.IsAny<ElectronicDocumentType>()), Times.Never);
    }

    [Fact]
    public async Task Usuario_reanuda_explicitamente_el_mismo_pendiente()
    {
        var document = NewFailed(failures: 1);
        var h = new Harness(Enabled(since: DateTime.UtcNow.AddMinutes(1)));
        h.Returns(document);

        var manual = await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        manual.Code.Should().NotBe(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Suppliers.Verify(r => r.Resolve(ElectronicDocumentType.Invoice), Times.Once, "pasó el control y entró al pipeline");
    }

    [Fact]
    public async Task Proceso_automatico_reintenta_actividad_posterior_a_la_habilitacion()
    {
        var document = NewFailed(failures: 1);
        var h = new Harness(Enabled(since: DateTime.UtcNow.AddHours(-1)));
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        automatic.Code.Should().NotBe(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Suppliers.Verify(r => r.Resolve(ElectronicDocumentType.Invoice), Times.Once);
    }

    [Fact]
    public async Task Apagado_concurrente_detectado_en_el_reclamo_hace_rollback_sin_Signed_ni_envio()
    {
        var guard = new Mock<IElectronicDocumentSourceLifecycleGuard>();
        guard.SetupGet(g => g.SourceModule).Returns("Sales");
        guard.SetupGet(g => g.UsesDispatchProtocol).Returns(false);
        guard
            .Setup(g => g.EvaluateAsync(TenantId, CompanyId, SourceId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElectronicDocumentSourceLifecycle.Allowed);
        var h = new Harness(Enabled(), guard.Object);
        h.PipelineReady();
        h.Repository.Setup(r => r.GetBySourceAsync(TenantId, "Sales", SourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElectronicDocument?)null);
        // El apagado se confirma mientras se genera/firma el XML: el reclamo lo ve bajo FOR SHARE.
        h.UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => h.Emission.State = ElectronicEmissionState.Disabled)
            .Returns(Task.CompletedTask);

        var result = await h.Issuer.RegisterAsync(Register(UserId));

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Added!.CurrentState.Should().Be(ElectronicDocumentState.Draft);
        h.Added.AccessKey.Should().BeNull();
        h.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Reception.VerifyNoOtherCalls();
        h.Emission.Calls.Should().Equal((TenantId, CompanyId, false), (TenantId, CompanyId, true));
    }

    [Fact]
    public async Task SRI_TOGGLE_04_Draft_creado_despues_de_habilitar_con_origen_anterior_no_se_transmite_automaticamente()
    {
        // Draft recién creado (CreatedAt posterior a la habilitación) para un origen autorizado ANTES de ella:
        // decide el instante persistido del origen, nunca la creación del Draft.
        var since = DateTime.UtcNow.AddMinutes(-5);
        var document = NewDraft();
        var h = new Harness(Enabled(since));
        h.Emission.SourceActivityUtc = since.AddMinutes(-1);
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        document.CreatedAt.Should().BeAfter(since);
        automatic.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Suppliers.Verify(r => r.Resolve(It.IsAny<ElectronicDocumentType>()), Times.Never);
        h.Reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SRI_TOGGLE_04_origen_desconocido_no_se_transmite_automaticamente()
    {
        var document = NewDraft();
        var h = new Harness(Enabled());
        h.Emission.SourceActivityUtc = null;
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        automatic.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
    }

    [Fact]
    public async Task SRI_TOGGLE_04_origen_posterior_a_la_habilitacion_se_transmite_automaticamente()
    {
        var since = DateTime.UtcNow.AddMinutes(-5);
        var document = NewDraft();
        var h = new Harness(Enabled(since));
        h.Emission.SourceActivityUtc = since.AddMinutes(1);
        h.Returns(document);

        var automatic = await h.Issuer.RetryAsync(TenantId, document.Id, Guid.Empty);

        automatic.Code.Should().NotBe(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Suppliers.Verify(r => r.Resolve(ElectronicDocumentType.Invoice), Times.Once);
    }

    [Fact]
    public async Task SRI_TOGGLE_04_rehabilitacion_antes_del_reclamo_bloquea_la_recuperacion_automatica()
    {
        var guard = new Mock<IElectronicDocumentSourceLifecycleGuard>();
        guard.SetupGet(g => g.SourceModule).Returns("Sales");
        guard
            .Setup(g => g.EvaluateAsync(TenantId, CompanyId, SourceId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElectronicDocumentSourceLifecycle.Allowed);
        var authorizedAt = DateTime.UtcNow.AddMinutes(-10);
        var h = new Harness(Enabled(since: authorizedAt.AddMinutes(-1)), guard.Object);
        h.Emission.SourceActivityUtc = authorizedAt;
        h.PipelineReady();
        // Re-habilitación (corte nuevo, posterior a la autorización) confirmada antes del reclamo.
        h.UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => h.Emission.State = Enabled(since: DateTime.UtcNow))
            .Returns(Task.CompletedTask);

        var result = await h.Issuer.RegisterAsync(Register(Guid.Empty));

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        h.Added!.CurrentState.Should().Be(ElectronicDocumentState.Draft);
        h.Added.AccessKey.Should().BeNull();
        h.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.Reception.VerifyNoOtherCalls();
        h.Emission.Calls.Select(c => c.LockForShare).Should().Equal(false, true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SRI_TOGGLE_07_todo_reintento_corre_en_el_ambito_SRI_y_siempre_lo_libera(bool enabled)
    {
        var document = NewReceived();
        var h = new Harness(enabled ? Enabled() : ElectronicEmissionState.Disabled);
        h.Returns(document);

        await h.Issuer.RetryAsync(TenantId, document.Id, UserId);

        h.Emission.SriCallsEntered.Should().Equal((TenantId, CompanyId));
        h.Emission.OpenSriCalls.Should().Be(0);
        if (enabled)
            h.Authorization.Verify(a => a.CheckAsync(CompanyId, AccessKeyValue, It.IsAny<CancellationToken>()), Times.Once);
        else
            h.Authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SRI_TOGGLE_07_registrar_corre_el_pipeline_dentro_del_ambito_SRI()
    {
        var h = new Harness(ElectronicEmissionState.Disabled);

        await h.Issuer.RegisterAsync(Register(UserId));

        h.Emission.SriCallsEntered.Should().Equal((TenantId, CompanyId));
        h.Emission.OpenSriCalls.Should().Be(0);
    }

    [Fact]
    public void Regla_de_acceso_al_SRI_automatico_vs_explicito()
    {
        var since = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var state = new ElectronicEmissionState(true, since);

        state.AllowsSriAccess(null, since.AddMinutes(-5), automatic: true).Should().BeFalse();
        state.AllowsSriAccess(since.AddMinutes(1), since.AddMinutes(-5), automatic: true).Should().BeTrue();
        state.AllowsSriAccess(null, since, automatic: true).Should().BeTrue();
        state.AllowsSriAccess(null, null, automatic: true).Should().BeFalse("sin actividad conocida");
        state.AllowsSriAccess(null, since.AddMinutes(-5), automatic: false).Should().BeTrue();
        ElectronicEmissionState.Disabled.AllowsSriAccess(since, since, automatic: false).Should().BeFalse();
    }
}
