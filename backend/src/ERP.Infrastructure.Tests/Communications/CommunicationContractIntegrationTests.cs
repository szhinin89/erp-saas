using System.Collections.Concurrent;
using System.Net.Mail;
using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Infrastructure.Communications;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.Communications;
using ERP.Infrastructure.Persistence.Repositories.Communications;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — contrato de Communications contra PostgreSQL real: alcance
/// System/Company (visibilidad, CHECK, processor de plataforma), encolado idempotente concurrente
/// (también frente a una transacción de negocio abierta) e historial de intentos ligado al claim.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CommunicationContractIntegrationTests
    : IClassFixture<CommunicationOutboxDeliveryIntegrationTests.Database>,
        IAsyncLifetime
{
    private readonly CommunicationOutboxDeliveryIntegrationTests.Database _db;
    private readonly MutableTimeProvider _time = new(DateTimeOffset.UtcNow.AddMinutes(1));

    public CommunicationContractIntegrationTests(CommunicationOutboxDeliveryIntegrationTests.Database db) => _db = db;

    public async Task InitializeAsync()
    {
        await using var ctx = _db.Context();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM communication_outbox_attachments; DELETE FROM communication_outbox;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── 20. Scope ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cada_empresa_ve_solo_lo_suyo_y_ninguna_ve_comunicaciones_System()
    {
        var a = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)));
        var b = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantB, _db.CompanyB)));
        var system = await EnqueueAsync(ResetRequest());

        (await VisibleFromAsync(_db.TenantA, _db.CompanyA)).Should().Equal(a.Id);
        (await VisibleFromAsync(_db.TenantB, _db.CompanyB)).Should().Equal(b.Id);
        (await VisibleFromAsync(Guid.Empty, Guid.Empty)).Should().BeEmpty("sin contexto: fail-closed");

        await using var platform = _db.Context();
        (await platform.CommunicationOutbox.IgnoreQueryFilters().Select(x => x.Id).ToListAsync())
            .Should().BeEquivalentTo([a.Id, b.Id, system.Id], "solo la vía explícita de plataforma ve todo");
    }

    [Fact]
    public async Task Processor_de_plataforma_reclama_y_entrega_System_con_perfil_de_instancia_sin_contexto_de_empresa()
    {
        var system = await EnqueueAsync(ResetRequest());
        var company = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)));
        var deliveries = new ConcurrentDictionary<Guid, (string? Sender, Guid ContextTenant, Guid ContextCompany)>();
        var sender = new RecordingSender((msg, settings) =>
            deliveries[msg.CommunicationId!.Value] = (settings.SenderEmail, JobTenantContext.Current, JobCompanyContext.Current));

        await RunAsync(sender);

        deliveries[system.Id].Should().Be((ScopedResolver.InstanceSender, Guid.Empty, Guid.Empty));
        deliveries[company.Id].Should().Be((ScopedResolver.SenderFor(_db.CompanyA), _db.TenantA, _db.CompanyA));
        (await RowAsync(system.Id)).Status.Should().Be(CommunicationStatus.Sent);
        (await AttemptsAsync(system.Id)).Should().ContainSingle().Which.Result.Should().Be(CommunicationAttemptResult.Sent);
    }

    [Theory]
    [InlineData("System", true, false)]
    [InlineData("System", false, true)]
    [InlineData("Company", false, true)]
    [InlineData("Company", true, false)]
    public async Task Combinacion_invalida_de_alcance_no_persiste(string scopeKind, bool withTenant, bool withCompany)
    {
        await using var ctx = _db.Context();
        var act = () => ctx.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO communication_outbox (id, scope_kind, tenant_id, company_id, channel, purpose, recipient_email, subject,
                body_text, status, priority, scheduled_at_utc, retry_count, max_retries, resend_sequence, created_at, created_by)
            VALUES ({Guid.NewGuid()}, {scopeKind}, {(withTenant ? _db.TenantA : (Guid?)null)}, {(withCompany ? _db.CompanyA : (Guid?)null)},
                'Email', 'P', 'a@b.c', 's', 't', 'Pending', 'Normal', {DateTime.UtcNow}, 0, 3, 0, {DateTime.UtcNow}, {Guid.Empty})
            """
        );

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be(CommunicationOutboxConfiguration.ScopeCheckConstraint);
    }

    // ── 22. Encolado idempotente concurrente ─────────────────────────────────────────────

    [Fact]
    public async Task Encolados_concurrentes_de_la_misma_request_crean_una_sola_fila_sin_excepciones()
    {
        var request = InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA));

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => EnqueueAsync(request))));

        results.Select(r => r.Id).Distinct().Should().ContainSingle("todos reciben la misma comunicación");
        results.Count(r => !r.WasAlreadyQueued).Should().Be(1, "exactamente uno la creó");
        (await CountAsync(results[0].Id)).Should().Be(1);
    }

    [Fact]
    public async Task Colision_dentro_de_una_transaccion_de_negocio_no_la_aborta()
    {
        var request = InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA));
        var existing = await EnqueueAsync(request);

        await using var ctx = _db.Context();
        await using var tx = await ctx.Database.BeginTransactionAsync();
        var duplicate = await QueueFor(ctx).EnqueueAsync(request);
        // La transacción sigue usable: una colisión con ON CONFLICT no la deja en estado abortado.
        (await ctx.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"").ToListAsync()).Should().Equal(1);
        await tx.CommitAsync();

        duplicate.WasAlreadyQueued.Should().BeTrue();
        duplicate.Id.Should().Be(existing.Id);
        (await CountAsync(existing.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Encolado_concurrente_espera_a_la_transaccion_que_tiene_la_misma_identidad(bool commit)
    {
        var request = InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA));
        await using var business = _db.Context();
        await using var tx = await business.Database.BeginTransactionAsync();
        var first = await QueueFor(business).EnqueueAsync(request);

        var competing = Task.Run(() => EnqueueAsync(request));
        await Task.Delay(300);
        competing.IsCompleted.Should().BeFalse("PostgreSQL hace esperar al segundo INSERT hasta que la primera transacción termine");

        if (commit)
            await tx.CommitAsync();
        else
            await tx.RollbackAsync();
        var second = await competing;

        if (commit)
        {
            second.WasAlreadyQueued.Should().BeTrue();
            second.Id.Should().Be(first.Id);
        }
        else
        {
            second.WasAlreadyQueued.Should().BeFalse("la primera revirtió: el segundo encolado crea la comunicación");
            (await CountAsync(second.Id)).Should().Be(1);
        }
    }

    [Fact]
    public async Task Cambio_de_email_no_duplica_la_comunicacion_original()
    {
        var original = InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA));
        var first = await EnqueueAsync(original);

        var second = await EnqueueAsync(original with { RecipientEmail = "otro-email@cliente.com", Template = CommunicationTestTemplates.Invoice("Otro nombre") });

        second.WasAlreadyQueued.Should().BeTrue();
        second.Id.Should().Be(first.Id);
        (await RowAsync(first.Id)).RecipientEmail.Should().Be("cliente@test.com");
    }

    [Fact]
    public async Task Encolado_persiste_adjuntos_con_el_alcance_de_la_comunicacion()
    {
        var request = InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)) with
        {
            Attachments = [new QueueCommunicationAttachmentDto(CommunicationAttachmentType.AuthorizedXml, "f.xml", "application/xml", "edocs/f.xml")],
        };

        var queued = await EnqueueAsync(request);

        await using var ctx = _db.Context();
        var attachment = await ctx.CommunicationOutboxAttachments.IgnoreQueryFilters().SingleAsync(a => a.CommunicationOutboxId == queued.Id);
        attachment.TenantId.Should().Be(_db.TenantA);
        attachment.CompanyId.Should().Be(_db.CompanyA);
        (await RowAsync(queued.Id)).SourceModule.Should().Be("Sales");
    }

    // ── 23. Historial de intentos ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Transient_luego_Sent_conserva_ambos_intentos_y_el_estado_final()
    {
        var queued = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)));
        var fail = true;
        var sender = new RecordingSender((_, _) =>
        {
            if (fail)
                throw new SmtpException(SmtpStatusCode.ServiceNotAvailable);
        }, providerMessageId: "prov-42");

        await RunAsync(sender);
        fail = false;
        _time.Advance(TimeSpan.FromMinutes(3));
        await RunAsync(sender);

        (await RowAsync(queued.Id)).Status.Should().Be(CommunicationStatus.Sent);
        var attempts = await AttemptsAsync(queued.Id);
        attempts.Select(a => a.AttemptNumber).Should().Equal(1, 2);
        attempts[0].Result.Should().Be(CommunicationAttemptResult.Failed);
        attempts[0].FailureCategory.Should().Be(CommunicationFailureCategory.Transient);
        attempts[0].ProviderCode.Should().Be("smtp:421");
        attempts[0].CompletedAtUtc.Should().NotBeNull();
        attempts[1].Result.Should().Be(CommunicationAttemptResult.Sent);
        attempts[1].ProviderMessageId.Should().Be("prov-42", "se guarda el id REAL del proveedor si existe");
        attempts.Should().OnlyContain(a => a.Transport == "smtp" && a.TenantId == _db.TenantA && a.CompanyId == _db.CompanyA);
    }

    [Fact]
    public async Task Worker_muerto_deja_intento_Abandoned_y_la_recuperacion_abre_uno_nuevo()
    {
        var queued = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)));
        await using (var dead = _db.Context())
            (await new CommunicationOutboxDeliveryStore(dead).ClaimNextAsync(_time.Now, CommunicationDeliveryTiming.Lease))!
                .AttemptNumber.Should().Be(1);

        (await AttemptsAsync(queued.Id)).Single().Result.Should().BeNull("intento en curso");

        _time.Advance(CommunicationDeliveryTiming.Lease + TimeSpan.FromSeconds(1));
        await RunAsync(new RecordingSender((_, _) => { }));

        var attempts = await AttemptsAsync(queued.Id);
        attempts.Select(a => (a.AttemptNumber, a.Result)).Should().Equal(
            (1, CommunicationAttemptResult.Abandoned),
            (2, CommunicationAttemptResult.Sent)
        );
        attempts[0].CompletedAtUtc.Should().NotBeNull();
        (await RowAsync(queued.Id)).Status.Should().Be(CommunicationStatus.Sent);
    }

    [Fact]
    public async Task Fencing_perdido_marca_ClaimLost_sin_corromper_estado_ni_historial()
    {
        var queued = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)));
        await using var ctxA = _db.Context();
        await using var ctxB = _db.Context();
        var storeA = new CommunicationOutboxDeliveryStore(ctxA);
        var storeB = new CommunicationOutboxDeliveryStore(ctxB);

        var claimA = (await storeA.ClaimNextAsync(_time.Now, CommunicationDeliveryTiming.Lease))!;
        var claimB = (await storeB.ClaimNextAsync(_time.Now + CommunicationDeliveryTiming.Lease + TimeSpan.FromSeconds(1), CommunicationDeliveryTiming.Lease))!;
        claimB.AttemptNumber.Should().Be(2);

        using (JobExecutionContext.Begin(_db.TenantA, _db.CompanyA))
        {
            (await storeA.MarkSentAsync(claimA, EmailDeliveryReceipt.WithoutProviderId, _time.Now)).Should().BeFalse();
            (await storeB.MarkSentAsync(claimB, EmailDeliveryReceipt.WithoutProviderId, _time.Now)).Should().BeTrue();
        }

        (await RowAsync(queued.Id)).Status.Should().Be(CommunicationStatus.Sent);
        var attempts = await AttemptsAsync(queued.Id);
        attempts.Select(a => (a.AttemptNumber, a.Result)).Should().Equal(
            (1, CommunicationAttemptResult.ClaimLost),
            (2, CommunicationAttemptResult.Sent)
        );
        attempts[0].ErrorSafeText.Should().Contain("después de perder el claim");
    }

    [Fact]
    public async Task El_historial_no_guarda_destinatario_ni_contenido()
    {
        var queued = await EnqueueAsync(InvoiceRequest(CompanyScope(_db.TenantA, _db.CompanyA)));
        var sender = new RecordingSender((msg, _) =>
            throw new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "cliente@test.com"));

        await RunAsync(sender);

        var attempt = (await AttemptsAsync(queued.Id)).Single();
        attempt.FailureCategory.Should().Be(CommunicationFailureCategory.Permanent);
        attempt.ErrorSafeText.Should().Be("SmtpFailedRecipientException (smtp:550)");
        (await RowAsync(queued.Id)).LastError.Should().NotContain("cliente@test.com");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private static CommunicationScope CompanyScope(Guid tenantId, Guid companyId) => CommunicationScope.Company(tenantId, companyId);

    private static readonly Guid InvoiceSourceId = Guid.NewGuid();

    private static CommunicationRequest InvoiceRequest(CommunicationScope scope) =>
        new(
            scope,
            CommunicationPurposes.SalesInvoiceAuthorized,
            new CommunicationSource("Sales", "SalesInvoice", InvoiceSourceId),
            CommunicationRecipientRole.Customer,
            "Cliente",
            "cliente@test.com",
            CommunicationTestTemplates.Invoice(),
            ScheduledAtUtc: DateTime.UtcNow.AddMinutes(-1)
        );

    private static CommunicationRequest ResetRequest() =>
        new(
            CommunicationScope.System,
            CommunicationPurposes.PasswordReset,
            new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()),
            CommunicationRecipientRole.User,
            null,
            "usuario@test.com",
            new CommunicationTestTemplates.PasswordResetModel("Usuario"),
            ScheduledAtUtc: DateTime.UtcNow.AddMinutes(-1)
        );

    private static CommunicationQueue QueueFor(ErpDbContext ctx) =>
        new(
            new CommunicationOutboxRepository(ctx),
            new CurrentCompanyService(new HttpContextAccessor()),
            Mock.Of<ICurrentUser>(u => u.UserId == Guid.Empty),
            new ScopedResolver(),
            new CommunicationTestTemplates.WithStructuralSystemTemplate(
                new CommunicationTemplateResolver(new CommunicationTemplateRepository(ctx))
            ),
            NullLogger<CommunicationQueue>.Instance
        );

    private async Task<QueuedCommunicationDto> EnqueueAsync(CommunicationRequest request)
    {
        await using var ctx = _db.Context();
        return await QueueFor(ctx).EnqueueAsync(request);
    }

    private async Task RunAsync(IEmailSender sender)
    {
        await using var ctx = _db.Context();
        await new CommunicationOutboxProcessor(
            new CommunicationOutboxDeliveryStore(ctx),
            sender,
            new ScopedResolver(),
            _time,
            NullLogger<CommunicationOutboxProcessor>.Instance
        ).ProcessPendingAsync();
    }

    private async Task<List<Guid>> VisibleFromAsync(Guid tenantId, Guid companyId)
    {
        await using var ctx = _db.Context();
        if (tenantId == Guid.Empty)
            return await ctx.CommunicationOutbox.Select(x => x.Id).ToListAsync();

        using var _ = JobExecutionContext.Begin(tenantId, companyId);
        var outbox = await ctx.CommunicationOutbox.Select(x => x.Id).ToListAsync();
        // Adjuntos e intentos comparten el mismo filtro de alcance.
        (await ctx.CommunicationDeliveryAttempts.Where(a => !outbox.Contains(a.CommunicationId)).CountAsync()).Should().Be(0);
        return outbox;
    }

    private async Task<CommunicationOutbox> RowAsync(Guid id)
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<int> CountAsync(Guid id)
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().CountAsync(x => x.Id == id || x.SourceId == InvoiceSourceId);
    }

    private async Task<List<CommunicationDeliveryAttempt>> AttemptsAsync(Guid communicationId)
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationDeliveryAttempts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.CommunicationId == communicationId)
            .OrderBy(a => a.AttemptNumber)
            .ToListAsync();
    }

    private sealed class RecordingSender(Action<EmailMessage, CommunicationEmailSettings> behavior, string? providerMessageId = null) : IEmailSender
    {
        public Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default)
        {
            behavior(message, settings);
            return Task.FromResult(new EmailDeliveryReceipt(providerMessageId));
        }
    }

    /// <summary>Resolver por alcance explícito: perfil por empresa o de instancia (System).</summary>
    private sealed class ScopedResolver : ICommunicationSettingsResolver
    {
        public const string InstanceSender = "instancia@sender.test";

        public static string SenderFor(Guid companyId) => $"{companyId:N}@sender.test";

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("Se exige alcance explícito.");

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default) =>
            Task.FromResult(
                new CommunicationEmailSettings(true, "smtp.test", 587, null, null,
                    scope.CompanyId is { } company ? SenderFor(company) : InstanceSender, null, true, null, 3, "es")
            );
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public DateTime Now => _now.UtcDateTime;
        public void Advance(TimeSpan delta) => _now += delta;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
