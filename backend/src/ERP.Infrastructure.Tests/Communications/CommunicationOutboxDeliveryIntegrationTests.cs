using System.Collections.Concurrent;
using System.Net.Mail;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Communications;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — entrega de <c>CommunicationOutbox</c> contra PostgreSQL
/// real: claim atómico entre workers/nodos, fencing por <c>ClaimToken</c>, recuperación de lease,
/// timeout, clasificación y política de reintento, y aislamiento multi-tenant. Sin Hangfire.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CommunicationOutboxDeliveryIntegrationTests
    : IClassFixture<CommunicationOutboxDeliveryIntegrationTests.Database>,
        IAsyncLifetime
{
    private const string Purpose = "SALES_INVOICE_AUTHORIZED";
    private readonly Database _db;
    private readonly MutableTimeProvider _time = new(DateTimeOffset.UtcNow.AddMinutes(1));

    public CommunicationOutboxDeliveryIntegrationTests(Database db) => _db = db;

    // El claim es cross-tenant: cada test parte de una outbox vacía.
    public async Task InitializeAsync()
    {
        await using var ctx = _db.Context();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM communication_outbox_attachments; DELETE FROM communication_outbox;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── 20. Concurrencia: varios workers, cada mensaje una sola vez ─────────────────────

    [Fact]
    public async Task Tres_workers_concurrentes_entregan_cada_mensaje_exactamente_una_vez()
    {
        var ids = await SeedAsync(_db.CompanyA, count: 40);
        var deliveries = new ConcurrentBag<(int Worker, Guid Id)>();

        await Task.WhenAll(Enumerable.Range(1, 3).Select(worker => Task.Run(async () =>
        {
            var sender = new FakeSender(async (msg, ct) =>
            {
                await Task.Delay(5, ct);
                deliveries.Add((worker, msg.CommunicationId!.Value));
            });
            await RunAsync(sender, _db.ConnectionString);
        })));

        deliveries.Select(d => d.Id).Should().BeEquivalentTo(ids, "cada mensaje se entrega una sola vez");
        deliveries.GroupBy(d => d.Id).Should().OnlyContain(g => g.Count() == 1);
        deliveries.Select(d => d.Worker).Distinct().Count().Should().BeGreaterThan(1, "el trabajo se repartió entre workers");
        (await RowsAsync()).Should().OnlyContain(r => r.Status == CommunicationStatus.Sent && r.ClaimToken == null && r.LeaseUntilUtc == null);
    }

    // ── 21. Dos nodos: contextos, pools de conexión y processors independientes ──────────

    [Fact]
    public async Task Dos_nodos_independientes_sobre_el_mismo_PostgreSQL_entregan_una_vez_por_mensaje()
    {
        var ids = await SeedAsync(_db.CompanyA, count: 30);
        var deliveries = new ConcurrentBag<Guid>();
        var sender = new FakeSender(async (msg, ct) =>
        {
            await Task.Delay(5, ct);
            deliveries.Add(msg.CommunicationId!.Value);
        });

        await Task.WhenAll(
            Task.Run(() => RunAsync(sender, _db.ConnectionStringFor("node-a"), new MutableTimeProvider(_time.GetUtcNow()))),
            Task.Run(() => RunAsync(sender, _db.ConnectionStringFor("node-b"), new MutableTimeProvider(_time.GetUtcNow())))
        );

        deliveries.Should().BeEquivalentTo(ids);
        deliveries.Should().OnlyHaveUniqueItems();
    }

    // ── 22. Lease vencido + fencing ───────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_viejo_no_puede_finalizar_un_claim_recuperado_por_otro()
    {
        var id = (await SeedAsync(_db.CompanyA, count: 1)).Single();
        var t0 = _time.Now;

        await using var ctxA = _db.Context();
        await using var ctxB = _db.Context();
        var storeA = new CommunicationOutboxDeliveryStore(ctxA);
        var storeB = new CommunicationOutboxDeliveryStore(ctxB);

        var claimA = await storeA.ClaimNextAsync(t0, CommunicationDeliveryTiming.Lease);
        claimA.Should().NotBeNull();
        claimA!.Recovered.Should().BeFalse();

        (await storeB.ClaimNextAsync(t0.AddMinutes(1), CommunicationDeliveryTiming.Lease))
            .Should().BeNull("el lease de A sigue vigente");

        var claimB = await storeB.ClaimNextAsync(t0 + CommunicationDeliveryTiming.Lease + TimeSpan.FromSeconds(1), CommunicationDeliveryTiming.Lease);
        claimB.Should().NotBeNull();
        claimB!.Id.Should().Be(id);
        claimB.ClaimToken.Should().NotBe(claimA.ClaimToken);
        claimB.Recovered.Should().BeTrue();
        claimB.RetryCount.Should().Be(1, "el intento del worker muerto se cuenta");

        using (JobExecutionContext.Begin(_db.TenantA, _db.CompanyA))
        {
            (await storeA.MarkSentAsync(claimA, EmailDeliveryReceipt.WithoutProviderId, t0)).Should().BeFalse("A perdió el claim");
            (await storeA.MarkFailedAsync(claimA, new(CommunicationStatus.Failed, 9, null), CommunicationFailureCategory.Permanent, "tarde", null, t0))
                .Should().BeFalse();

            var row = await RowAsync(id);
            row.Status.Should().Be(CommunicationStatus.Processing);
            row.ClaimToken.Should().Be(claimB.ClaimToken);

            (await storeB.MarkSentAsync(claimB, EmailDeliveryReceipt.WithoutProviderId, t0)).Should().BeTrue();
        }

        (await RowAsync(id)).Status.Should().Be(CommunicationStatus.Sent);
    }

    // ── 23. Worker muerto ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_muerto_tras_el_claim_se_recupera_al_vencer_el_lease_y_se_entrega()
    {
        var id = (await SeedAsync(_db.CompanyA, count: 1)).Single();
        await using (var dead = _db.Context())
            (await new CommunicationOutboxDeliveryStore(dead).ClaimNextAsync(_time.Now, CommunicationDeliveryTiming.Lease)).Should().NotBeNull();

        var deliveries = new ConcurrentBag<Guid>();
        var sender = new FakeSender((msg, _) => { deliveries.Add(msg.CommunicationId!.Value); return Task.CompletedTask; });

        _time.Advance(TimeSpan.FromMinutes(1));
        await RunAsync(sender, _db.ConnectionString, _time);
        deliveries.Should().BeEmpty("el lease del worker muerto sigue vigente");

        _time.Advance(CommunicationDeliveryTiming.Lease);
        await RunAsync(sender, _db.ConnectionString, _time);

        deliveries.Should().Equal(id);
        var row = await RowAsync(id);
        row.Status.Should().Be(CommunicationStatus.Sent);
        row.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task Recuperacion_con_intentos_agotados_termina_Failed_sin_reenviar()
    {
        var id = (await SeedAsync(_db.CompanyA, count: 1, maxRetries: 1)).Single();
        await using (var dead = _db.Context())
            await new CommunicationOutboxDeliveryStore(dead).ClaimNextAsync(_time.Now, CommunicationDeliveryTiming.Lease);

        var sender = new FakeSender((_, _) => Task.CompletedTask);
        _time.Advance(CommunicationDeliveryTiming.Lease + TimeSpan.FromSeconds(1));
        await RunAsync(sender, _db.ConnectionString, _time);

        sender.Calls.Should().Be(0);
        var row = await RowAsync(id);
        row.Status.Should().Be(CommunicationStatus.Failed);
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Unknown);
        row.ClaimToken.Should().BeNull();
    }

    // ── 24. SMTP lento ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sender_que_excede_el_timeout_queda_Transient_reprogramado_y_no_en_Processing()
    {
        CommunicationDeliveryTiming.MaxSmtpTimeout.Add(CommunicationDeliveryTiming.WorkMargin)
            .Should().BeLessThan(CommunicationDeliveryTiming.Lease, "timeout < lease para cualquier configuración");

        var id = (await SeedAsync(_db.CompanyA, count: 1)).Single();
        var sender = new FakeSender((_, ct) => Task.Delay(Timeout.Infinite, ct));
        var resolver = new ScopedResolver(timeout: TimeSpan.FromMilliseconds(300));

        await RunAsync(sender, _db.ConnectionString, _time, resolver);

        var row = await RowAsync(id);
        row.Status.Should().Be(CommunicationStatus.Pending);
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Transient);
        row.RetryCount.Should().Be(1);
        row.NextAttemptAtUtc.Should().BeCloseTo(_time.Now.AddMinutes(2), TimeSpan.FromSeconds(5));
        row.ClaimToken.Should().BeNull();
        row.LastError.Should().Contain(nameof(TimeoutException));
    }

    // ── 25. Configuración ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SMTP_no_configurado_termina_Failed_Configuration_sin_busy_loop()
    {
        var id = (await SeedAsync(_db.CompanyA, count: 1, maxRetries: 5)).Single();
        var sender = new FakeSender((_, _) => Task.CompletedTask);
        var resolver = new ScopedResolver(canSend: false);

        await RunAsync(sender, _db.ConnectionString, _time, resolver);
        var first = await RowAsync(id);

        _time.Advance(TimeSpan.FromMinutes(10));
        await RunAsync(sender, _db.ConnectionString, _time, resolver);
        _time.Advance(TimeSpan.FromHours(2));
        await RunAsync(sender, _db.ConnectionString, _time, resolver);

        sender.Calls.Should().Be(0);
        resolver.Calls.Should().Be(1, "una fila terminal no vuelve a reclamarse");
        var row = await RowAsync(id);
        row.Status.Should().Be(CommunicationStatus.Failed);
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Configuration);
        row.RetryCount.Should().Be(1, "no se queman los 5 intentos");
        row.Should().BeEquivalentTo(first, o => o.Excluding(r => r.Attachments));
    }

    // ── 26. Permanent / Transient / Unknown / MaxRetries ─────────────────────────────────

    [Fact]
    public async Task Clasificacion_y_politica_de_reintento_por_categoria()
    {
        var permanent = (await SeedAsync(_db.CompanyA, count: 1, subject: "permanent")).Single();
        var transient = (await SeedAsync(_db.CompanyA, count: 1, subject: "transient")).Single();
        var unknown = (await SeedAsync(_db.CompanyA, count: 1, subject: "unknown")).Single();
        var exhausted = (await SeedAsync(_db.CompanyA, count: 1, subject: "exhausted", maxRetries: 1)).Single();

        var sender = new FakeSender((msg, _) => throw (msg.Subject switch
        {
            "permanent" => new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "x@test.com"),
            "unknown" => new InvalidOperationException("no clasificado"),
            _ => (Exception)new SmtpException(SmtpStatusCode.ServiceNotAvailable),
        }));

        await RunAsync(sender, _db.ConnectionString, _time);

        var p = await RowAsync(permanent);
        p.Status.Should().Be(CommunicationStatus.Failed);
        p.FailureCategory.Should().Be(CommunicationFailureCategory.Permanent);

        var t = await RowAsync(transient);
        t.Status.Should().Be(CommunicationStatus.Pending);
        t.FailureCategory.Should().Be(CommunicationFailureCategory.Transient);
        t.NextAttemptAtUtc.Should().BeAfter(_time.Now);

        var u = await RowAsync(unknown);
        u.Status.Should().Be(CommunicationStatus.Pending, "Unknown reintenta, no es Permanent");
        u.FailureCategory.Should().Be(CommunicationFailureCategory.Unknown);

        var e = await RowAsync(exhausted);
        e.Status.Should().Be(CommunicationStatus.Failed, "MaxRetries=1 agotado");
        e.FailureCategory.Should().Be(CommunicationFailureCategory.Transient);

        // El reintento respeta NextAttemptAtUtc: antes no se reclama, después sí y se entrega.
        var retrySender = new FakeSender((_, _) => Task.CompletedTask);
        await RunAsync(retrySender, _db.ConnectionString, _time);
        retrySender.Calls.Should().Be(0);
        _time.Advance(TimeSpan.FromMinutes(3));
        await RunAsync(retrySender, _db.ConnectionString, _time);
        retrySender.Calls.Should().Be(2);
        (await RowAsync(transient)).Status.Should().Be(CommunicationStatus.Sent);
        (await RowAsync(transient)).FailureCategory.Should().BeNull();
    }

    // ── Message-ID inmutable entre reintentos ─────────────────────────────────────────────

    [Fact]
    public async Task Message_ID_no_cambia_entre_reintentos_aunque_cambie_el_remitente_SMTP()
    {
        var id = (await SeedAsync(_db.CompanyA, count: 1)).Single();
        var resolver = new MutableSenderResolver("facturacion@dominio-a.test");
        var messageIds = new List<string>();
        var sender = new MessageIdCapturingSender(messageIds);

        // Intento 1: remitente dominio A; SMTP falla Transient después de armar el mensaje.
        await RunAsync(sender, _db.ConnectionString, _time, resolver);
        (await RowAsync(id)).Status.Should().Be(CommunicationStatus.Pending);

        // Cambia la configuración SMTP y se reintenta LA MISMA comunicación.
        resolver.SenderEmail = "envios@dominio-b.test";
        _time.Advance(TimeSpan.FromMinutes(3));
        await RunAsync(sender, _db.ConnectionString, _time, resolver);

        messageIds.Should().HaveCount(2);
        messageIds[1].Should().Be(messageIds[0], "la identidad del mensaje no depende de la configuración SMTP");
        messageIds[0].Should().Contain(id.ToString("N")).And.NotContain("dominio-a").And.NotContain("dominio-b");
    }

    /// <summary>Arma el MailMessage real (como SmtpEmailSender), captura su Message-ID y falla Transient.</summary>
    private sealed class MessageIdCapturingSender(List<string> messageIds) : IEmailSender
    {
        public Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default)
        {
            using var mail = SmtpEmailSender.BuildMailMessage(message, settings);
            messageIds.Add(mail.Headers["Message-ID"]!);
            throw new SmtpException(SmtpStatusCode.ServiceNotAvailable);
        }
    }

    private sealed class MutableSenderResolver(string senderEmail) : ICommunicationSettingsResolver
    {
        public string SenderEmail { get; set; } = senderEmail;

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) => Resolve();

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default) => Resolve();

        private Task<CommunicationEmailSettings> Resolve() =>
            Task.FromResult(new CommunicationEmailSettings(true, "smtp.test", 587, null, null, SenderEmail, null, true, null, 3, "es"));
    }

    // ── 27. Multi-tenant ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lote_mezclado_de_dos_empresas_usa_la_configuracion_de_cada_una()
    {
        var idsA = await SeedAsync(_db.CompanyA, count: 6);
        var idsB = await SeedAsync(_db.CompanyB, count: 6);
        var used = new ConcurrentDictionary<Guid, (string Sender, Guid ContextCompany)>();
        var sender = new FakeSender((msg, _) =>
        {
            used[msg.CommunicationId!.Value] = (msg.SenderEmailUsed!, JobCompanyContext.Current);
            return Task.CompletedTask;
        });

        await Task.WhenAll(
            Task.Run(() => RunAsync(sender, _db.ConnectionString, new MutableTimeProvider(_time.GetUtcNow()))),
            Task.Run(() => RunAsync(sender, _db.ConnectionString, new MutableTimeProvider(_time.GetUtcNow())))
        );

        used.Keys.Should().BeEquivalentTo(idsA.Concat(idsB));
        foreach (var id in idsA)
            used[id].Should().Be((ScopedResolver.SenderFor(_db.CompanyA), _db.CompanyA));
        foreach (var id in idsB)
            used[id].Should().Be((ScopedResolver.SenderFor(_db.CompanyB), _db.CompanyB));
    }

    // ── 18. Idempotencia de encolado (regresión) ─────────────────────────────────────────

    [Fact]
    public async Task Indice_unico_tenant_company_idempotency_key_sigue_vigente()
    {
        var invoiceId = Guid.NewGuid();
        await SeedAsync(_db.CompanyA, count: 1, sourceId: invoiceId);
        await SeedAsync(_db.CompanyB, count: 1, sourceId: invoiceId); // otra empresa: otra identidad

        // Escritura directa (sin la cola idempotente): el índice único sigue siendo la autoridad.
        var duplicate = () => SeedAsync(_db.CompanyA, count: 1, sourceId: invoiceId);

        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("ux_communication_outbox_idempotency");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private async Task RunAsync(
        IEmailSender sender,
        string connectionString,
        TimeProvider? time = null,
        ICommunicationSettingsResolver? resolver = null
    )
    {
        await using var ctx = _db.Context(connectionString);
        var processor = new CommunicationOutboxProcessor(
            new CommunicationOutboxDeliveryStore(ctx),
            sender,
            NoAttachments.Resolver,
            resolver ?? new ScopedResolver(),
            time ?? _time,
            NullLogger<CommunicationOutboxProcessor>.Instance
        );
        await processor.ProcessPendingAsync();
    }

    private async Task<List<Guid>> SeedAsync(
        Guid companyId,
        int count,
        int maxRetries = 3,
        string? subject = null,
        Guid? sourceId = null
    )
    {
        var tenantId = companyId == _db.CompanyA ? _db.TenantA : _db.TenantB;
        await using var ctx = _db.Context();
        var rows = Enumerable.Range(0, count)
            .Select(i => CommunicationOutbox.CreateEmail(
                CommunicationIdentity.For(
                    CommunicationScope.Company(tenantId, companyId),
                    Purpose,
                    CommunicationChannel.Email,
                    new CommunicationSource("Sales", "SalesInvoice", sourceId ?? Guid.NewGuid()),
                    CommunicationRecipientRole.Customer
                ),
                null, $"cliente{i}@test.com", new CommunicationTemplateUsage(Purpose, 1, CommunicationTemplateSource.Default),
                subject ?? $"msg-{Guid.NewGuid():N}", "<p>x</p>", null,
                CommunicationPriority.Normal, DateTime.UtcNow.AddMinutes(-1), maxRetries, Guid.Empty))
            .ToList();
        ctx.CommunicationOutbox.AddRange(rows);
        await ctx.SaveChangesAsync();
        return rows.Select(r => r.Id).ToList();
    }

    private async Task<CommunicationOutbox> RowAsync(Guid id)
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<List<CommunicationOutbox>> RowsAsync()
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    public sealed class Database : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("erp_communications_delivery_test")
            .Build();

        public Guid TenantA { get; private set; }
        public Guid CompanyA { get; private set; }
        public Guid TenantB { get; private set; }
        public Guid CompanyB { get; private set; }
        public string ConnectionString => _pg.GetConnectionString();

        public string ConnectionStringFor(string applicationName) =>
            new NpgsqlConnectionStringBuilder(ConnectionString) { ApplicationName = applicationName }.ConnectionString;

        public async Task InitializeAsync()
        {
            await _pg.StartAsync();
            await using var ctx = Context();
            await ctx.Database.MigrateAsync();
            var actor = Guid.NewGuid();
            var tenantA = Tenant.Create("A", $"a-{Guid.NewGuid():N}"[..16], actor);
            var tenantB = Tenant.Create("B", $"b-{Guid.NewGuid():N}"[..16], actor);
            ctx.Tenants.AddRange(tenantA, tenantB);
            await ctx.SaveChangesAsync();
            var companyA = Company.CreateManaged(tenantA.Id, "1790012345001", "Empresa A", createdBy: actor);
            var companyB = Company.CreateManaged(tenantB.Id, "1790012346001", "Empresa B", createdBy: actor);
            ctx.Companies.AddRange(companyA, companyB);
            await ctx.SaveChangesAsync();
            (TenantA, CompanyA, TenantB, CompanyB) = (tenantA.Id, companyA.Id, tenantB.Id, companyB.Id);
        }

        public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

        /// <summary>Contexto como en producción: tenant/empresa desde JobExecutionContext (sin HttpContext).</summary>
        public ErpDbContext Context(string? connectionString = null, MediatR.IPublisher? publisher = null)
        {
            var accessor = new HttpContextAccessor();
            var options = new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
                .Options;
            return new ErpDbContext(options, new CurrentTenantService(accessor), publisher ?? new NoPublisher(), new CurrentCompanyService(accessor));
        }
    }

    /// <summary>Sender de prueba: registra cada llamada y el remitente efectivamente usado.</summary>
    private sealed class FakeSender(Func<RecordedMessage, CancellationToken, Task> behavior) : IEmailSender
    {
        private int _calls;
        public int Calls => _calls;

        public async Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            await behavior(new RecordedMessage(message.CommunicationId, message.Subject, settings.SenderEmail), ct);
            return EmailDeliveryReceipt.WithoutProviderId;
        }
    }

    private sealed record RecordedMessage(Guid? CommunicationId, string Subject, string? SenderEmailUsed);

    /// <summary>
    /// Resuelve por el alcance EXPLÍCITO (como el resolver real): remitente por empresa; System usa el
    /// remitente de instancia. El sender verifica además el contexto ambiente abierto por el processor.
    /// </summary>
    private sealed class ScopedResolver(bool canSend = true, TimeSpan? timeout = null) : ICommunicationSettingsResolver
    {
        private int _calls;
        public int Calls => _calls;
        public const string InstanceSender = "instancia@sender.test";

        public static string SenderFor(Guid companyId) => $"{companyId:N}@sender.test";

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("El processor debe resolver con el alcance explícito de la fila.");

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(
                new CommunicationEmailSettings(canSend, "smtp.test", 587, null, null, scope.CompanyId is { } company ? SenderFor(company) : InstanceSender, null, true, null, 3, "es")
                {
                    SmtpTimeout = timeout ?? CommunicationDeliveryTiming.DefaultSmtpTimeout,
                }
            );
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public DateTime Now => _now.UtcDateTime;
        public void Advance(TimeSpan delta) => _now += delta;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class NoPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
