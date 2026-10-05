using ERP.API.Tests.Support;
using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01B — pipeline MediatR REAL de la API (behaviors registrados por
/// AddApplication, IUnitOfWork y ErpDbContext reales, PostgreSQL):
/// <list type="bullet">
/// <item>orden efectivo de ejecución (no el de registro): trazas insertadas como el behavior más
/// externo y el más interno;</item>
/// <item>una regla de negocio lanzada tras escribir dentro de la transacción hace rollback ANTES de
/// convertirse en Result (DOMAIN_RULE_VIOLATION) — nada persiste, tampoco el outbox;</item>
/// <item>un rechazo por regla nunca se cachea y la ejecución válida posterior vuelve al handler.</item>
/// </list>
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DomainRulePipelineIntegrationTests : IAsyncLifetime
{
    // ── Sondas (solo existen en este test) ──

    public sealed class Trace
    {
        public ConcurrentQueue<string> Events { get; } = new();
    }

    public sealed record ProbeDeactivateTwiceCommand(Guid PartnerId, Guid UserId)
        : IRequest<Result<bool>>,
            IPlatformScopedRequest;

    /// <summary>Mismo patrón que los handlers transaccionales del ERP (p. ej. CancelPurchaseHandler).</summary>
    public sealed class ProbeDeactivateTwiceHandler(ErpDbContext db, IUnitOfWork uow, Trace trace)
        : IRequestHandler<ProbeDeactivateTwiceCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(ProbeDeactivateTwiceCommand cmd, CancellationToken ct)
        {
            await uow.BeginTransactionAsync(ct);
            try
            {
                var partner = await db.BusinessPartners.IgnoreQueryFilters().SingleAsync(p => p.Id == cmd.PartnerId, ct);
                partner.Deactivate(cmd.UserId); // válido: muta + evento de dominio
                await db.SaveChangesAsync(ct); // flush DENTRO de la transacción (fila + outbox)
                trace.Events.Enqueue("handler:flushed");
                partner.Deactivate(cmd.UserId); // regla: "El BusinessPartner ya está inactivo."
                await uow.CommitAsync(ct);
                return Result<bool>.Success(true);
            }
            catch
            {
                await uow.RollbackAsync(ct);
                trace.Events.Enqueue($"handler:rolledback(active={uow.HasActiveTransaction})");
                throw;
            }
        }
    }

    public sealed class CacheSwitch
    {
        public bool FailNext { get; set; }
        public int Executions;
    }

    public sealed record ProbeCachedQuery(string Key) : IRequest<Result<int>>, ICacheable, IPlatformScopedRequest
    {
        public int CacheTTL => 300;
    }

    public sealed class ProbeCachedHandler(CacheSwitch sw) : IRequestHandler<ProbeCachedQuery, Result<int>>
    {
        public Task<Result<int>> Handle(ProbeCachedQuery request, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref sw.Executions);
            if (sw.FailNext)
                throw new DomainRuleViolationException("Regla de negocio de prueba.");
            return Task.FromResult(Result<int>.Success(n));
        }
    }

    public sealed class OuterTraceBehavior<TRequest, TResponse>(Trace trace) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            if (request is not ProbeDeactivateTwiceCommand)
                return await next(ct);
            trace.Events.Enqueue("outer:enter");
            var response = await next(ct);
            trace.Events.Enqueue(response is Result<bool> r ? $"outer:exit(result:{r.Code})" : "outer:exit");
            return response;
        }
    }

    public sealed class InnerTraceBehavior<TRequest, TResponse>(Trace trace) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            if (request is not ProbeDeactivateTwiceCommand)
                return await next(ct);
            trace.Events.Enqueue("inner:enter");
            try
            {
                return await next(ct);
            }
            catch (Exception ex)
            {
                trace.Events.Enqueue($"inner:exception({ex.GetType().Name})");
                throw;
            }
        }
    }

    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private WebApplicationFactory<Program> _app = null!;
    private Guid _tenantId;
    private Guid _partnerId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = Tenant.Create("ZH-DomainRule", $"zh-dr-{Guid.NewGuid():N}", _userId);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            _tenantId = tenant.Id;
            var partner = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Tercero Activo", _userId);
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            _partnerId = partner.Id;
        }

        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableUser.UserId = _userId;

        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<Trace>();
            services.AddSingleton<CacheSwitch>();
            services.AddTransient<IRequestHandler<ProbeDeactivateTwiceCommand, Result<bool>>, ProbeDeactivateTwiceHandler>();
            services.AddTransient<IRequestHandler<ProbeCachedQuery, Result<int>>, ProbeCachedHandler>();
            // Traza más externa: antes del primer behavior registrado por AddApplication.
            var first = services.ToList().FindIndex(d => d.ServiceType == typeof(IPipelineBehavior<,>));
            services.Insert(first, ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(OuterTraceBehavior<,>)));
            // Traza más interna: después del último (inmediatamente antes del handler).
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(InnerTraceBehavior<,>));
        }));
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Orden_real_del_pipeline_rollback_antes_de_convertir_y_nada_persiste()
    {
        int outboxBefore;
        using (var scope = _app.Services.CreateScope())
            outboxBefore = await scope.ServiceProvider.GetRequiredService<ErpDbContext>().OutboxMessages.CountAsync();

        Result<bool> result;
        IReadOnlyList<Type> registered;
        using (var scope = _app.Services.CreateScope())
        {
            registered = scope.ServiceProvider
                .GetServices<IPipelineBehavior<ProbeDeactivateTwiceCommand, Result<bool>>>()
                .Select(b => b.GetType().GetGenericTypeDefinition())
                .ToList();
            result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new ProbeDeactivateTwiceCommand(_partnerId, _userId));
        }

        // Orden resuelto por el contenedor real (externo → interno).
        registered.Should().Equal(
            typeof(OuterTraceBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(CompanyScopeBehavior<,>),
            typeof(BranchScopeBehavior<,>),
            typeof(DomainRuleBehavior<,>),
            typeof(CachingBehavior<,>),
            typeof(InnerTraceBehavior<,>)
        );

        // Orden EFECTIVO de ejecución: el handler hace rollback, la excepción atraviesa los behaviors
        // internos y DomainRuleBehavior la convierte recién después.
        var trace = _app.Services.GetRequiredService<Trace>().Events.ToArray();
        trace.Should().Equal(
            "outer:enter",
            "inner:enter",
            "handler:flushed",
            "handler:rolledback(active=False)",
            "inner:exception(DomainRuleViolationException)",
            $"outer:exit(result:{ApiResponseCodes.Common.DomainRuleViolation})"
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        result.Error.Should().Be("El BusinessPartner ya está inactivo.");

        // Otra instancia de DbContext: ni la mutación ni el outbox del flush intermedio persisten.
        using var verify = _app.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var partner = await db.BusinessPartners.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == _partnerId);
        partner.IsActive.Should().BeTrue("el flush dentro de la transacción se revirtió");
        (await db.OutboxMessages.CountAsync()).Should().Be(outboxBefore, "el outbox del evento de desactivación no debe quedar parcial");
    }

    [Fact]
    public async Task Rechazo_por_regla_nunca_se_cachea_y_la_ejecucion_valida_vuelve_al_handler()
    {
        var sw = _app.Services.GetRequiredService<CacheSwitch>();
        var query = new ProbeCachedQuery($"k-{Guid.NewGuid():N}");

        async Task<Result<int>> Send()
        {
            using var scope = _app.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(query);
        }

        sw.FailNext = true;
        var rejected = await Send();
        sw.FailNext = false;
        var valid = await Send();
        var cached = await Send();

        rejected.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        valid.IsSuccess.Should().BeTrue("el rechazo no quedó en caché: el handler se ejecutó otra vez");
        sw.Executions.Should().Be(2);
        cached.Value.Should().Be(valid.Value, "la respuesta válida sí se cachea (control)");
        sw.Executions.Should().Be(2, "la tercera llamada sale de caché");
    }
}
