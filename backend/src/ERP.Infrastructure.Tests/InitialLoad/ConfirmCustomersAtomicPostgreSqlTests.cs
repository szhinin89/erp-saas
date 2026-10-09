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
/// IL-2B — PostgreSQL 16 + migraciones completas. Comandos MasterData, validadores, repositorios y
/// UoW reales: BP + rol Cliente + contacto + CompanyBpSalesSettings de todo el lote quedan juntos
/// o no queda nada (incluido el Outbox). Las filas llegan ya validadas (IL-2A); los fallos se
/// provocan con datos que el dominio rechaza o con cambios del maestro posteriores a la validación.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class ConfirmCustomersAtomicPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    private const string ExistingRuc = "1791352688001";
    private const string CustomerRuc = "1790016919001";
    private static long _nextTaxNumber = 1790098000;
    private readonly ServiceProvider _services;
    private readonly string _connectionString;
    private readonly Mock<ICustomerImportSheetReader> _reader = new();
    private Action? _onLockAttempt;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _contado;
    private Guid _credito;
    private Guid _existingBp;
    private Guid _existingCustomer;
    private Counts _baseline = null!;

    private sealed record Counts(int Partners, int CustomerRoles, int Contacts, int Settings, int Outbox, int Activities);

    public ConfirmCustomersAtomicPostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        _connectionString = postgres.ConnectionString;
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
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il2b@test" && x.FullName == "IL2B"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor(), new BatchLockObserver(this)));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<IBusinessPartnerRepository, BusinessPartnerRepository>();
        services.AddScoped<IBusinessPartnerRoleRepository, BusinessPartnerRoleRepository>();
        services.AddScoped<IBusinessPartnerContactRepository, BusinessPartnerContactRepository>();
        services.AddScoped<IBusinessPartnerLocationRepository, BusinessPartnerLocationRepository>();
        services.AddScoped<ICompanyBpSalesSettingsRepository, CompanyBpSalesSettingsRepository>();
        services.AddScoped<IPaymentTermRepository, PaymentTermRepository>();
        services.AddScoped<ILegalEntityTypeRepository, LegalEntityTypeRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IIdentificationUsageValidator, IdentificationUsageValidator>();
        services.AddScoped<ICustomerImportLookup, CustomerImportLookup>();
        services.AddSingleton(_reader.Object);
        var files = new Mock<IFileStorage>();
        files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1]));
        services.AddSingleton(files.Object);
        services.AddScoped<CustomerImportProcessor>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IImportBatchRowRepository, ImportBatchRowRepository>();
        services.AddScoped<IImportBatchIssueRepository, ImportBatchIssueRepository>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Customers] = sp.GetRequiredService<CustomerImportProcessor>() });
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL2B", "il2b-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL2B S.A.", createdBy: _user);
        _company = company.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        var contado = PaymentTerm.Create(_tenant, "CONTADO", "Contado", 1, 0, _user);
        var credito = PaymentTerm.Create(_tenant, "CREDITO30", "Crédito 30", 1, 30, _user);
        (_contado, _credito) = (contado.Id, credito.Id);
        db.PaymentTerms.AddRange(contado, credito);
        var existing = BusinessPartner.Create(_tenant, "04", ExistingRuc, null, "Tercero Sin Rol S.A.", _user);
        var customer = BusinessPartner.Create(_tenant, "04", CustomerRuc, null, "Cliente Previo S.A.", _user);
        (_existingBp, _existingCustomer) = (existing.Id, customer.Id);
        db.BusinessPartners.AddRange(existing, customer);
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, customer.Id, RoleType.Customer, _user));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private ParsedCustomerRow NewCustomer(string passport, string? email = "cliente@ejemplo.test") =>
        new("06", passport, 1, "Cliente " + passport, null, "EC", email, "0999999999", _contado,
            CustomerImportAction.CreateCustomer, null);

    private ParsedCustomerRow AssignRole() =>
        new("04", ExistingRuc, null, "Tercero Sin Rol S.A.", null, null, null, null, _contado,
            CustomerImportAction.AssignCustomerRole, _existingBp);

    private ParsedCustomerRow AlreadyCustomer(Guid? term = null) =>
        new("04", CustomerRuc, null, "Cliente Previo S.A.", null, null, null, null, term ?? _contado,
            CustomerImportAction.AlreadyCustomer, _existingCustomer);

    private async Task<Guid> BatchAsync(params ParsedCustomerRow[] rows)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.Customers, _user);
        batch.AttachFile("clientes.xlsx", "clientes.xlsx", 1, _user);
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
            await db.BusinessPartnerRoles.CountAsync(r => r.RoleType == RoleType.Customer && r.IsActive),
            await db.BusinessPartnerContacts.CountAsync(),
            await db.CompanyBpSalesSettings.CountAsync(),
            await db.OutboxMessages.CountAsync(o => o.TenantId == _tenant),
            await db.UserActivities.CountAsync(a => a.TenantId == _tenant));
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
    }

    [Fact]
    public async Task Lote_mixto_confirma_todo_en_una_transaccion()
    {
        var batchId = await BatchAsync(NewCustomer("PAS0001"), AssignRole(), AlreadyCustomer());

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        result.Value.ImportedRows.Should().Be(3);
        var after = await CountsAsync();
        after.Partners.Should().Be(_baseline.Partners + 1, "solo la identificación nueva crea BP");
        after.CustomerRoles.Should().Be(_baseline.CustomerRoles + 2);
        after.Contacts.Should().Be(_baseline.Contacts + 1);
        after.Settings.Should().Be(_baseline.Settings + 3);
        after.Outbox.Should().BeGreaterThan(_baseline.Outbox);

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.CompanyBpSalesSettings.AsNoTracking().Where(s => s.CompanyId == _company)
            .AllAsync(s => s.PaymentTermId == _contado)).Should().BeTrue();
        (await db.BusinessPartnerRoles.AsNoTracking().AnyAsync(r => r.BusinessPartnerId == _existingBp
            && r.RoleType == RoleType.Customer && r.IsActive)).Should().BeTrue();
    }

    [Fact]
    public async Task Contacto_invalido_en_fila_intermedia_revierte_todo_el_lote()
    {
        var batchId = await BatchAsync(NewCustomer("PAS0101"), AssignRole(),
            NewCustomer("PAS0102", email: "no-es-email"), NewCustomer("PAS0103"));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 3").And.Contain("No se importó ningún cliente").And.NotContain("Error interno");
        await AssertRolledBackAsync(batchId);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.BusinessPartnerRoles.AsNoTracking().AnyAsync(r => r.BusinessPartnerId == _existingBp))
            .Should().BeFalse("el rol asignado en la fila 2 también se revierte");
    }

    [Fact]
    public async Task Bp_existente_sin_rol_se_reutiliza_sin_duplicar()
    {
        var batchId = await BatchAsync(AssignRole());

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var after = await CountsAsync();
        after.Partners.Should().Be(_baseline.Partners);
        after.CustomerRoles.Should().Be(_baseline.CustomerRoles + 1);
        after.Settings.Should().Be(_baseline.Settings + 1);
        after.Contacts.Should().Be(_baseline.Contacts);
    }

    [Fact]
    public async Task Cliente_existente_con_la_misma_condicion_es_idempotente()
    {
        await SeedSettingsAsync(_existingCustomer, _contado);
        var batchId = await BatchAsync(AlreadyCustomer());

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        var after = await CountsAsync();
        after.Should().Be(_baseline, "un cliente ya configurado no genera escrituras de maestro");
    }

    [Fact]
    public async Task Condicion_de_pago_conflictiva_revierte_todo_el_lote()
    {
        // La validación vio al cliente sin condición; luego alguien le asignó otra en esta empresa.
        var batchId = await BatchAsync(NewCustomer("PAS0201"), AlreadyCustomer());
        await SeedSettingsAsync(_existingCustomer, _credito);
        _baseline = await CountsAsync();

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("otra condición de pago").And.Contain("Vuelva a validar");
        await AssertRolledBackAsync(batchId);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.CompanyBpSalesSettings.AsNoTracking().SingleAsync(s => s.BusinessPartnerId == _existingCustomer))
            .PaymentTermId.Should().Be(_credito, "la condición existente nunca se sobrescribe");
    }

    [Fact]
    public async Task Tercero_creado_despues_de_validar_no_se_duplica()
    {
        var batchId = await BatchAsync(NewCustomer("PAS0301"), NewCustomer("PAS0302"));
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.BusinessPartners.Add(BusinessPartner.Create(_tenant, "06", "pas0302", 1, "Creado Manual", _user));
            await db.SaveChangesAsync();
        }
        _baseline = await CountsAsync();

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya existe");
        await AssertRolledBackAsync(batchId);
    }

    private async Task SeedSettingsAsync(Guid businessPartnerId, Guid termId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.CompanyBpSalesSettings.Add(CompanyBpSalesSettings.Create(_tenant, _company, businessPartnerId, termId, _user));
        await db.SaveChangesAsync();
    }
}
