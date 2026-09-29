using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.MasterData;

/// <summary>
/// ZH-BP-IDENTIFICATION-UNIQUE-01 — regla real de unicidad de BusinessPartner (ADR-BP-03) contra
/// PostgreSQL con la cadena de migraciones vigente: UNIQUE incondicional
/// <c>uq_mbp_identification (tenant_id, identification_type, identification_number)</c>.
/// BusinessPartner es tenant-scoped (sin company_id) y un mismo BP concentra sus roles
/// (Customer/Supplier/…), así que la identificación no se repite ni aunque el BP esté inactivo.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class BusinessPartnerIdentificationUniqueIndexTests : IAsyncLifetime
{
    private const string MigrationBeforeIndex = "20260926215819_CashFundingRequestFoundation";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_bp_identification_unique_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(_postgres.GetConnectionString(), _tenantA);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private static ErpDbContext CreateContext(string connectionString, Guid tenantId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(connectionString).Options,
            new FixedCurrentTenant(() => tenantId),
            Mock.Of<IPublisher>(),
            new FixedCurrentCompany(() => Guid.Empty)
        );

    private ErpDbContext Db(Guid tenantId) => CreateContext(_postgres.GetConnectionString(), tenantId);

    private BusinessPartner Bp(Guid tenantId, string type, string number, string name) =>
        BusinessPartner.Create(tenantId, type, number, type == "04" ? 2 : 1, name, _actor);

    private static async Task<PostgresException> SaveExpectingUniqueViolation(ErpDbContext db)
    {
        var ex = await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        var pg = ex.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        pg.ConstraintName.Should().Be("uq_mbp_identification");
        return pg;
    }

    [Fact]
    public async Task Indice_existe_con_la_definicion_de_ADR_BP_03()
    {
        await using var db = Db(_tenantA);
        var definition = await db
            .Database.SqlQuery<string>(
                $"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'uq_mbp_identification'"
            )
            .SingleAsync();

        definition.Should().Contain("CREATE UNIQUE INDEX uq_mbp_identification ON public.master_business_partners");
        definition.Should().Contain("(tenant_id, identification_type, identification_number)");
        definition.Should().NotContain("WHERE", "ADR-BP-03: el índice es incondicional");
    }

    [Fact]
    public async Task Misma_identificacion_en_el_mismo_tenant_falla()
    {
        await using var db = Db(_tenantA);
        db.BusinessPartners.Add(Bp(_tenantA, "04", "1790016919001", "Empresa A"));
        await db.SaveChangesAsync();

        db.BusinessPartners.Add(Bp(_tenantA, "04", "1790016919001", "Empresa A duplicada"));

        await SaveExpectingUniqueViolation(db);
    }

    [Fact]
    public async Task Identificacion_de_un_BP_inactivo_tampoco_se_reutiliza()
    {
        await using var db = Db(_tenantA);
        var original = Bp(_tenantA, "05", "1710034065", "Persona Inactiva");
        db.BusinessPartners.Add(original);
        await db.SaveChangesAsync();
        original.Deactivate(_actor);
        await db.SaveChangesAsync();

        db.BusinessPartners.Add(Bp(_tenantA, "05", "1710034065", "Reuso"));

        await SaveExpectingUniqueViolation(db);
    }

    [Fact]
    public async Task Misma_identificacion_en_otro_tenant_esta_permitida_y_aislada()
    {
        await using (var dbA = Db(_tenantA))
        {
            dbA.BusinessPartners.Add(Bp(_tenantA, "04", "1791352688001", "Empresa en A"));
            await dbA.SaveChangesAsync();
        }

        await using (var dbB = Db(_tenantB))
        {
            dbB.BusinessPartners.Add(Bp(_tenantB, "04", "1791352688001", "Empresa en B"));
            await FluentActions.Invoking(() => dbB.SaveChangesAsync()).Should().NotThrowAsync();

            var visibleInB = await dbB.BusinessPartners.AsNoTracking()
                .Where(x => x.Identification.Number == "1791352688001")
                .ToListAsync();
            visibleInB.Should().ContainSingle().Which.TenantId.Should().Be(_tenantB);
        }
    }

    [Fact]
    public async Task Mismo_numero_con_distinto_tipo_de_identificacion_esta_permitido()
    {
        await using var db = Db(_tenantA);
        db.BusinessPartners.Add(Bp(_tenantA, "06", "AB123456", "Pasaporte"));
        db.BusinessPartners.Add(Bp(_tenantA, "08", "AB123456", "Identificación exterior"));

        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Actualizar_identificacion_a_una_ya_usada_falla_y_no_deja_duplicados()
    {
        await using (var db = Db(_tenantA))
        {
            var first = Bp(_tenantA, "05", "1710034073", "Primero");
            var second = Bp(_tenantA, "05", "1710034081", "Segundo");
            db.BusinessPartners.AddRange(first, second);
            await db.SaveChangesAsync();

            second.UpdateIdentification("05", "1710034073", _actor);

            await SaveExpectingUniqueViolation(db);
        }

        await using var check = Db(_tenantA);
        (await check.BusinessPartners.AsNoTracking().CountAsync(x => x.Identification.Number == "1710034073"))
            .Should()
            .Be(1);
        (await check.BusinessPartners.AsNoTracking().CountAsync(x => x.Identification.Number == "1710034081"))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Customer_y_Supplier_comparten_un_unico_BP_y_no_se_crea_un_segundo_por_rol()
    {
        await using var db = Db(_tenantA);
        var bp = Bp(_tenantA, "04", "1791352688005", "Cliente y Proveedor");
        db.BusinessPartners.Add(bp);
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenantA, bp.Id, RoleType.Customer, _actor, customerConfig: CustomerRoleConfig.Create()));
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenantA, bp.Id, RoleType.Supplier, _actor, SupplierRoleConfig.Create()));
        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().NotThrowAsync();

        // Registrar al mismo RUC "como proveedor" en un BP aparte duplicaría la identificación.
        db.BusinessPartners.Add(Bp(_tenantA, "04", "1791352688005", "Mismo RUC como proveedor"));
        await SaveExpectingUniqueViolation(db);
    }

    [Fact]
    public async Task Traductor_reconoce_la_violacion_real_para_que_los_handlers_respondan_Conflict()
    {
        await using var db = Db(_tenantA);
        db.BusinessPartners.Add(Bp(_tenantA, "04", "1790016919002", "Carrera 1"));
        await db.SaveChangesAsync();
        db.BusinessPartners.Add(Bp(_tenantA, "04", "1790016919002", "Carrera 2"));

        var ex = await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();

        new PostgresDatabaseExceptionTranslator().TryGetUniqueViolation(ex.Which, out var info).Should().BeTrue();
        info.ConstraintName.Should().Be("uq_mbp_identification");
    }

    [Fact]
    public async Task Migracion_se_detiene_con_detalle_si_la_BD_ya_tiene_duplicados()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "erp_bp_identification_precheck",
        }.ConnectionString;

        await using var db = CreateContext(connectionString, _tenantA);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeIndex);

        // Estado de una BD que corrió sin el índice: duplicados acumulados.
        db.BusinessPartners.Add(Bp(_tenantA, "04", "1790016919001", "Duplicado 1"));
        db.BusinessPartners.Add(Bp(_tenantA, "04", "1790016919001", "Duplicado 2"));
        await db.SaveChangesAsync();

        var ex = await FluentActions.Invoking(() => migrator.MigrateAsync()).Should().ThrowAsync<PostgresException>();

        ex.Which.MessageText.Should().Contain("uq_mbp_identification: existen BusinessPartners duplicados");
        ex.Which.MessageText.Should().Contain("04/1790016919001 (x2)");
    }
}
