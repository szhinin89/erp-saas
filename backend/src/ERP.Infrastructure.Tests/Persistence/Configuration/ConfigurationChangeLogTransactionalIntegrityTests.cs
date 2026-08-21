using ERP.Application.Common;
using ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Configuration;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Configuration;

/// <summary>
/// CONFIG-FOUNDATION-P2-01 — verificación post-implementación exigida antes de commit: (1)
/// ConfigurationChangeLog.ChangedBy se llena con el usuario real autenticado, no con un valor
/// vacío/hardcodeado; (2) si la inserción del log falla, el cambio crítico de org_settings que
/// audita NO queda aplicado (misma transacción vía un único SaveChangesAsync). Usa el flujo real
/// de producción: UpdateConsumerFinalMaxAmountCommandHandler -> OrgSettingsRepository ->
/// ConfigurationChangeLogger, sobre Postgres real (Testcontainers). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ConfigurationChangeLogTransactionalIntegrityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_config_change_log_tx_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.NewGuid());
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid currentUserId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private UpdateConsumerFinalMaxAmountCommandHandler BuildHandler(
        ErpDbContext db,
        Guid currentUserId,
        out Mock<ISalesFiscalPolicyResolver> resolver
    )
    {
        var repo = new OrgSettingsRepository(db, new ConfigurationChangeLogger(db));
        resolver = new Mock<ISalesFiscalPolicyResolver>();
        resolver
            .Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalesFiscalPolicyResult(true, 0m, ConsumerFinalMaxAmountSource.Manual, null));

        return new UpdateConsumerFinalMaxAmountCommandHandler(
            repo,
            resolver.Object,
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentUser(currentUserId)
        );
    }

    /// <summary>
    /// VERIFICACIÓN 1 — un cambio hecho por un usuario autenticado real (no null, no vacío, no
    /// "system") debe quedar registrado exactamente con ese Guid en ChangedBy. El flujo real:
    /// ICurrentUser (JWT, en producción) -> handler -> OrgSetting.CreatedBy/UpdatedBy ->
    /// OrgSettingsRepository.UpsertAsync -> ConfigurationChangeLogEntry.ChangedBy.
    /// </summary>
    [Fact]
    public async Task Handle_con_usuario_autenticado_real_guarda_ChangedBy_igual_al_usuario_no_vacio()
    {
        var realAuthenticatedUserId = Guid.NewGuid();
        realAuthenticatedUserId.Should().NotBe(Guid.Empty);

        await using (var db = CreateContext(realAuthenticatedUserId))
        {
            var handler = BuildHandler(db, realAuthenticatedUserId, out _);
            var result = await handler.Handle(new UpdateConsumerFinalMaxAmountCommand(250.00m), default);
            result.IsSuccess.Should().BeTrue();
        }

        await using var verifyDb = CreateContext(Guid.NewGuid());
        var log = await verifyDb
            .ConfigurationChangeLogs.IgnoreQueryFilters()
            .SingleAsync(l => l.Key == OrgSettingKeys.Sales.ConsumerFinalMaxAmount);

        log.ChangedBy.Should().Be(realAuthenticatedUserId);
        log.ChangedBy.Should().NotBe(Guid.Empty);
        log.Source.Should().Be(ConfigurationChangeSource.Api);
    }

    /// <summary>
    /// VERIFICACIÓN 2 — si la inserción en configuration_change_log falla, el cambio crítico de
    /// org_settings (RequiresAudit=true) tampoco debe quedar aplicado. Se fuerza un fallo real de
    /// Postgres (DROP TABLE configuration_change_log) para que el INSERT que hace
    /// ConfigurationChangeLogger dentro del mismo SaveChangesAsync del handler falle a nivel de
    /// base de datos — no se toca ni se simula el código del logger, es el camino real de
    /// producción con la tabla de auditoría indisponible.
    /// </summary>
    [Fact]
    public async Task Fallo_al_insertar_el_log_revierte_el_cambio_critico_en_org_settings()
    {
        var user1 = Guid.NewGuid();
        await using (var db = CreateContext(user1))
        {
            var handler = BuildHandler(db, user1, out _);
            var seedResult = await handler.Handle(
                new UpdateConsumerFinalMaxAmountCommand(0.00m),
                default
            );
            seedResult.IsSuccess.Should().BeTrue();
        }

        await using (var breakDb = CreateContext(Guid.NewGuid()))
        {
            await breakDb.Database.ExecuteSqlRawAsync("DROP TABLE configuration_change_log;");
        }

        var user2 = Guid.NewGuid();
        Func<Task> act = async () =>
        {
            await using var db = CreateContext(user2);
            var handler = BuildHandler(db, user2, out _);
            await handler.Handle(new UpdateConsumerFinalMaxAmountCommand(999.99m), default);
        };

        await act.Should()
            .ThrowAsync<Exception>()
            .Where(ex =>
                ex is DbUpdateException
                || ex is PostgresException
                || (ex.InnerException is PostgresException)
            );

        await using var verifyDb = CreateContext(Guid.NewGuid());
        var setting = await verifyDb
            .OrgSettings.IgnoreQueryFilters()
            .SingleAsync(s => s.Key == OrgSettingKeys.Sales.ConsumerFinalMaxAmount);

        setting.Value.Should().Be("0.00");
        setting.Value.Should().NotBe("999.99");
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

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "test.user";
        public string? Email => "test.user@example.com";
        public string? FullName => "Test User";
        public string? Role => "Admin";
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
