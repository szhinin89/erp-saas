using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-CANCELLATION-ADR-01 — pruebas de CARACTERIZACIÓN (reproducción) del
/// comportamiento ACTUAL de <see cref="ElectronicDocumentIssuer"/> frente a una retención cuya compra
/// origen se anula. Afirman el comportamiento de hoy, no el deseado: documentan la divergencia
/// ERP=Cancelled / SRI=Authorized descrita en ADR-036. Cuando se implemente el gate de ADR-036 estas
/// aserciones deben INVERTIRSE (no borrarse) — p.ej. "SendAsync se invoca" pasa a "nunca se invoca".
///
/// Real: PostgreSQL, ConfirmPurchaseHandler/CancelPurchaseHandler/RetentionCanceller,
/// ElectronicDocumentRepository (xmin) y ElectronicDocumentIssuer. Simulado: solo la frontera SRI
/// (recepción/autorización), el almacenamiento de archivos y, en el caso del pipeline completo, el
/// XML/XSD/firma (el gate de estado de la retención sí es el real: RetentionElectronicDocumentDataProvider).
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    private const string RetentionSourceModule = "Retentions";
    private const string SignedPath = "retentions/signed.xml";

    private async Task<(Guid InvoiceId, Guid RetentionId)> ConfirmWithIssuedRetentionAsync()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoiceId, VatIntent())).IsSuccess.Should().BeTrue();
        var retention = (await ReadAsync(invoiceId)).Retentions.Single();
        retention.Status.Should().Be(RetentionStatus.Issued);
        return (invoiceId, retention.Id);
    }

    private static string NewAccessKey() =>
        string.Concat(Guid.NewGuid().ToByteArray().Select(b => (b % 10).ToString()))
            .PadRight(49, '7')[..49];

    /// <summary>Siembra el ElectronicDocument de la retención en el estado pedido (solo vía transiciones de dominio).</summary>
    private async Task<Guid> SeedRetentionElectronicDocumentAsync(Guid retentionId, ElectronicDocumentState state)
    {
        await using var db = CreateContext();
        var document = ElectronicDocument.Create(
            _tenantId, _companyId, ElectronicDocumentType.Retention, RetentionSourceModule, retentionId, _userId);
        if (state is ElectronicDocumentState.Failed)
            document.MarkFailed("Fallo previo simulado", _userId);
        if (state is ElectronicDocumentState.Signed or ElectronicDocumentState.Sent or ElectronicDocumentState.Received)
        {
            document.MarkXmlGenerated("retentions/draft.xml", "1.0.0", "1.0.0", _userId);
            document.MarkSigned(SignedPath, AccessKey.Create(NewAccessKey()), _userId);
        }
        if (state is ElectronicDocumentState.Sent or ElectronicDocumentState.Received)
            document.MarkSent(_userId);
        if (state is ElectronicDocumentState.Received)
            document.MarkReceived(_userId);
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private async Task<(ElectronicDocumentState State, RetentionStatus RetentionStatus)> ReadElectronicAsync(Guid documentId, Guid retentionId)
    {
        await using var db = CreateContext();
        var document = await db.ElectronicDocuments.AsNoTracking().SingleAsync(d => d.Id == documentId);
        var retention = await db.Set<RetentionDocument>().AsNoTracking().SingleAsync(r => r.Id == retentionId);
        return (document.CurrentState, retention.Status);
    }

    private sealed class SriBoundary
    {
        public Mock<IElectronicDocumentReceptionService> Reception { get; } = new();
        public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
        public Mock<IElectronicDocumentSigningService> Signing { get; } = new();
        public Mock<IElectronicDocumentXmlSupplierResolver> Suppliers { get; } = new();
        public Mock<IElectronicDocumentSchemaValidatorResolver> Validators { get; } = new();
        public Mock<IElectronicDocumentXmlStorageService> Storage { get; } = new();
        public Mock<IFileStorage> Files { get; } = new();

        /// <summary>Si se fija, SendAsync avisa que entró y espera esta señal antes de responder (ventana de carrera).</summary>
        public TaskCompletionSource? HoldSend { get; set; }
        public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SriBoundary(Guid companyId)
        {
            Files.Setup(f => f.GetAsync(SignedPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("<comprobanteRetencion><ds:Signature/></comprobanteRetencion>")));
            Reception.Setup(r => r.SendAsync(companyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    SendEntered.TrySetResult();
                    if (HoldSend is not null)
                        await HoldSend.Task;
                    return Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" });
                });
            Authorization.Setup(a => a.CheckAsync(companyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, string key, CancellationToken _) =>
                    Result<SriAuthorizationResult>.Success(new SriAuthorizationResult
                    {
                        Status = "AUTORIZADO",
                        AuthorizationNumber = key,
                        AuthorizationDate = DateTime.UtcNow,
                    }));
        }
    }

    private ElectronicDocumentIssuer Issuer(ErpDbContext db, SriBoundary sri) =>
        new(
            new ElectronicDocumentRepository(db, new CompanyClock(db)),
            sri.Suppliers.Object,
            sri.Validators.Object,
            sri.Signing.Object,
            sri.Storage.Object,
            sri.Reception.Object,
            sri.Authorization.Object,
            sri.Files.Object,
            new PostgresDatabaseExceptionTranslator(),
            NullLogger<ElectronicDocumentIssuer>.Instance
        );

    private async Task<Result<ElectronicDocumentDto>> RetryAsync(Guid documentId, SriBoundary sri)
    {
        await using var db = CreateContext();
        return await Issuer(db, sri).RetryAsync(_tenantId, documentId, _userId, CancellationToken.None);
    }

    // ── CASO B: la anulación gana primero, el reintento llega después ───────────────────────────

    [Fact]
    public async Task Reproduccion_B_Signed_anulada_la_compra_el_reintento_igual_envia_el_XML_y_queda_Authorized()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedRetentionElectronicDocumentAsync(retentionId, ElectronicDocumentState.Signed);
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue("la anulación del origen no consulta el ElectronicDocument");

        var sri = new SriBoundary(_companyId);
        var retry = await RetryAsync(documentId, sri);

        retry.IsSuccess.Should().BeTrue();
        sri.Reception.Verify(r => r.SendAsync(_companyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once(),
            "HOY: RetryAsync reutiliza el XML firmado sin releer la retención — el XML sale DESPUÉS de Cancelled");
        var state = await ReadElectronicAsync(documentId, retentionId);
        state.RetentionStatus.Should().Be(RetentionStatus.Cancelled);
        state.State.Should().Be(ElectronicDocumentState.Authorized, "HOY: divergencia ERP=Cancelled / SRI=Authorized");
    }

    // ── CASO A: el reintento ya está en vuelo (entre releer el XML y la respuesta SRI) y se anula ──

    [Fact]
    public async Task Reproduccion_A_anulacion_durante_un_reintento_en_vuelo_no_se_bloquea_ni_lo_detiene()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedRetentionElectronicDocumentAsync(retentionId, ElectronicDocumentState.Signed);
        var sri = new SriBoundary(_companyId) { HoldSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };

        var retryTask = RetryAsync(documentId, sri);
        await sri.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var cancel = await CancelPurchaseAsync(invoiceId).WaitAsync(TimeSpan.FromSeconds(30));
        cancel.IsSuccess.Should().BeTrue("HOY: ningún lock/versión compartido entre la anulación del origen y el ElectronicDocument");
        (await ReadElectronicAsync(documentId, retentionId)).RetentionStatus.Should().Be(RetentionStatus.Cancelled);

        sri.HoldSend.SetResult();
        (await retryTask).IsSuccess.Should().BeTrue();

        var state = await ReadElectronicAsync(documentId, retentionId);
        state.State.Should().Be(ElectronicDocumentState.Authorized, "HOY: el reintento completa el envío y la autorización tras la anulación");
        state.RetentionStatus.Should().Be(RetentionStatus.Cancelled);
    }

    // ── CASO C: el reintento termina primero (Authorized), la anulación llega después ───────────

    [Fact]
    public async Task Reproduccion_C_Authorized_la_anulacion_posterior_del_origen_se_acepta_y_no_toca_el_ElectronicDocument()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedRetentionElectronicDocumentAsync(retentionId, ElectronicDocumentState.Signed);
        var sri = new SriBoundary(_companyId);
        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();
        (await ReadElectronicAsync(documentId, retentionId)).State.Should().Be(ElectronicDocumentState.Authorized);

        var cancel = await CancelPurchaseAsync(invoiceId);

        cancel.IsSuccess.Should().BeTrue("HOY: anular el origen no consulta el estado electrónico de la retención");
        var state = await ReadElectronicAsync(documentId, retentionId);
        state.RetentionStatus.Should().Be(RetentionStatus.Cancelled);
        state.State.Should().Be(ElectronicDocumentState.Authorized, "HOY: no hay anulación SRI ni MarkCancelled — anulación local silenciosa");
    }

    // ── CASO D: Sent persistido ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reproduccion_D_Sent_persistido_no_es_reintentable_ni_candidato_del_job()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedRetentionElectronicDocumentAsync(retentionId, ElectronicDocumentState.Sent);
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        var sri = new SriBoundary(_companyId);
        var retry = await RetryAsync(documentId, sri);

        retry.IsSuccess.Should().BeFalse("RetryAsync solo acepta Signed/Received (y Draft/Failed/DeadLetter)");
        sri.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never());
        sri.Authorization.Verify(a => a.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never(),
            "HOY: un Sent persistido no tiene camino de consulta de autorización (queda varado)");
        await using var db = CreateContext();
        var candidates = await new ElectronicDocumentRepository(db, new CompanyClock(db)).GetRetryCandidatesAsync();
        candidates.Should().NotContain(d => d.Id == documentId);
        (await ReadElectronicAsync(documentId, retentionId)).State.Should().Be(ElectronicDocumentState.Sent);
    }

    // ── CASO E: Received — solo consulta autorización, puede llegar Authorized tras la anulación ──

    [Fact]
    public async Task Reproduccion_E_Received_anulada_la_compra_el_reintento_no_reenvia_pero_consulta_y_queda_Authorized()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedRetentionElectronicDocumentAsync(retentionId, ElectronicDocumentState.Received);
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        var sri = new SriBoundary(_companyId);
        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();

        sri.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never(),
            "Received nunca reenvía el XML");
        sri.Authorization.Verify(a => a.CheckAsync(_companyId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        var state = await ReadElectronicAsync(documentId, retentionId);
        state.RetentionStatus.Should().Be(RetentionStatus.Cancelled);
        state.State.Should().Be(ElectronicDocumentState.Authorized, "el SRI ya tenía el comprobante: el resultado llega igual");
    }

    // ── NO ENVIADO: Failed tras la anulación — el gate real del proveedor de datos bloquea el XML ─

    [Fact]
    public async Task Reproduccion_Failed_anulada_la_compra_el_reintento_no_genera_XML_pero_queda_Failed_reintentable()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedRetentionElectronicDocumentAsync(retentionId, ElectronicDocumentState.Failed);
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        await using var db = CreateContext();
        var company = new FixedCurrentCompany(_companyId);
        var docTypes = new Mock<ISriDocTypeCatalogResolver>();
        docTypes.Setup(d => d.IsActiveElectronicDocTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var supplier = new RetentionElectronicDocumentXmlSupplier(
            new RetentionElectronicDocumentXmlService(
                new RetentionElectronicDocumentDataProvider(
                    new RetentionDocumentRepository(db, company),
                    Mock.Of<IEmissionPointRepository>(),
                    Mock.Of<IEstablishmentRepository>(),
                    Mock.Of<ICompanyRepository>(),
                    Mock.Of<ISriSettingsRepository>(),
                    Mock.Of<IBusinessPartnerRepository>(),
                    docTypes.Object
                ),
                Mock.Of<ERP.Application.Modules.ElectronicDocuments.XmlBuilders.IRetentionXmlBuilder>()
            )
        );
        var sri = new SriBoundary(_companyId);
        sri.Suppliers.Setup(s => s.Resolve(ElectronicDocumentType.Retention)).Returns(supplier);

        await Issuer(db, sri).RetryAsync(_tenantId, documentId, _userId, CancellationToken.None);

        sri.Signing.Verify(s => s.SignAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()), Times.Never());
        sri.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never());
        await using var verify = CreateContext();
        var document = await verify.ElectronicDocuments.AsNoTracking().SingleAsync(d => d.Id == documentId);
        document.CurrentState.Should().Be(ElectronicDocumentState.Failed,
            "HOY: el gate existe solo en el proveedor de datos — el documento queda Failed y vuelve a ser candidato del job (ruido hasta DeadLetter)");
        document.LastError.Should().Contain("debe estar emitida");
    }
}
