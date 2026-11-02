using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Audit;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.ElectronicDocuments;

/// <summary>
/// R12 Fase B — retransmisión corregida contra PostgreSQL real: reclamo exclusivo bajo el lock de la
/// factura (guard de Fase A) con xmin e índices únicos, idempotencia, aislamiento tenant/empresa, carrera
/// con la anulación de la factura, evidencia versionada en disco con hashes verificables y plazo R11 por
/// intento. Solo se simula la frontera SRI (recepción, autorización, firma y XML del origen).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ElectronicDocumentCorrectedRetransmissionPostgreSqlTests : IAsyncLifetime
{
    private const string SalesSource = "Sales";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);
    private static readonly DateTime OldNat = new(2026, 10, 31, 15, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_corrected_retransmission_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"r12b-{Guid.NewGuid():N}");
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _userId;
    private int _sequence;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _userId = Guid.NewGuid();
        var tenant = Tenant.Create("R12 Fase B", $"r12b-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "R12B S.A.", createdBy: _userId);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, true, _userId, companyId: company.Id
        );
        var customer = BusinessPartner.Create(tenant.Id, "05", "1710034065", 1, "Cliente R12B", _userId);
        var establishment = Establishment.Create(
            tenant.Id, branchId: branch.Id, company.Id, code: "001", name: "Matriz",
            address: "Av. Principal 123", phone: null, isMain: true, createdBy: _userId
        );
        var cashRegister = CashRegister.Create(tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja", _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id, company.Id, establishment.Id, code: "001", name: "PE-001",
            emissionType: EmissionType.Electronic, isDefault: true, createdBy: _userId
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        var cashSession = CashSession.Open(
            tenant.Id, company.Id, branch.Id, _userId, cashRegister.Id, "CAJA-01", "Caja",
            emissionPoint.Id, "001", 0m, _userId
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    // ── 1. Reclamo exclusivo ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dos_solicitudes_concurrentes_producen_exactamente_un_reclamo_y_una_transmision()
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key) { SignBarrier = 2 };

        var first = RetransmitAsync(documentId, sri, Guid.NewGuid());
        var second = RetransmitAsync(documentId, sri, Guid.NewGuid());
        var results = await Task.WhenAll(first, second).WaitAsync(Wait);

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.Conflict);
        sri.SendCalls.Should().Be(1, "como máximo un intento de transmisión por reclamo");
        (await ReadAttemptsAsync(documentId)).Should().ContainSingle().Which.AttemptNumber.Should().Be(2);
        var document = await ReadElectronicAsync(invoiceId);
        document!.TransmissionAttempt.Should().Be(2);
        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        var attempt = (await ReadAttemptsAsync(documentId)).Single();
        Directory.EnumerateFiles(AttemptsDirectory(documentId), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_storageRoot, f).Replace('\\', '/'))
            .Should().BeEquivalentTo(
                [attempt.DraftXmlPath, attempt.SignedXmlPath],
                "los archivos del reclamo perdedor se descartan; solo queda la versión reclamada"
            );
    }

    [Fact]
    public async Task Misma_clave_de_idempotencia_concurrente_o_repetida_no_transmite_dos_veces()
    {
        var (_, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key) { SignBarrier = 2 };
        var idempotencyKey = Guid.NewGuid();

        var results = await Task.WhenAll(
                RetransmitAsync(documentId, sri, idempotencyKey),
                RetransmitAsync(documentId, sri, idempotencyKey)
            )
            .WaitAsync(Wait);
        var replay = await RetransmitAsync(documentId, sri, idempotencyKey);

        results.Should().OnlyContain(r => r.IsSuccess, "el perdedor devuelve el resultado del reclamo ganador");
        replay.IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(1);
        (await ReadAttemptsAsync(documentId)).Should().ContainSingle();
    }

    [Fact]
    public async Task El_indice_unico_impide_registrar_dos_veces_el_mismo_intento()
    {
        var (_, documentId, _) = await SeedRejectedInvoiceAsync();
        await using var db = CreateContext();
        var document = await db.ElectronicDocuments.SingleAsync(d => d.Id == documentId);
        string Hash() => new('a', 64);
        db.ElectronicDocumentTransmissionAttempts.AddRange(
            Enumerable.Range(0, 2).Select(_ => ElectronicDocumentTransmissionAttempt.Create(
                document, 2, Guid.NewGuid(), "Motivo", _userId, "Authorization", ["52"], OldNat,
                "p/signed.xml", Hash(), "n/draft.xml", Hash(), "n/signed.xml", Hash(), "dirMatriz"
            ))
        );

        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("uq_electronic_document_transmission_attempt_number");
    }

    [Theory]
    [InlineData("crash")]
    [InlineData("timeout")]
    public async Task Tras_el_reclamo_confirmado_un_reinicio_del_worker_solo_consulta_y_nunca_reenvia(string failure)
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key) { ReceptionFailure = failure };
        sri.Authorization.Setup(a => a.CheckAsync(_companyId, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Failure("El SRI no respondió."));

        (await RetransmitAsync(documentId, sri, Guid.NewGuid())).IsSuccess.Should().BeTrue();

        var claimed = await ReadElectronicAsync(invoiceId);
        claimed!.CurrentState.Should().Be(ElectronicDocumentState.Signed, "el reclamo quedó confirmado antes de Recepción");
        claimed.TransmissionAttempt.Should().Be(2);
        ElectronicDocumentRetryPolicy.AutomaticRetryCandidate(DateTime.UtcNow.AddHours(2)).Compile()(claimed)
            .Should().BeTrue("el job retoma el Signed tras el reinicio");

        // Worker reiniciado: contenedor de servicios y DbContext nuevos; el job reintenta varias veces.
        for (var i = 0; i < 3; i++)
            (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();
        // Una nueva solicitud explícita tampoco reenvía: ya no está Rejected.
        (await RetransmitAsync(documentId, sri, Guid.NewGuid()))
            .Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed);

        sri.SendCalls.Should().Be(1, "como máximo una transmisión por reclamo, aun con timeout, caída o reinicio");
        sri.Authorization.Verify(a => a.CheckAsync(_companyId, key, It.IsAny<CancellationToken>()), Times.Exactly(3));
        var recovered = await ReadElectronicAsync(invoiceId);
        recovered!.CurrentState.Should().Be(ElectronicDocumentState.Signed, "sin RECIBIDA no hay DeadLetter por plazo (R11, fail-closed)");
        recovered.RequiresSriReconciliation.Should().BeTrue();
        (await ReadAttemptsAsync(documentId)).Should().ContainSingle();

        sri.Authorization.Setup(a => a.CheckAsync(_companyId, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult
            {
                Status = "AUTORIZADO",
                AuthorizationNumber = key,
                AuthorizationDate = DateTime.UtcNow,
                Decisions = [new SriAuthorizationDecision("AUTORIZADO", DateTime.UtcNow, [])],
            }));
        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();
        (await ReadElectronicAsync(invoiceId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        sri.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task La_marca_persistida_descarta_la_NO_AUTORIZADO_historica_releida_tras_el_reinicio()
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key);
        var historical = new SriAuthorizationDecision("NO AUTORIZADO", OldNat, [new SriMessage("52", "ERROR", "X", null)]);
        sri.Authorization.Setup(a => a.CheckAsync(_companyId, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult
            {
                Status = "NO AUTORIZADO",
                ErrorMessage = "[52] X",
                Decisions = [historical],
            }));

        await RetransmitAsync(documentId, sri, Guid.NewGuid());
        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();

        var document = await ReadElectronicAsync(invoiceId);
        document!.SupersededSriDecisionUtc.Should().Be(OldNat);
        document.SupersededSriDecisionUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        document.CurrentState.Should().Be(ElectronicDocumentState.Received, "la NO AUTORIZADO histórica nunca cierra el intento nuevo");
        sri.SendCalls.Should().Be(1);
    }

    // ── 2. Aislamiento tenant / empresa ──────────────────────────────────────────────────────

    [Fact]
    public async Task Otro_tenant_u_otra_empresa_no_ven_ni_retransmiten_el_comprobante()
    {
        var (_, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key);

        var foreignTenant = await RetransmitAsync(documentId, sri, Guid.NewGuid(), tenantId: Guid.NewGuid());
        foreignTenant.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        await using (var db = CreateContext(companyId: Guid.NewGuid()))
        {
            var repository = new ElectronicDocumentRepository(db, new CompanyClock(db));
            (await repository.GetByIdAsync(_tenantId, documentId)).Should().BeNull();
            (await repository.GetRejectionEvidenceAsync(_tenantId, documentId, null)).Should().BeNull();
        }
        await using (var db = CreateContext())
        {
            var repository = new ElectronicDocumentRepository(db, new CompanyClock(db));
            (await repository.GetRejectionEvidenceAsync(Guid.NewGuid(), documentId, null)).Should().BeNull();
            (await repository.GetRejectionEvidenceAsync(_tenantId, documentId, null))!
                .Messages.Should().ContainSingle(m => m.Code == "52");
        }

        sri.SendCalls.Should().Be(0);
        (await ReadAttemptsAsync(documentId)).Should().BeEmpty();
    }

    // ── 3. Carrera con la anulación de la factura ────────────────────────────────────────────

    [Fact]
    public async Task El_reclamo_espera_la_anulacion_en_curso_y_luego_no_transmite()
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key);

        await using var cancelDb = CreateContext();
        var cancelUow = new UnitOfWork(cancelDb);
        await cancelUow.BeginTransactionAsync();
        var locked = await new SalesInvoiceRepository(cancelDb, new FixedCurrentCompany(_companyId))
            .GetByIdForUpdateAsync(_tenantId, invoiceId);

        var retransmit = RetransmitAsync(documentId, sri, Guid.NewGuid());
        await WaitForLockWaiterAsync();
        retransmit.IsCompleted.Should().BeFalse("el reclamo espera el lock de la factura");

        locked!.Cancel("Anulación concurrente", _userId);
        await cancelDb.SaveChangesAsync();
        await cancelUow.CommitAsync();

        var result = await retransmit.WaitAsync(Wait);
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SendCalls.Should().Be(0);
        (await ReadElectronicAsync(invoiceId))!.CurrentState.Should().Be(ElectronicDocumentState.Rejected);
        (await ReadAttemptsAsync(documentId)).Should().BeEmpty();
        Directory.Exists(AttemptsDirectory(documentId)).Should().BeTrue();
        Directory.EnumerateFiles(AttemptsDirectory(documentId), "*", SearchOption.AllDirectories)
            .Should().BeEmpty("los archivos de un reclamo revertido se eliminan");
    }

    [Fact]
    public async Task Reclamada_la_retransmision_la_anulacion_queda_bloqueada_por_resultado_incierto()
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key)
        {
            HoldSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        var retransmit = RetransmitAsync(documentId, sri, Guid.NewGuid());
        await sri.SendEntered.Task.WaitAsync(Wait);

        var cancel = await CancelAsync(invoiceId);
        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Authorized);

        sri.HoldSend.SetResult();
        (await retransmit.WaitAsync(Wait)).IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task Factura_ya_anulada_no_es_elegible()
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        (await CancelAsync(invoiceId)).IsSuccess.Should().BeTrue("Rejected permite anular (Fase A)");
        var sri = new RetransmissionSri(_companyId, key);

        var result = await RetransmitAsync(documentId, sri, Guid.NewGuid());

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed);
        sri.SendCalls.Should().Be(0);
        sri.HistoryCalls.Should().Be(0);
    }

    // ── 4. Evidencia versionada ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_XML_anterior_queda_intacto_y_la_version_nueva_es_independiente_y_verificable()
    {
        var (invoiceId, documentId, key) = await SeedRejectedInvoiceAsync();
        var previousFile = Path.Combine(_storageRoot, SignedRelativePath(documentId));
        var previousBytes = await File.ReadAllBytesAsync(previousFile);
        var sri = new RetransmissionSri(_companyId, key);

        (await RetransmitAsync(documentId, sri, Guid.NewGuid())).IsSuccess.Should().BeTrue();

        (await File.ReadAllBytesAsync(previousFile)).Should().Equal(previousBytes);
        var attempt = (await ReadAttemptsAsync(documentId)).Single();
        attempt.PreviousSignedXmlPath.Should().Be(SignedRelativePath(documentId));
        attempt.PreviousSignedXmlSha256.Should().Be(Sha256(previousBytes));
        attempt.SignedXmlPath.Should().Contain("/attempts/").And.NotBe(attempt.PreviousSignedXmlPath);
        var newSigned = await File.ReadAllBytesAsync(Path.Combine(_storageRoot, attempt.SignedXmlPath));
        var newDraft = await File.ReadAllBytesAsync(Path.Combine(_storageRoot, attempt.DraftXmlPath));
        attempt.SignedXmlSha256.Should().Be(Sha256(newSigned));
        attempt.DraftXmlSha256.Should().Be(Sha256(newDraft));
        Encoding.UTF8.GetString(newSigned).Should().Contain("Av. Corregida 456");
        attempt.CorrectionSummary.Should().Contain("dirMatriz");
        var document = await ReadElectronicAsync(invoiceId);
        document!.SignedXmlPath.Should().Be(attempt.SignedXmlPath);
        document.AccessKey!.Value.Should().Be(key);
    }

    [Fact]
    public async Task Sin_correccion_no_se_almacena_ni_se_transmite_nada()
    {
        var (_, documentId, key) = await SeedRejectedInvoiceAsync();
        var sri = new RetransmissionSri(_companyId, key) { RegeneratedDirMatriz = OriginalDirMatriz };

        var result = await RetransmitAsync(documentId, sri, Guid.NewGuid());

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection);
        sri.SendCalls.Should().Be(0);
        Directory.Exists(AttemptsDirectory(documentId)).Should().BeFalse();
    }

    // ── 5. R11 por intento ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Plazo_R11_se_cuenta_desde_la_RECIBIDA_del_intento()
    {
        var (_, documentId, _) = await SeedRejectedInvoiceAsync();
        var attemptStart = DateTime.UtcNow;
        await using (var db = CreateContext())
        {
            db.Set<ElectronicDocumentAudit>().Add(Audit(documentId, "Received", ElectronicDocumentState.Sent,
                ElectronicDocumentState.Received));
            await db.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var repository = new ElectronicDocumentRepository(read, new CompanyClock(read));
        var original = await repository.GetFirstReceivedAtUtcAsync(_tenantId, documentId);
        var perAttempt = await repository.GetFirstReceivedAtUtcAsync(_tenantId, documentId, attemptStart);

        original.Should().BeBefore(attemptStart, "sin intento la ventana sigue contando desde la primera RECIBIDA");
        perAttempt.Should().BeOnOrAfter(attemptStart);
        (await repository.GetFirstReceivedAtUtcAsync(_tenantId, documentId, DateTime.UtcNow.AddMinutes(1)))
            .Should().BeNull("un intento sin RECIBIDA propia no tiene plazo calculable (fail-closed)");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private const string OriginalDirMatriz = "Av. Principal 123";

    private static string Invoice(string key, string dirMatriz, string signature) =>
        $"""
        <factura id="comprobante" version="1.1.0">
          <infoTributaria><ambiente>1</ambiente><tipoEmision>1</tipoEmision><ruc>1790012345001</ruc>
            <claveAcceso>{key}</claveAcceso><codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi>
            <secuencial>000000001</secuencial><dirMatriz>{dirMatriz}</dirMatriz></infoTributaria>
          <infoFactura><fechaEmision>31/10/2026</fechaEmision><identificacionComprador>1710034065</identificacionComprador>
            <totalSinImpuestos>20.00</totalSinImpuestos><importeTotal>20.00</importeTotal></infoFactura>
          <ds:Signature xmlns:ds="http://www.w3.org/2000/09/xmldsig#"><ds:SignatureValue>{signature}</ds:SignatureValue></ds:Signature>
        </factura>
        """;

    private string AttemptsDirectory(Guid documentId) =>
        Path.Combine(_storageRoot, "electronic-documents", _tenantId.ToString("N"), "invoice",
            documentId.ToString("N"), "attempts");

    private string SignedRelativePath(Guid documentId) =>
        new ElectronicDocumentStorageNamingStrategy().BuildRelativePath(
            _tenantId, ElectronicDocumentType.Invoice, documentId, ElectronicDocumentXmlVariant.Signed);

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private ElectronicDocumentXmlStorageService Storage() =>
        new(
            new LocalFileStorage(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _storageRoot })
                .Build()),
            new ElectronicDocumentStorageNamingStrategy(),
            NullLogger<ElectronicDocumentXmlStorageService>.Instance
        );

    private ElectronicDocumentAudit Audit(Guid documentId, string action, ElectronicDocumentState from, ElectronicDocumentState to) =>
        ElectronicDocumentAudit.Create(
            new AuditActor(_tenantId, _userId, "test", null, null, null, null, null, AuditSource.System),
            _companyId, documentId, action, ElectronicDocumentType.Invoice, from, to);

    /// <summary>Factura autorizada con su comprobante NO AUTORIZADO (52): XML firmado en disco y evidencia en el timeline.</summary>
    private async Task<(Guid InvoiceId, Guid DocumentId, string Key)> SeedRejectedInvoiceAsync()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var key = string.Concat(Guid.NewGuid().ToByteArray().Select(b => (b % 10).ToString())).PadRight(49, '7')[..49];
        await using var db = CreateContext();
        var document = ElectronicDocument.Create(_tenantId, _companyId, ElectronicDocumentType.Invoice, SalesSource, invoiceId, _userId);
        var signedXml = Invoice(key, OriginalDirMatriz, "ORIGINAL");
        var stored = await Storage().StoreAsync(
            _tenantId, ElectronicDocumentType.Invoice, document.Id,
            new ElectronicDocumentXml(signedXml, "UTF-8", "1.1.0", ElectronicDocumentType.Invoice, "1", key, DateTime.UtcNow),
            new SignedElectronicDocumentXml(signedXml, "UTF-8", "1.1.0", ElectronicDocumentType.Invoice, key, DateTime.UtcNow));
        document.SetEnvironment("1");
        document.MarkXmlGenerated(stored.Value!.DraftXmlPath, "1.1.0", "1.1.0", _userId);
        document.MarkSigned(stored.Value.SignedXmlPath, AccessKey.Create(key), _userId);
        document.MarkSent(_userId);
        document.MarkReceived(_userId);
        document.MarkRejected("[52] ERROR EN DIFERENCIAS", _userId);
        db.ElectronicDocuments.Add(document);
        db.Set<ElectronicDocumentAudit>().Add(Audit(document.Id, "Received", ElectronicDocumentState.Sent, ElectronicDocumentState.Received));
        db.Set<ElectronicDocumentAudit>().Add(Audit(document.Id, "Rejected", ElectronicDocumentState.Received, ElectronicDocumentState.Rejected));
        db.ElectronicDocumentSriMessages.Add(ElectronicDocumentSriMessage.Create(
            new AuditActor(_tenantId, _userId, "test", null, null, null, null, null, AuditSource.System),
            _companyId, document.Id, new SriMessage("52", "ERROR", "ERROR EN DIFERENCIAS", null)));
        await db.SaveChangesAsync();
        return (invoiceId, document.Id, key);
    }

    private async Task<Result<ElectronicDocumentDto>> RetransmitAsync(
        Guid documentId,
        RetransmissionSri sri,
        Guid idempotencyKey,
        Guid? tenantId = null
    )
    {
        await using var db = CreateContext();
        var guards = new ElectronicDocumentSourceLifecycleGuardResolver([
            new SalesElectronicSourceLifecycleGuard(
                new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId)),
                new SalesReturnRepository(db, new FixedCurrentCompany(_companyId))),
        ]);
        var issuer = new ElectronicDocumentIssuer(
            new ElectronicDocumentRepository(db, new CompanyClock(db)),
            sri.Suppliers.Object,
            sri.Validators.Object,
            sri.Signing.Object,
            Storage(),
            sri.Reception.Object,
            sri.Authorization.Object,
            new PostgresDatabaseExceptionTranslator(),
            guards,
            ERP.Infrastructure.Tests.TestData.EnabledElectronicEmissionSwitch.Instance,
            new UnitOfWork(db),
            NullLogger<ElectronicDocumentIssuer>.Instance
        );
        return await issuer.RetransmitCorrectedAsync(
            new CorrectedRetransmissionRequest(tenantId ?? _tenantId, documentId, "Corrección de dirección matriz", idempotencyKey, _userId));
    }

    private async Task<Result<ElectronicDocumentDto>> RetryAsync(Guid documentId, RetransmissionSri sri)
    {
        await using var db = CreateContext();
        var guards = new ElectronicDocumentSourceLifecycleGuardResolver([
            new SalesElectronicSourceLifecycleGuard(
                new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId)),
                new SalesReturnRepository(db, new FixedCurrentCompany(_companyId))),
        ]);
        return await new ElectronicDocumentIssuer(
                new ElectronicDocumentRepository(db, new CompanyClock(db)),
                sri.Suppliers.Object, sri.Validators.Object, sri.Signing.Object, Storage(),
                sri.Reception.Object, sri.Authorization.Object, new PostgresDatabaseExceptionTranslator(),
                guards, ERP.Infrastructure.Tests.TestData.EnabledElectronicEmissionSwitch.Instance, new UnitOfWork(db), NullLogger<ElectronicDocumentIssuer>.Instance)
            .RetryAsync(_tenantId, documentId, _userId);
    }

    private async Task<Result<ERP.Application.Modules.Sales.DTOs.SalesInvoiceDto>> CancelAsync(Guid invoiceId)
    {
        var company = new FixedCurrentCompany(_companyId);
        await using var db = CreateContext();
        var edocRepo = new ElectronicDocumentRepository(db, new CompanyClock(db));
        var guards = new ElectronicDocumentSourceLifecycleGuardResolver([
            new SalesElectronicSourceLifecycleGuard(new SalesInvoiceRepository(db, company), new SalesReturnRepository(db, company)),
        ]);
        var branch = new Mock<ICurrentBranch>();
        branch.SetupGet(b => b.BranchId).Returns(_branchId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(_userId);
        var handler = new CancelSalesInvoiceHandler(
            new SalesInvoiceRepository(db, company),
            new SalesReceivableRepository(db, company),
            new SalesReturnRepository(db, company),
            new StockRepository(db, company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance),
            edocRepo,
            new ElectronicDocumentSourceCancellation(edocRepo, guards, new UnitOfWork(db)),
            new FixedCurrentTenant(_tenantId),
            company,
            branch.Object,
            user.Object,
            new CompanyClock(db),
            new UnitOfWork(db)
        );
        return await handler.Handle(new CancelSalesInvoiceCommand(invoiceId, "Motivo R12B"), CancellationToken.None);
    }

    private async Task<Guid> SeedAuthorizedInvoiceAsync()
    {
        await using var db = CreateContext();
        var number = Interlocked.Increment(ref _sequence);
        var invoice = SalesInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _customerId,
            CustomerSnapshot.Create("Cliente R12B", "1710034065", "05"),
            invoiceNumber: $"001-001-{number:D9}",
            issueDate: new DateOnly(2026, 10, 31),
            createdBy: _userId,
            paymentTerm: PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0),
            cashSessionId: _cashSessionId,
            emissionType: EmissionType.Electronic
        );
        invoice.ReplaceLines(
            new[] { SalesInvoiceDetail.Create(invoice.Id, _tenantId, "Servicio R12B", quantity: 2m, unitPrice: 10m, vatCode: "0", uomCode: "UNIT") },
            _userId);
        invoice.ReplacePayments(
            new[] { SalesInvoicePayment.Create(invoice.Id, _tenantId, Guid.NewGuid(), "01", "Efectivo", 20m) },
            _userId);
        invoice.Authorize(_userId);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task WaitForLockWaiterAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'",
                connection);
            if ((long)(await command.ExecuteScalarAsync())! > 0)
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Ninguna sesión quedó esperando el lock de la factura.");
    }

    private async Task<ElectronicDocument?> ReadElectronicAsync(Guid sourceId)
    {
        await using var db = CreateContext();
        return await db.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.SourceModule == SalesSource && d.SourceEntityId == sourceId);
    }

    private async Task<List<ElectronicDocumentTransmissionAttempt>> ReadAttemptsAsync(Guid documentId)
    {
        await using var db = CreateContext();
        return await db.ElectronicDocumentTransmissionAttempts.AsNoTracking()
            .Where(a => a.ElectronicDocumentId == documentId).ToListAsync();
    }

    private async Task<SalesInvoiceStatus> ReadInvoiceStatusAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        return await db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.Status).SingleAsync();
    }

    private ErpDbContext CreateContext(Guid? companyId = null) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId ?? _companyId)
        );

    /// <summary>
    /// Frontera SRI simulada para la retransmisión: XML regenerado con la misma clave (por defecto con la
    /// dirección matriz corregida), firma distinta en cada intento, historial con el NO AUTORIZADO previo.
    /// </summary>
    private sealed class RetransmissionSri
    {
        private int _signers;
        private readonly TaskCompletionSource _signGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Mock<IElectronicDocumentXmlSupplierResolver> Suppliers { get; } = new();
        public Mock<IElectronicDocumentSchemaValidatorResolver> Validators { get; } = new();
        public Mock<IElectronicDocumentSigningService> Signing { get; } = new();
        public Mock<IElectronicDocumentReceptionService> Reception { get; } = new();
        public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
        public string RegeneratedDirMatriz { get; set; } = "Av. Corregida 456";

        /// <summary>Si > 0, cada firma espera a que esa cantidad de solicitudes haya llegado a firmar.</summary>
        public int SignBarrier { get; set; }

        public TaskCompletionSource? HoldSend { get; set; }

        /// <summary>"crash": la llamada aborta (proceso caído); "timeout": fallo de transporte sin respuesta.</summary>
        public string? ReceptionFailure { get; set; }
        public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SendCalls => Reception.Invocations.Count(i => i.Method.Name == nameof(IElectronicDocumentReceptionService.SendAsync));
        public int HistoryCalls => Authorization.Invocations.Count(i => i.Method.Name == nameof(IElectronicDocumentAuthorizationService.QueryDecisionsAsync));

        public RetransmissionSri(Guid companyId, string key)
        {
            var supplier = new Mock<IElectronicDocumentXmlSupplier>();
            supplier.Setup(s => s.BuildXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Result<ElectronicDocumentXml>.Success(new(
                    Invoice(key, RegeneratedDirMatriz, ""), "UTF-8", "1.1.0", ElectronicDocumentType.Invoice, "1", key, DateTime.UtcNow)));
            Suppliers.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(supplier.Object);

            var validator = new Mock<IElectronicDocumentSchemaValidator>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ElectronicDocumentSchemaValidationResult(true, [], [], "1.1.0", ElectronicDocumentType.Invoice));
            Validators.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(validator.Object);

            Signing.Setup(s => s.SignAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid _, Guid _, ElectronicDocumentXml xml, CancellationToken _) =>
                {
                    if (SignBarrier > 0)
                    {
                        if (Interlocked.Increment(ref _signers) >= SignBarrier)
                            _signGate.TrySetResult();
                        await _signGate.Task.WaitAsync(Wait);
                    }
                    return Result<SignedElectronicDocumentXml>.Success(new(
                        xml.Xml.Replace("<ds:SignatureValue></ds:SignatureValue>", $"<ds:SignatureValue>{Guid.NewGuid():N}</ds:SignatureValue>"),
                        "UTF-8", xml.Version, xml.DocumentType, xml.AccessKey, DateTime.UtcNow));
                });

            Reception.Setup(r => r.SendAsync(companyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    SendEntered.TrySetResult();
                    if (HoldSend is not null)
                        await HoldSend.Task;
                    return ReceptionFailure switch
                    {
                        "crash" => throw new OperationCanceledException("Proceso detenido durante la llamada a Recepción."),
                        "timeout" => Result<SriReceptionResult>.Failure("Timeout del servicio de recepción del SRI."),
                        _ => Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" }),
                    };
                });

            Authorization.Setup(a => a.QueryDecisionsAsync(companyId, key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IReadOnlyList<SriAuthorizationDecision>>.Success(
                    [new SriAuthorizationDecision("NO AUTORIZADO", OldNat, [new SriMessage("52", "ERROR", "ERROR EN DIFERENCIAS", null)])]));
            Authorization.Setup(a => a.CheckAsync(companyId, key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult
                {
                    Status = "AUTORIZADO",
                    AuthorizationNumber = key,
                    AuthorizationDate = DateTime.UtcNow,
                    Decisions = [new SriAuthorizationDecision("AUTORIZADO", DateTime.UtcNow, [])],
                }));
        }
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
