using ERP.Application.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.InitialLoad;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// R21/R22 — PostgreSQL 16 + migración completa:
/// las designaciones por resolución se persisten tal cual las normaliza el dominio, una empresa sin
/// designación conserva sus columnas nulas, y la base rechaza un par número/vigencia incompleto
/// aunque se escriba por fuera del dominio.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CompanyFiscalDesignationsPostgreSqlTests
    : IClassFixture<InitialLoadPostgresFixture>,
        IAsyncLifetime
{
    private static long _nextTaxNumber = 1790097000;
    private static readonly DateOnly From = new(2026, 11, 1);
    private readonly InitialLoadPostgresFixture _postgres;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;

    public CompanyFiscalDesignationsPostgreSqlTests(InitialLoadPostgresFixture postgres) =>
        _postgres = postgres;

    private ErpDbContext NewContext()
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        return new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.ConnectionString).Options,
            tenant.Object,
            Mock.Of<IPublisher>(),
            Mock.Of<ICurrentCompany>()
        );
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("R21-R22", "r2122-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        await using var db = NewContext();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<CompanyEntity> NewCompanyAsync(Action<CompanyEntity>? configure = null)
    {
        var company = CompanyEntity.CreateManaged(
            _tenant,
            Interlocked.Increment(ref _nextTaxNumber) + "001",
            "Emisor",
            createdBy: _user
        );
        configure?.Invoke(company);
        await using var db = NewContext();
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private async Task<CompanyEntity> ReloadAsync(Guid id)
    {
        await using var db = NewContext();
        return await db.Companies.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    [Fact]
    public async Task Las_designaciones_se_persisten_normalizadas_y_se_evaluan_por_fecha_tras_recargar()
    {
        var company = await NewCompanyAsync(c =>
            c.UpdateFiscalDesignations(
                "00006498",
                From,
                "NAC-GCFOIOC21-00000868-E",
                From.AddDays(5),
                _user
            )
        );

        var stored = await ReloadAsync(company.Id);

        stored.RetentionAgentResolutionNo.Should().Be("6498");
        stored.RetentionAgentEffectiveFrom.Should().Be(From);
        stored.LargeTaxpayerResolutionNo.Should().Be("NAC-GCFOIOC21-00000868-E");
        stored.LargeTaxpayerEffectiveFrom.Should().Be(From.AddDays(5));
        stored.RetentionAgentResolutionFor(From).Should().Be("6498");
        stored.LargeTaxpayerResolutionFor(From).Should().BeNull();
    }

    [Fact]
    public async Task Una_empresa_sin_designacion_conserva_las_columnas_nulas_y_sus_datos_fiscales()
    {
        var company = await NewCompanyAsync(c =>
            c.UpdateFiscalSettings("01", true, "5368", false, true, true, _user)
        );

        var stored = await ReloadAsync(company.Id);

        stored.RetentionAgentResolutionNo.Should().BeNull();
        stored.RetentionAgentEffectiveFrom.Should().BeNull();
        stored.LargeTaxpayerResolutionNo.Should().BeNull();
        stored.LargeTaxpayerEffectiveFrom.Should().BeNull();
        stored.SpecialTaxpayerNo.Should().Be("5368");
        stored.WithholdsRenta.Should().BeTrue();
        stored.HasValidRetentionAgentDesignation.Should().BeTrue();
    }

    [Fact]
    public async Task Retirar_la_designacion_limpia_ambas_columnas()
    {
        var company = await NewCompanyAsync(c =>
            c.UpdateFiscalDesignations("6498", From, "NAC-1", From, _user)
        );
        await using (var db = NewContext())
        {
            var tracked = await db.Companies.SingleAsync(c => c.Id == company.Id);
            tracked.UpdateFiscalDesignations(null, null, null, null, _user);
            await db.SaveChangesAsync();
        }

        var stored = await ReloadAsync(company.Id);

        stored.RetentionAgentResolutionNo.Should().BeNull();
        stored.LargeTaxpayerEffectiveFrom.Should().BeNull();
    }

    [Theory]
    [InlineData("UPDATE company SET retention_agent_resolution_no = '6498' WHERE id = @id")]
    [InlineData("UPDATE company SET large_taxpayer_effective_from = DATE '2026-11-01' WHERE id = @id")]
    public async Task La_base_rechaza_un_par_numero_vigencia_incompleto(string sql)
    {
        var company = await NewCompanyAsync();
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", company.Id);

        var act = () => command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task El_dominio_impide_guardar_una_designacion_incompleta()
    {
        var company = await NewCompanyAsync();
        await using var db = NewContext();
        var tracked = await db.Companies.SingleAsync(c => c.Id == company.Id);

        var act = () => tracked.UpdateFiscalDesignations("6498", null, null, null, _user);

        act.Should().Throw<DomainRuleViolationException>();
    }
}
