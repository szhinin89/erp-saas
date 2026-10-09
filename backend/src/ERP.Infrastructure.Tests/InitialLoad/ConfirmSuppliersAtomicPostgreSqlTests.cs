using System.Text.Json;
using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Persistence.Repositories.MasterData;
using ERP.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-3B — PostgreSQL 16 + migraciones completas. Comandos MasterData, validadores, repositorios y
/// UoW reales: BP + rol Proveedor (con datos fiscales) + contacto + CompanyBpPurchaseSettings de
/// todo el lote quedan juntos o no queda nada (incluidos actividad y Outbox). Las filas llegan ya
/// validadas (IL-3A); los fallos se provocan con datos que el dominio rechaza o con cambios del
/// maestro posteriores a la validación.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ConfirmSuppliersAtomicPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    private const string CustomerOnlyRuc = "1791352688001";
    private const string SupplierRuc = "1790016919001";
    private const string NewRuc = "0302126842001";
    private static long _nextTaxNumber = 1790097000;
    private readonly ServiceProvider _services;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _contado;
    private Guid _credito;
    private Guid _customerOnly;
    private Guid _existingSupplier;
    private Counts _baseline = null!;

    private sealed record Counts(int Partners, int SupplierRoles, int Contacts, int Settings, int Outbox, int Activities);

    public ConfirmSuppliersAtomicPostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(x => x.CompanyId).Returns(() => _company);
        company.SetupGet(x => x.HasCompanyContext).Returns(() => _company != Guid.Empty);
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(() => _tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(() => _company);
        ctx.SetupGet(x => x.HasTenant).Returns(() => _tenant != Guid.Empty);
        ctx.SetupGet(x => x.HasCompany).Returns(() => _company != Guid.Empty);
        ctx.SetupGet(x => x.UserId).Returns(_user);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(CreateBusinessPartnerCommand).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<CreateBusinessPartnerValidator>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il3b@test" && x.FullName == "IL3B"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<IBusinessPartnerRepository, BusinessPartnerRepository>();
        services.AddScoped<IBusinessPartnerRoleRepository, BusinessPartnerRoleRepository>();
        services.AddScoped<IBusinessPartnerContactRepository, BusinessPartnerContactRepository>();
        services.AddScoped<IBusinessPartnerLocationRepository, BusinessPartnerLocationRepository>();
        services.AddScoped<ICompanyBpPurchaseSettingsRepository, CompanyBpPurchaseSettingsRepository>();
        services.AddScoped<IPaymentTermRepository, PaymentTermRepository>();
        services.AddScoped<ILegalEntityTypeRepository, LegalEntityTypeRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IIdentificationUsageValidator, IdentificationUsageValidator>();
        services.AddScoped<IBusinessPartnerImportLookup, BusinessPartnerImportLookup>();
        services.AddSingleton(Mock.Of<ISupplierImportSheetReader>());
        services.AddScoped<SupplierImportProcessor>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IImportBatchRowRepository, ImportBatchRowRepository>();
        services.AddScoped<IImportBatchIssueRepository, ImportBatchIssueRepository>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Suppliers] = sp.GetRequiredService<SupplierImportProcessor>() });
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL3B", "il3b-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL3B S.A.", createdBy: _user);
        _company = company.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        var contado = PaymentTerm.Create(_tenant, "CONTADO", "Contado", 1, 0, _user);
        var credito = PaymentTerm.Create(_tenant, "CREDITO30", "Crédito 30", 1, 30, _user);
        (_contado, _credito) = (contado.Id, credito.Id);
        db.PaymentTerms.AddRange(contado, credito);
        // Cliente que todavía no es proveedor (caso típico de un supermercado).
        var customerOnly = BusinessPartner.Create(_tenant, "04", CustomerOnlyRuc, null, "Solo Cliente S.A.", _user);
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, customerOnly.Id, RoleType.Customer, _user));
        // Proveedor existente con datos fiscales propios que la carga nunca debe tocar.
        var supplier = BusinessPartner.Create(_tenant, "04", SupplierRuc, null, "Proveedor Previo S.A.", _user);
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, supplier.Id, RoleType.Supplier, _user,
            supplierConfig: SupplierRoleConfig.Create(isRetentionExempt: true, isRequiredToKeepAccounting: true)));
        (_customerOnly, _existingSupplier) = (customerOnly.Id, supplier.Id);
        db.BusinessPartners.AddRange(customerOnly, supplier);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private ParsedSupplierRow NewSupplier(string number, string? email = "compras@proveedor.test",
        bool keepsAccounting = true, bool retentionExempt = false) =>
        number.StartsWith("EXT", StringComparison.OrdinalIgnoreCase)
            ? new("08", number, 2, "Proveedor " + number, null, "US", email, null, _contado,
                keepsAccounting, retentionExempt, PartnerImportAction.Create, null)
            : new("04", number, null, "Proveedor " + number, null, "EC", email, "0999999999", _contado,
                keepsAccounting, retentionExempt, PartnerImportAction.Create, null);

    // Archivo: NO obligado a contabilidad, SÍ exento de retención.
    private ParsedSupplierRow AssignRole() =>
        new("04", CustomerOnlyRuc, null, "Solo Cliente S.A.", null, null, null, null, _contado,
            false, true, PartnerImportAction.AssignRole, _customerOnly);

    // Archivo contradice los datos fiscales del proveedor existente (true/true): no deben aplicarse.
    private ParsedSupplierRow AlreadySupplier(Guid? term = null) =>
        new("04", SupplierRuc, null, "Proveedor Previo S.A.", null, null, null, null, term ?? _contado,
            false, false, PartnerImportAction.AlreadyHasRole, _existingSupplier);

    private async Task<Guid> BatchAsync(params ParsedSupplierRow[] rows)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.Suppliers, _user);
        batch.AttachFile("proveedores.xlsx", "proveedores.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(rows.Length, rows.Length, 0, 0, _user);
        db.ImportBatches.Add(batch);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = ImportBatchRow.Create(_tenant, _company, batch.Id, i + 1, "{}", _user);
            row.SetParsedData(JsonSerializer.Serialize(rows[i]), false, _user);
            db.ImportBatchRows.Add(row);
        }
        await db.SaveChangesAsync();
        _baseline = await CountsAsync();
        return batch.Id;
    }

    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ConfirmImportBatchCommand(batch));
    }

    private async Task<Counts> CountsAsync()
    {
        // Contexto y conexión nuevos: nunca confiar en el tracker de la request confirmada.
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return new Counts(
            await db.BusinessPartners.CountAsync(),
            await db.BusinessPartnerRoles.CountAsync(r => r.RoleType == RoleType.Supplier && r.IsActive),
            await db.BusinessPartnerContacts.CountAsync(),
            await db.CompanyBpPurchaseSettings.CountAsync(),
            await db.OutboxMessages.CountAsync(o => o.TenantId == _tenant),
            await db.UserActivities.CountAsync(a => a.TenantId == _tenant));
    }

    private async Task<SupplierRoleConfig?> SupplierConfigAsync(Guid businessPartnerId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return (await db.BusinessPartnerRoles.AsNoTracking()
            .SingleAsync(r => r.BusinessPartnerId == businessPartnerId && r.RoleType == RoleType.Supplier)).SupplierConfig;
    }

    private async Task AssertRolledBackAsync(Guid batchId)
    {
        (await CountsAsync()).Should().Be(_baseline, "ninguna escritura de ninguna fila sobrevive al fallo");
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = await db.ImportBatches.AsNoTracking().SingleAsync(b => b.Id == batchId);
        batch.Status.Should().Be(ImportStatus.Validated);
        batch.ImportedRows.Should().Be(0);
        (await db.ImportBatchRows.AsNoTracking().Where(r => r.ImportBatchId == batchId)
            .AllAsync(r => r.CreatedBusinessPartnerId == null)).Should().BeTrue();
        (await db.BusinessPartnerRoles.AsNoTracking().AnyAsync(r => r.BusinessPartnerId == _customerOnly
            && r.RoleType == RoleType.Supplier)).Should().BeFalse("el rol asignado a un BP existente también se revierte");
    }

    private async Task SeedSettingsAsync(Guid businessPartnerId, Guid termId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.CompanyBpPurchaseSettings.Add(CompanyBpPurchaseSettings.Create(_tenant, _company, businessPartnerId, termId, _user));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Lote_mixto_confirma_todo_en_una_transaccion_y_preserva_datos_fiscales()
    {
        var batchId = await BatchAsync(NewSupplier(NewRuc), NewSupplier("EXT-0001"), AssignRole(), AlreadySupplier());

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        result.Value.ImportedRows.Should().Be(4);
        var after = await CountsAsync();
        after.Partners.Should().Be(_baseline.Partners + 2, "solo las identificaciones nuevas crean BP");
        after.SupplierRoles.Should().Be(_baseline.SupplierRoles + 3);
        after.Contacts.Should().Be(_baseline.Contacts + 2);
        after.Settings.Should().Be(_baseline.Settings + 4);
        after.Outbox.Should().BeGreaterThan(_baseline.Outbox);
        after.Activities.Should().BeGreaterThan(_baseline.Activities);

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.CompanyBpPurchaseSettings.AsNoTracking().Where(s => s.CompanyId == _company)
            .AllAsync(s => s.PaymentTermId == _contado)).Should().BeTrue();
        var created = await db.BusinessPartners.AsNoTracking().SingleAsync(b => b.Identification.Number == NewRuc);
        var createdConfig = await SupplierConfigAsync(created.Id);
        createdConfig!.IsRequiredToKeepAccounting.Should().BeTrue();
        createdConfig.IsRetentionExempt.Should().BeFalse();
        var assigned = await SupplierConfigAsync(_customerOnly);
        assigned!.IsRequiredToKeepAccounting.Should().BeFalse("SI/NO del archivo al asignar el rol");
        assigned.IsRetentionExempt.Should().BeTrue();
        var kept = await SupplierConfigAsync(_existingSupplier);
        kept!.IsRequiredToKeepAccounting.Should().BeTrue("un proveedor existente conserva sus datos fiscales");
        kept.IsRetentionExempt.Should().BeTrue();
    }

    [Fact]
    public async Task Contacto_invalido_en_fila_intermedia_revierte_todo_el_lote()
    {
        var batchId = await BatchAsync(NewSupplier("EXT-0101"), AssignRole(),
            NewSupplier("EXT-0102", email: "no-es-email"), NewSupplier("EXT-0103"));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 3").And.Contain("No se importó ningún proveedor")
            .And.NotContain("Error interno");
        await AssertRolledBackAsync(batchId);
    }

    [Fact]
    public async Task Bp_existente_sin_rol_se_reutiliza_sin_duplicar()
    {
        var batchId = await BatchAsync(AssignRole());

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var after = await CountsAsync();
        after.Partners.Should().Be(_baseline.Partners);
        after.SupplierRoles.Should().Be(_baseline.SupplierRoles + 1);
        after.Settings.Should().Be(_baseline.Settings + 1);
        after.Contacts.Should().Be(_baseline.Contacts);
    }

    [Fact]
    public async Task Proveedor_existente_con_la_misma_condicion_es_idempotente()
    {
        await SeedSettingsAsync(_existingSupplier, _contado);
        var batchId = await BatchAsync(AlreadySupplier());

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        (await CountsAsync()).Should().Be(_baseline, "un proveedor ya configurado no genera escrituras de maestro");
        var kept = await SupplierConfigAsync(_existingSupplier);
        kept!.IsRequiredToKeepAccounting.Should().BeTrue();
        kept.IsRetentionExempt.Should().BeTrue();
    }

    [Fact]
    public async Task Condicion_de_pago_conflictiva_revierte_todo_el_lote()
    {
        // La validación vio al proveedor sin condición; luego alguien le asignó otra en esta empresa.
        var batchId = await BatchAsync(NewSupplier("EXT-0201"), AssignRole(), AlreadySupplier());
        await SeedSettingsAsync(_existingSupplier, _credito);
        _baseline = await CountsAsync();

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("otra condición de pago").And.Contain("Vuelva a validar");
        await AssertRolledBackAsync(batchId);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.CompanyBpPurchaseSettings.AsNoTracking().SingleAsync(s => s.BusinessPartnerId == _existingSupplier))
            .PaymentTermId.Should().Be(_credito, "la condición existente nunca se sobrescribe");
    }

    [Fact]
    public async Task Tercero_creado_despues_de_validar_no_se_duplica()
    {
        var batchId = await BatchAsync(NewSupplier("EXT-0301"), NewSupplier("EXT-0302"));
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.BusinessPartners.Add(BusinessPartner.Create(_tenant, "08", "ext-0302", 2, "Creado Manual", _user));
            await db.SaveChangesAsync();
        }
        _baseline = await CountsAsync();

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya existe");
        await AssertRolledBackAsync(batchId);
    }

    // ── Rol Proveedor revocado ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedRevokedSupplierAsync(string ruc, SupplierRoleConfig? config)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var bp = BusinessPartner.Create(_tenant, "04", ruc, null, "Ex Proveedor " + ruc, _user);
        var role = BusinessPartnerRole.Create(_tenant, bp.Id, RoleType.Supplier, _user, supplierConfig: config);
        role.Revoke(_user);
        db.BusinessPartners.Add(bp);
        db.BusinessPartnerRoles.Add(role);
        await db.SaveChangesAsync();
        return bp.Id;
    }

    private ParsedSupplierRow Reactivate(string ruc, Guid bpId) =>
        // Archivo: NO/NO — contradice los datos previos; nunca deben aplicarse.
        new("04", ruc, null, "Ex Proveedor " + ruc, null, null, null, null, _contado,
            false, false, PartnerImportAction.ReactivateRole, bpId);

    [Fact]
    public async Task Lookup_distingue_rol_revocado_con_y_sin_datos_fiscales()
    {
        await SeedRevokedSupplierAsync(NewRuc, SupplierRoleConfig.Create(isRetentionExempt: true, isRequiredToKeepAccounting: true));
        await SeedRevokedSupplierAsync("1710034065001", null);
        await using var scope = _services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IBusinessPartnerImportLookup>();

        var withData = await lookup.FindByIdentificationAsync("04", NewRuc, RoleType.Supplier, default);
        var withoutData = await lookup.FindByIdentificationAsync("04", "1710034065001", RoleType.Supplier, default);
        var neverSupplier = await lookup.FindByIdentificationAsync("04", CustomerOnlyRuc, RoleType.Supplier, default);

        withData!.HasActiveRole.Should().BeFalse();
        withData.HasRevokedRole.Should().BeTrue();
        withData.RevokedRoleHasFiscalData.Should().BeTrue();
        withoutData!.HasRevokedRole.Should().BeTrue();
        withoutData.RevokedRoleHasFiscalData.Should().BeFalse();
        neverSupplier!.HasRevokedRole.Should().BeFalse("nunca tuvo rol Proveedor: es 'sin rol', no 'revocado'");
    }

    [Fact]
    public async Task Proveedor_revocado_se_reactiva_conservando_sus_datos_fiscales()
    {
        var bpId = await SeedRevokedSupplierAsync(NewRuc,
            SupplierRoleConfig.Create(isRetentionExempt: true, isRequiredToKeepAccounting: true));
        var batchId = await BatchAsync(NewSupplier("EXT-0401"), Reactivate(NewRuc, bpId));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var after = await CountsAsync();
        after.Partners.Should().Be(_baseline.Partners + 1, "el revocado se reutiliza, solo EXT-0401 es nuevo");
        after.SupplierRoles.Should().Be(_baseline.SupplierRoles + 2);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.BusinessPartnerRoles.AsNoTracking().CountAsync(r => r.BusinessPartnerId == bpId))
            .Should().Be(1, "se reactiva el mismo rol, no se crea otro");
        var config = await SupplierConfigAsync(bpId);
        config!.IsRequiredToKeepAccounting.Should().BeTrue("los datos fiscales previos no se sobrescriben con el NO del archivo");
        config.IsRetentionExempt.Should().BeTrue();
    }

    [Fact]
    public async Task Revocado_sin_datos_fiscales_revierte_todo_el_lote()
    {
        // Validate lo bloquea (IL-3A); aquí se fuerza la fila para probar la revalidación al confirmar.
        var bpId = await SeedRevokedSupplierAsync(NewRuc, null);
        var batchId = await BatchAsync(NewSupplier("EXT-0501"), Reactivate(NewRuc, bpId));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("no tiene datos fiscales");
        await AssertRolledBackAsync(batchId);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.BusinessPartnerRoles.AsNoTracking().SingleAsync(r => r.BusinessPartnerId == bpId))
            .IsActive.Should().BeFalse("el rol sigue revocado: nada se reactiva sin datos fiscales");
    }
}
