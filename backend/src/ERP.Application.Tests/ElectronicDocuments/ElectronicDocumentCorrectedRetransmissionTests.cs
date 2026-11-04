using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static ERP.Application.Tests.ElectronicDocuments.ElectronicDocumentXmlCorrectionComparerTests;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// R12 Fase B — <see cref="ElectronicDocumentIssuer.RetransmitCorrectedAsync"/> sobre el emisor real:
/// elegibilidad, historial SRI, corrección demostrada, reclamo y exactamente una transmisión; las
/// respuestas NO AUTORIZADO históricas nunca cierran el intento nuevo.
/// </summary>
public sealed class ElectronicDocumentCorrectedRetransmissionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime OldNat = new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);
    private const string PreviousSignedPath = "electronic-documents/t/invoice/d/signed.xml";

    private sealed class Harness
    {
        public ElectronicDocument Document { get; }
        public Mock<IElectronicDocumentRepository> Repository { get; } = new();
        public Mock<IElectronicDocumentXmlStorageService> Storage { get; } = new();
        public Mock<IElectronicDocumentReceptionService> Reception { get; } = new();
        public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
        public Mock<IElectronicDocumentSourceLifecycleGuard> Guard { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public List<ElectronicDocumentTransmissionAttempt> Attempts { get; } = [];
        public List<ElectronicDocumentState> StateAtReception { get; } = [];
        public string RegeneratedXml { get; set; } = Invoice(dirMatriz: "Av. Corregida 456", signature: "");
        public string? RegeneratedKey { get; set; }
        public ElectronicDocumentRejectionEvidence? Evidence { get; set; }
        public IReadOnlyList<SriAuthorizationDecision> History { get; set; } = [];
        public ElectronicDocumentIssuer Issuer { get; }

        public Harness(
            ElectronicDocumentType type = ElectronicDocumentType.Invoice,
            bool atReception = false,
            bool dispatchProtocol = false,
            string code = "52"
        )
        {
            var sourceModule = type == ElectronicDocumentType.Retention ? "Retentions" : "Sales";
            Document = ElectronicDocument.Create(Guid.NewGuid(), Guid.NewGuid(), type, sourceModule, Guid.NewGuid(), UserId);
            Document.MarkXmlGenerated("electronic-documents/t/invoice/d/draft.xml", "1.1.0", "1.1.0", UserId);
            Document.SetEnvironment("1");
            Document.MarkSigned(PreviousSignedPath, AccessKey.Create(Key), UserId);
            Document.MarkSent(UserId);
            if (!atReception)
                Document.MarkReceived(UserId);
            Document.MarkRejected($"[{code}] rechazo", UserId);
            Document.ClearDomainEvents();

            Evidence = new(
                atReception ? ElectronicDocumentState.Sent : ElectronicDocumentState.Received,
                DateTime.UtcNow,
                [new SriMessage(code, "ERROR", "rechazo", null)]
            );
            if (!atReception)
                History = [Nat(OldNat, code)];

            Repository.Setup(r => r.GetByIdAsync(Document.TenantId, Document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Document);
            Repository.Setup(r => r.GetRejectionEvidenceAsync(Document.TenantId, Document.Id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Evidence);
            Repository.Setup(r => r.GetTransmissionAttemptByIdempotencyKeyAsync(Document.TenantId, Document.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, Guid key, CancellationToken _) => Attempts.FirstOrDefault(a => a.IdempotencyKey == key));
            Repository.Setup(r => r.GetLatestTransmissionAttemptAsync(Document.TenantId, Document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Attempts.LastOrDefault());
            Repository.Setup(r => r.AddTransmissionAttemptAsync(It.IsAny<ElectronicDocumentTransmissionAttempt>(), It.IsAny<CancellationToken>()))
                .Callback((ElectronicDocumentTransmissionAttempt a, CancellationToken _) => Attempts.Add(a))
                .Returns(Task.CompletedTask);
            Repository.Setup(r => r.GetRetryMaximumSettingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            Storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) =>
                    path == PreviousSignedPath
                        ? Result<byte[]>.Success(Encoding.UTF8.GetBytes(Invoice(signature: "OLD")))
                        : Result<byte[]>.NotFound("no existe"));
            Storage.Setup(s => s.StoreAttemptAsync(Document.TenantId, type, Document.Id, It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, ElectronicDocumentType _, Guid _, Guid attemptId, ElectronicDocumentXml _, SignedElectronicDocumentXml _, CancellationToken _) =>
                    Result<ElectronicDocumentStoredXmlPaths>.Success(new($"attempts/{attemptId:N}/draft.xml", $"attempts/{attemptId:N}/signed.xml")));

            Reception.Setup(r => r.SendAsync(Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => StateAtReception.Add(Document.CurrentState))
                .ReturnsAsync(Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" }));
            Authorization.Setup(a => a.QueryDecisionsAsync(Document.CompanyId, Key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Result<IReadOnlyList<SriAuthorizationDecision>>.Success(History));
            AuthorizeWith(Authorized());

            Guard.SetupGet(g => g.SourceModule).Returns(sourceModule);
            Guard.SetupGet(g => g.UsesDispatchProtocol).Returns(dispatchProtocol);
            Guard.Setup(g => g.EvaluateAsync(Document.TenantId, Document.CompanyId, Document.SourceEntityId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ElectronicDocumentSourceLifecycle.Allowed);

            var supplier = new Mock<IElectronicDocumentXmlSupplier>();
            supplier.SetupGet(s => s.DocumentType).Returns(type);
            supplier.Setup(s => s.BuildXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Result<ElectronicDocumentXml>.Success(
                    new(RegeneratedXml, "UTF-8", "1.1.0", type, "1", RegeneratedKey ?? Key, DateTime.UtcNow)));
            var supplierResolver = new Mock<IElectronicDocumentXmlSupplierResolver>();
            supplierResolver.Setup(r => r.Resolve(type)).Returns(supplier.Object);

            var validator = new Mock<IElectronicDocumentSchemaValidator>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ElectronicDocumentSchemaValidationResult(true, [], [], "1.1.0", type));
            var validatorResolver = new Mock<IElectronicDocumentSchemaValidatorResolver>();
            validatorResolver.Setup(r => r.Resolve(type)).Returns(validator.Object);

            // La firma nueva siempre difiere de la anterior: la comparación debe ignorarla.
            var signing = new Mock<IElectronicDocumentSigningService>();
            signing.Setup(s => s.SignAsync(Document.TenantId, Document.CompanyId, It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, ElectronicDocumentXml x, CancellationToken _) =>
                    Result<SignedElectronicDocumentXml>.Success(new(
                        x.Xml.Replace("<ds:SignatureValue></ds:SignatureValue>", $"<ds:SignatureValue>{Guid.NewGuid():N}</ds:SignatureValue>"),
                        "UTF-8", "1.1.0", type, x.AccessKey, DateTime.UtcNow)));

            var dbEx = new Mock<IDatabaseExceptionTranslator>();
            Issuer = new ElectronicDocumentIssuer(
                Repository.Object,
                supplierResolver.Object,
                validatorResolver.Object,
                signing.Object,
                Storage.Object,
                Reception.Object,
                Authorization.Object,
                dbEx.Object,
                new ElectronicDocumentSourceLifecycleGuardResolver([Guard.Object]),
                ERP.Application.Tests.TestSupport.RetentionElectronicTestDoubles.Emission(),
                UnitOfWork.Object,
                NullLogger<ElectronicDocumentIssuer>.Instance
            );
        }

        public void AuthorizeWith(SriAuthorizationResult result) =>
            Authorization.Setup(a => a.CheckAsync(Document.CompanyId, Key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriAuthorizationResult>.Success(result));

        public Task<Result<ElectronicDocumentDto>> RetransmitAsync(Guid? key = null) =>
            Issuer.RetransmitCorrectedAsync(new(Document.TenantId, Document.Id, "Corrección de dirección matriz", key ?? Guid.NewGuid(), UserId));

        public void NothingTransmitted()
        {
            Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
            Attempts.Should().BeEmpty();
            Document.CurrentState.Should().Be(ElectronicDocumentState.Rejected);
            Document.TransmissionAttempt.Should().Be(1);
            Document.SignedXmlPath.Should().Be(PreviousSignedPath);
        }
    }

    private static SriAuthorizationDecision Nat(DateTime date, string code = "52") =>
        new("NO AUTORIZADO", date, [new SriMessage(code, "ERROR", $"Error {code}", null)]);

    private static SriAuthorizationResult Authorized() =>
        new()
        {
            Status = "AUTORIZADO",
            AuthorizationNumber = Key,
            AuthorizationDate = DateTime.UtcNow,
            Decisions = [new("AUTORIZADO", DateTime.UtcNow, [])],
        };

    private static SriAuthorizationResult NotAuthorized(params SriAuthorizationDecision[] decisions) =>
        new()
        {
            Status = "NO AUTORIZADO",
            ErrorMessage = decisions[0].Describe(),
            StructuredMessages = decisions[0].Messages,
            Decisions = decisions,
        };

    [Fact]
    public async Task Eligible_rejection_with_real_correction_is_claimed_and_transmitted_exactly_once()
    {
        var h = new Harness();

        var result = await h.RetransmitAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        h.Reception.Verify(r => r.SendAsync(h.Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        h.StateAtReception.Should().Equal(ElectronicDocumentState.Signed);
        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        h.Document.AccessKey!.Value.Should().Be(Key);
        h.Document.TransmissionAttempt.Should().Be(2);
        h.Document.SupersededSriDecisionUtc.Should().Be(OldNat);
        h.Document.SignedXmlPath.Should().StartWith("attempts/").And.NotBe(PreviousSignedPath);
        var attempt = h.Attempts.Single();
        attempt.AttemptNumber.Should().Be(2);
        attempt.PreviousSignedXmlPath.Should().Be(PreviousSignedPath);
        attempt.PreviousSignedXmlSha256.Should().HaveLength(64);
        attempt.RejectionStage.Should().Be("Authorization");
        attempt.RejectionCodes.Should().Be("52");
        attempt.CorrectionSummary.Should().Contain("dirMatriz");
        h.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Blocked_code_never_queries_regenerates_nor_transmits()
    {
        var h = new Harness(code: "39");

        var result = await h.RetransmitAsync();

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed);
        h.Authorization.Verify(a => a.QueryDecisionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Storage.Verify(s => s.StoreAttemptAsync(It.IsAny<Guid>(), It.IsAny<ElectronicDocumentType>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()), Times.Never);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Different_access_key_aborts_without_storing_or_transmitting()
    {
        var h = new Harness { RegeneratedKey = new string('9', 49) };

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection);
        result.Error.Should().Contain("clave de acceso distinta");
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Xml_without_correction_aborts_without_storing_or_transmitting()
    {
        var h = new Harness { RegeneratedXml = Invoice(signature: "") };

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection);
        result.Error.Should().Contain("corrección demostrable");
        h.Storage.Verify(s => s.StoreAttemptAsync(It.IsAny<Guid>(), It.IsAny<ElectronicDocumentType>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()), Times.Never);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Commercial_data_change_aborts()
    {
        var h = new Harness { RegeneratedXml = Invoice(dirMatriz: "Nueva", importeTotal: "12.00", signature: "") };

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task New_not_authorized_response_rejects_the_new_attempt_with_its_own_messages()
    {
        var h = new Harness();
        var newNat = Nat(OldNat.AddHours(2), "67");
        h.AuthorizeWith(NotAuthorized(Nat(OldNat), newNat));

        await h.RetransmitAsync();

        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Rejected);
        h.Document.TransmissionAttempt.Should().Be(2);
        h.Document.LastError.Should().Contain("[67]").And.NotContain("[52]");
    }

    [Fact]
    public async Task Historical_not_authorized_response_never_closes_the_new_attempt()
    {
        var h = new Harness();
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));

        var result = await h.RetransmitAsync();

        result.IsSuccess.Should().BeTrue();
        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        h.Document.RequiresSriReconciliation.Should().BeTrue();
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Later_authorization_from_the_query_job_authorizes_without_resending()
    {
        var h = new Harness();
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));
        await h.RetransmitAsync();
        h.AuthorizeWith(Authorized());

        await h.Issuer.RetryAsync(h.Document.TenantId, h.Document.Id, UserId);

        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Previous_authorization_at_sri_blocks()
    {
        var h = new Harness();
        h.History = [Nat(OldNat), new("AUTORIZADO", OldNat.AddMinutes(1), [])];

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked);
        result.Error.Should().Contain("AUTORIZACIÓN");
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Uncertain_sri_history_blocks()
    {
        var h = new Harness();
        h.Authorization.Setup(a => a.QueryDecisionsAsync(h.Document.CompanyId, Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SriAuthorizationDecision>>.Failure("ERROR_CONEXION"));

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Pending_or_undated_sri_decisions_block()
    {
        var pending = new Harness();
        pending.History = [Nat(OldNat), new("EN PROCESO", null, [])];
        (await pending.RetransmitAsync()).Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked);
        pending.NothingTransmitted();

        var undated = new Harness();
        undated.History = [Nat(OldNat), new("NO AUTORIZADO", null, [])];
        (await undated.RetransmitAsync()).Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked);
        undated.NothingTransmitted();
    }

    [Fact]
    public async Task Devuelta_and_not_authorized_are_validated_against_their_own_evidence()
    {
        // DEVUELTA: el SRI no guarda el comprobante — historial vacío, se retransmite.
        var devuelta = new Harness(atReception: true, code: "35");
        (await devuelta.RetransmitAsync()).IsSuccess.Should().BeTrue();
        devuelta.Attempts.Single().RejectionStage.Should().Be("Reception");
        devuelta.Document.SupersededSriDecisionUtc.Should().BeNull();

        // DEVUELTA con una decisión registrada en el SRI: evidencia inconsistente.
        var inconsistent = new Harness(atReception: true, code: "35") { History = [Nat(OldNat)] };
        (await inconsistent.RetransmitAsync()).Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked);
        inconsistent.NothingTransmitted();

        // NO AUTORIZADO sin la decisión en el SRI: evidencia inconsistente.
        var missing = new Harness { History = [] };
        (await missing.RetransmitAsync()).Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked);
        missing.NothingTransmitted();
    }

    [Theory]
    [InlineData("43")]
    [InlineData("45")]
    [InlineData("70")]
    public async Task Already_registered_codes_on_retransmission_only_query(string code)
    {
        var h = new Harness();
        h.Reception.Setup(r => r.SendAsync(h.Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriReceptionResult>.Success(new SriReceptionResult
            {
                Status = "DEVUELTA",
                Errors = [$"[{code}] Clave registrada: x"],
                StructuredMessages = [new SriMessage(code, "ERROR", "Clave registrada", null)],
            }));
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));

        await h.RetransmitAsync();

        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Devuelta_50_or_transport_failure_leaves_the_attempt_uncertain_and_never_resends()
    {
        var h = new Harness();
        h.Reception.Setup(r => r.SendAsync(h.Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriReceptionResult>.Success(new SriReceptionResult
            {
                Status = "DEVUELTA",
                Errors = ["[50] Error interno general: x"],
                StructuredMessages = [new SriMessage("50", "ERROR", "Error interno general", null)],
            }));
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));

        await h.RetransmitAsync();
        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Signed);

        await h.Issuer.RetryAsync(h.Document.TenantId, h.Document.Id, UserId);

        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Authorization.Verify(a => a.CheckAsync(h.Document.CompanyId, Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Same_idempotency_key_never_transmits_twice()
    {
        var h = new Harness();
        var key = Guid.NewGuid();
        await h.RetransmitAsync(key);

        var replay = await h.RetransmitAsync(key);

        replay.IsSuccess.Should().BeTrue();
        h.Attempts.Should().ContainSingle();
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Source_cancelled_under_the_claim_lock_aborts_and_discards_the_new_files()
    {
        var h = new Harness();
        h.Guard.Setup(g => g.EvaluateAsync(h.Document.TenantId, h.Document.CompanyId, h.Document.SourceEntityId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElectronicDocumentSourceLifecycle.Denied("La factura fue anulada."));

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        h.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.Storage.Verify(s => s.DiscardAttemptAsync(It.IsAny<ElectronicDocumentStoredXmlPaths>(), It.IsAny<CancellationToken>()), Times.Once);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Tampered_previous_evidence_blocks()
    {
        var h = new Harness();
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));
        await h.RetransmitAsync();
        h.Document.MarkRejected("[52] otra vez", UserId);
        h.Evidence = new(ElectronicDocumentState.Received, DateTime.UtcNow, [new SriMessage("52", "ERROR", "x", null)]);
        h.History = [Nat(OldNat), Nat(OldNat.AddHours(1))];
        h.Storage.Setup(s => s.ReadAsync(h.Document.SignedXmlPath!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<byte[]>.Success(Encoding.UTF8.GetBytes("<factura>alterado</factura>")));

        var result = await h.RetransmitAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("hash");
        h.Attempts.Should().ContainSingle();
    }

    [Fact]
    public async Task Retention_uses_the_dispatch_claim_and_blocks_a_changed_subject()
    {
        var h = new Harness(ElectronicDocumentType.Retention, dispatchProtocol: true)
        {
            RegeneratedXml = Retention(dir: "Corregida"),
        };
        h.Storage.Setup(s => s.ReadAsync(PreviousSignedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<byte[]>.Success(Encoding.UTF8.GetBytes(Retention())));

        (await h.RetransmitAsync()).IsSuccess.Should().BeTrue();
        h.StateAtReception.Should().Equal(ElectronicDocumentState.Dispatching);

        var changed = new Harness(ElectronicDocumentType.Retention, dispatchProtocol: true)
        {
            RegeneratedXml = Retention(identificacion: "0990000000002", dir: "Corregida"),
        };
        changed.Storage.Setup(s => s.ReadAsync(PreviousSignedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<byte[]>.Success(Encoding.UTF8.GetBytes(Retention())));

        var result = await changed.RetransmitAsync();

        result.Error.Should().Contain("sujeto retenido");
        changed.NothingTransmitted();
    }

    private static Harness CreditNoteHarness(string regeneratedXml)
    {
        var h = new Harness(ElectronicDocumentType.CreditNote) { RegeneratedXml = regeneratedXml };
        h.Storage.Setup(s => s.ReadAsync(PreviousSignedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<byte[]>.Success(Encoding.UTF8.GetBytes(CreditNote(signature: "OLD"))));
        return h;
    }

    [Fact]
    public async Task Credit_note_with_real_correction_is_claimed_and_transmitted_exactly_once()
    {
        var h = CreditNoteHarness(CreditNote(dirMatriz: "Av. Corregida 456", signature: ""));

        var result = await h.RetransmitAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        h.Reception.Verify(r => r.SendAsync(h.Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        h.StateAtReception.Should().Equal(ElectronicDocumentState.Signed);
        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        h.Document.AccessKey!.Value.Should().Be(Key);
        h.Document.TransmissionAttempt.Should().Be(2);
        var attempt = h.Attempts.Single();
        attempt.PreviousSignedXmlPath.Should().Be(PreviousSignedPath);
        attempt.CorrectionSummary.Should().Contain("dirMatriz");
    }

    [Theory]
    [InlineData("<motivo>Devolución<", "<motivo>Otro motivo<")]
    [InlineData("<valorModificacion>11.50<", "<valorModificacion>11.51<")]
    [InlineData("001-001-000000016", "001-001-000000017")]
    public async Task Credit_note_protected_data_change_aborts_without_transmitting(string replace, string with)
    {
        var h = CreditNoteHarness(CreditNote(dirMatriz: "Av. Corregida 456", signature: "", replace: replace, with: with));

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Credit_note_with_cancelled_return_under_the_claim_lock_aborts()
    {
        var h = CreditNoteHarness(CreditNote(dirMatriz: "Av. Corregida 456", signature: ""));
        h.Guard.Setup(g => g.EvaluateAsync(h.Document.TenantId, h.Document.CompanyId, h.Document.SourceEntityId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ElectronicDocumentSourceLifecycle.Denied("La devolución fue anulada."));

        var result = await h.RetransmitAsync();

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        h.Storage.Verify(s => s.DiscardAttemptAsync(It.IsAny<ElectronicDocumentStoredXmlPaths>(), It.IsAny<CancellationToken>()), Times.Once);
        h.NothingTransmitted();
    }

    [Fact]
    public async Task Credit_note_transport_failure_leaves_the_attempt_uncertain_and_never_resends()
    {
        var h = CreditNoteHarness(CreditNote(dirMatriz: "Av. Corregida 456", signature: ""));
        h.Reception.Setup(r => r.SendAsync(h.Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriReceptionResult>.Failure("Timeout del servicio de recepción del SRI."));
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));

        await h.RetransmitAsync();
        await h.Issuer.RetryAsync(h.Document.TenantId, h.Document.Id, UserId);

        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        h.Document.TransmissionAttempt.Should().Be(2);
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Authorization.Verify(a => a.CheckAsync(h.Document.CompanyId, Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Credit_note_same_idempotency_key_never_transmits_twice()
    {
        var h = CreditNoteHarness(CreditNote(dirMatriz: "Av. Corregida 456", signature: ""));
        var key = Guid.NewGuid();
        await h.RetransmitAsync(key);

        (await h.RetransmitAsync(key)).IsSuccess.Should().BeTrue();

        h.Attempts.Should().ContainSingle();
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task R11_window_of_a_retransmission_counts_from_its_own_received()
    {
        var h = new Harness();
        h.AuthorizeWith(NotAuthorized(Nat(OldNat)));
        await h.RetransmitAsync();
        h.Repository.Setup(r => r.GetRetryMaximumSettingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1");
        h.Repository.Setup(r => r.GetFirstReceivedAtUtcAsync(h.Document.TenantId, h.Document.Id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddHours(-1));

        await h.Issuer.RetryAsync(h.Document.TenantId, h.Document.Id, UserId);

        h.Repository.Verify(r => r.GetFirstReceivedAtUtcAsync(
            h.Document.TenantId, h.Document.Id, h.Document.TransmissionAttemptStartedUtc, It.IsAny<CancellationToken>()), Times.Once);
        h.Document.CurrentState.Should().Be(ElectronicDocumentState.Received, "dentro de 24 h desde la RECIBIDA del intento");
    }

    [Fact]
    public async Task Retransmission_claim_is_recorded_in_the_timeline_with_its_attempt_and_reason()
    {
        var audit = new Mock<ERP.Application.Audit.IAuditService>();
        ElectronicDocumentAudit? recorded = null;
        audit.Setup(a => a.RecordAsync(It.IsAny<ElectronicDocumentAudit>(), It.IsAny<CancellationToken>()))
            .Callback((ElectronicDocumentAudit r, CancellationToken _) => recorded = r)
            .Returns(Task.CompletedTask);
        var context = new Mock<ERP.Application.Audit.IAuditContext>();
        var companyId = Guid.NewGuid();
        context.SetupGet(c => c.CompanyId).Returns(companyId);
        context.SetupGet(c => c.Actor).Returns(new ERP.Domain.Audit.AuditActor(
            Guid.NewGuid(), UserId, "user", null, null, null, null, null, ERP.Domain.Audit.AuditSource.UserAction));
        var handler = new ERP.Application.Modules.ElectronicDocuments.EventHandlers.ElectronicDocumentAuditHandler(
            audit.Object, context.Object);

        await handler.Handle(
            new ERP.Domain.Modules.ElectronicDocuments.Events.ElectronicDocumentRetransmissionClaimedEvent(
                Guid.NewGuid(), Guid.NewGuid(), ElectronicDocumentType.Invoice,
                ElectronicDocumentState.Rejected, ElectronicDocumentState.Signed, 2, "Dirección corregida"),
            CancellationToken.None);

        recorded!.Action.Should().Be("RetransmissionClaimed");
        recorded.FromState.Should().Be(ElectronicDocumentState.Rejected);
        recorded.ToState.Should().Be(ElectronicDocumentState.Signed);
        recorded.Reason.Should().Be("Intento 2: Dirección corregida");
        recorded.CompanyId.Should().Be(companyId);
    }

    [Fact]
    public async Task Reception_is_invoked_only_after_the_claim_commit_and_history_query_precedes_the_claim()
    {
        var h = new Harness();
        var order = new List<string>();
        h.Authorization.Setup(a => a.QueryDecisionsAsync(h.Document.CompanyId, Key, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("history"))
            .ReturnsAsync(Result<IReadOnlyList<SriAuthorizationDecision>>.Success([Nat(OldNat)]));
        h.UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("begin")).Returns(Task.CompletedTask);
        h.UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit")).Returns(Task.CompletedTask);
        h.Reception.Setup(r => r.SendAsync(h.Document.CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("reception"))
            .ReturnsAsync(Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" }));

        await h.RetransmitAsync();

        order.Should().Equal("history", "begin", "commit", "reception");
    }

    [Fact]
    public async Task Failed_commit_never_reaches_reception()
    {
        var h = new Harness();
        h.UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit perdido"));

        var act = () => h.RetransmitAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        h.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Undated_or_equal_to_watermark_not_authorized_never_closes_the_new_attempt()
    {
        var undated = new Harness();
        undated.AuthorizeWith(NotAuthorized(Nat(OldNat), new("NO AUTORIZADO", null, [new SriMessage("67", "ERROR", "x", null)])));
        await undated.RetransmitAsync();
        undated.Document.CurrentState.Should().Be(ElectronicDocumentState.Received);

        var sameInstant = new Harness();
        sameInstant.AuthorizeWith(NotAuthorized(Nat(OldNat), Nat(OldNat, "67")));
        await sameInstant.RetransmitAsync();
        sameInstant.Document.CurrentState.Should().Be(ElectronicDocumentState.Received);
    }
}
