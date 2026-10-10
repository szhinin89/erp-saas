using System.Globalization;
using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Tests.Seeding;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-6A — PostgreSQL 16 + migraciones completas. Validación real de CxP Inicial (handlers, lookups,
/// filtros globales, catálogo SRI y UoW reales; solo el lector de Excel es fake): 201 filas,
/// duplicados contra CxP existentes de cualquier origen dentro de la empresa, tipo de documento SRI
/// real, forma de la CxP InitialBalance reforzada en BD y lote atado a su sucursal (confirmación IL-6B: ver .Confirm.cs).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class ValidateInitialPayablesPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    // AlwaysTodayCompanyClock fija "hoy" en 2026-09-17: la apertura de la empresa es anterior.
    private const string Cutoff = "2026-08-31";
    private static readonly DateOnly OpeningBalanceDate = new(2026, 8, 31);
    private const string SupplierRuc = "1790016919001";
    private const string NoRoleRuc = "1791352688001";
    private static long _nextTaxNumber = 1790098000;
    private readonly ServiceProvider _services;
    private readonly Mock<IInitialPayableImportSheetReader> _reader = new();
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _branch;
    private Guid _otherCompany;
    private Guid _otherBranch;
    private BusinessPartner _supplier = null!;

    public ValidateInitialPayablesPostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(x => x.CompanyId).Returns(() => _company);
        company.SetupGet(x => x.HasCompanyContext).Returns(() => _company != Guid.Empty);
        var branch = new Mock<ICurrentBranch>();
        branch.SetupGet(x => x.BranchId).Returns(() => _branch);
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(() => _tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(() => _company);
        ctx.SetupGet(x => x.HasTenant).Returns(() => _tenant != Guid.Empty);
        ctx.SetupGet(x => x.HasCompany).Returns(() => _company != Guid.Empty);
        ctx.SetupGet(x => x.UserId).Returns(_user);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(InitialPayableImportProcessor).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<InitialPayableImportProcessor>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(branch.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il6a@test" && x.FullName == "IL6A"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<ICompanyClock>(new AlwaysTodayCompanyClock());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<IBusinessPartnerImportLookup, BusinessPartnerImportLookup>();
        services.AddScoped<IInitialPayableLookup, InitialPayableLookup>();
        services.AddScoped<IOpeningBalanceConstraintsReader, OpeningBalanceConstraintsReader>();
        // IL-6B — repositorio real envuelto para inyectar un fallo a mitad de la escritura del lote.
        services.AddScoped<ERP.Infrastructure.Persistence.Repositories.Payables.AccountsPayableRepository>();
        services.AddScoped<ERP.Domain.Modules.Payables.Interfaces.IAccountsPayableRepository>(sp =>
            new FailingPayableRepository(
                sp.GetRequiredService<ERP.Infrastructure.Persistence.Repositories.Payables.AccountsPayableRepository>(),
                () => _failOnAdd));
        services.AddScoped<ERP.Domain.MasterData.Interfaces.IBusinessPartnerRepository, BusinessPartnerRepository>();
        services.AddScoped<ERP.Domain.Modules.Purchases.Interfaces.ISupplierCreditRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.SupplierCreditRepository>();
        services.AddScoped<ERP.Domain.Modules.SriCatalogs.Interfaces.ISriCatalogLookupRepository,
            ERP.Infrastructure.Persistence.Repositories.SriCatalogs.SriCatalogLookupRepository>();
        services.AddSingleton(_reader.Object);
        var files = new Mock<ERP.Application.Common.Interfaces.IFileStorage>();
        files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1]));
        services.AddSingleton(files.Object);
        services.AddScoped<InitialPayableImportProcessor>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IImportBatchRowRepository, ImportBatchRowRepository>();
        services.AddScoped<IImportBatchIssueRepository, ImportBatchIssueRepository>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            new Dictionary<ImportType, IImportProcessor>
            {
                [ImportType.InitialPayables] = sp.GetRequiredService<InitialPayableImportProcessor>(),
            });
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL6A", "il6a-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL6A S.A.", createdBy: _user);
        var other = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL6A Otra S.A.", createdBy: _user);
        company.SetOpeningBalanceDate(OpeningBalanceDate, new OpeningBalanceDateConstraints(false, []), _user);
        _company = company.Id;
        _otherCompany = other.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        await db.SaveChangesAsync();

        var main = NewBranch(_company, "B01", isMain: true);
        var second = NewBranch(_company, "B02", isMain: false);
        var otherMain = NewBranch(_otherCompany, "B01", isMain: true);
        db.Branches.AddRange(main, second, otherMain);
        _supplier = BusinessPartner.Create(_tenant, "04", SupplierRuc, null, "Proveedor Uno S.A.", _user);
        var customerOnly = BusinessPartner.Create(_tenant, "04", NoRoleRuc, null, "Solo Cliente S.A.", _user);
        db.BusinessPartners.AddRange(_supplier, customerOnly);
        await db.SaveChangesAsync();
        // Sin SupplierRetentionDefault ni configuración por empresa: no se exige (decisión 6).
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, _supplier.Id, RoleType.Supplier, _user));
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, customerOnly.Id, RoleType.Customer, _user));
        await db.SaveChangesAsync();
        _branch = main.Id;
        _otherBranch = second.Id;
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private BranchEntity NewBranch(Guid companyId, string code, bool isMain) =>
        BranchEntity.Create(tenantId: _tenant, name: "Sucursal " + code, address: "Av. 1", code: code,
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: isMain, createdBy: _user, companyId: companyId);

    private static Dictionary<string, string?> Row(string number, string document, string balance = "100.00",
        string docType = "01") => new()
    {
        [InitialPayableImportColumns.IdentificationType] = "04",
        [InitialPayableImportColumns.IdentificationNumber] = number,
        [InitialPayableImportColumns.DocumentType] = docType,
        [InitialPayableImportColumns.DocumentNumber] = document,
        [InitialPayableImportColumns.IssueDate] = "2026-07-15",
        [InitialPayableImportColumns.DueDate] = "2026-09-15",
        [InitialPayableImportColumns.Balance] = balance,
        [InitialPayableImportColumns.Currency] = "USD",
        [InitialPayableImportColumns.CutoffDate] = Cutoff,
    };

    private async Task<Guid> UploadedBatchAsync(params Dictionary<string, string?>[] rows)
    {
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult(rows.Cast<IReadOnlyDictionary<string, string?>>().ToList()));
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.InitialPayables, _user);
        batch.AttachFile("cxp.xlsx", "cxp.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<Result<TResult>> SendAsync<TResult>(IRequest<Result<TResult>> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private Task<List<ImportBatchIssue>> IssuesAsync(Guid batchId) =>
        QueryAsync(db => db.ImportBatchIssues.Where(i => i.ImportBatchId == batchId).ToListAsync());

    private Task<int> TenantPayablesAsync() =>
        QueryAsync(db => db.AccountsPayables.IgnoreQueryFilters().CountAsync(p => p.TenantId == _tenant));

    [Fact]
    public async Task Valida_201_filas_sin_escribir_ninguna_cxp_ni_compra()
    {
        var rows = Enumerable.Range(1, 201)
            .Select(i => Row(SupplierRuc, $"001-001-{i:D9}", (i + 0.25m).ToString(CultureInfo.InvariantCulture),
                i % 3 == 0 ? "03" : "01"))
            .ToArray();
        var batchId = await UploadedBatchAsync(rows);

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Validated);
        result.Value.TotalRows.Should().Be(201);
        result.Value.ValidRows.Should().Be(201);
        result.Value.IssueRows.Should().Be(0);
        result.Value.WarningRows.Should().Be(0);
        (await IssuesAsync(batchId)).Should().BeEmpty("proveedor con rol, tipos SRI reales y corte = apertura");
        (await TenantPayablesAsync()).Should().Be(0, "IL-6A solo valida: no crea CxP");
        (await QueryAsync(db => db.PurchaseInvoices.IgnoreQueryFilters().CountAsync(p => p.TenantId == _tenant)))
            .Should().Be(0, "nunca se crean compras históricas ficticias");
    }

    [Fact]
    public async Task Duplicados_contra_cxp_existentes_de_la_empresa_de_cualquier_origen()
    {
        await SeedOriginPayableAsync(AccountsPayableOriginType.PurchaseInvoice, "001-001-000000001");
        await SeedOriginPayableAsync(AccountsPayableOriginType.ExpenseDocument, "GTO-7");
        await SeedInitialBalanceAsync(_company, _branch, "SI-8");
        await SeedInitialBalanceAsync(_otherCompany, await OtherCompanyBranchAsync(), "OTRA-9");
        var batchId = await UploadedBatchAsync(
            Row(SupplierRuc, "001001000000001"),
            Row(SupplierRuc, "gto 7"),
            Row(SupplierRuc, "si-8"),
            Row(SupplierRuc, "OTRA-9"),
            Row(NoRoleRuc, "X-1"),
            Row("1790012345001", "X-2"));

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        var issues = (await IssuesAsync(batchId)).Where(i => i.Severity == ImportSeverity.Error).ToList();
        issues.Where(i => i.Code == "DOCUMENT_ALREADY_EXISTS").Select(i => i.RowNumber).Should()
            .BeEquivalentTo([1, 2, 3], "compra, gasto y saldo inicial existentes se comparan normalizados");
        issues.Should().NotContain(i => i.RowNumber == 4, "una CxP de otra empresa no es visible ni duplica");
        issues.Should().ContainSingle(i => i.RowNumber == 5 && i.Code == "SUPPLIER_ROLE_MISSING");
        issues.Should().ContainSingle(i => i.RowNumber == 6 && i.Code == "SUPPLIER_NOT_FOUND");
        result.Value!.ValidRows.Should().Be(1);
        (await TenantPayablesAsync()).Should().Be(4, "la validación no escribe CxP");
    }

    [Fact]
    public async Task Tipo_de_documento_se_valida_contra_el_catalogo_SRI_real()
    {
        var batchId = await UploadedBatchAsync(
            Row(SupplierRuc, "D-1", docType: "05"),
            Row(SupplierRuc, "D-2", docType: "02"),
            Row(SupplierRuc, "D-3", docType: "04"),
            Row(SupplierRuc, "D-4", docType: "99"));

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        var issues = (await IssuesAsync(batchId)).Where(i => i.Severity == ImportSeverity.Error).ToList();
        issues.Should().NotContain(i => i.RowNumber == 1);
        issues.Should().ContainSingle(i => i.RowNumber == 2 && i.Code == "DOCUMENT_TYPE_NOT_FOUND",
            "02 está declarado inactivo en el seed SRI y así queda en BD");
        issues.Should().ContainSingle(i => i.RowNumber == 3 && i.Code == "DOCUMENT_TYPE_NOT_PAYABLE");
        issues.Should().ContainSingle(i => i.RowNumber == 4 && i.Code == "DOCUMENT_TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task Sin_fecha_de_apertura_en_la_empresa_ninguna_fila_es_valida()
    {
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET opening_balance_date = NULL WHERE id = {_company}"));
        var batchId = await UploadedBatchAsync(Row(SupplierRuc, "A-1"), Row(SupplierRuc, "A-2"));

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ValidRows.Should().Be(0);
        (await IssuesAsync(batchId)).Where(i => i.Code == "OPENING_BALANCE_DATE_NOT_SET").Select(i => i.RowNumber)
            .Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Fecha_de_corte_distinta_de_la_apertura_de_la_empresa_es_error()
    {
        var row = Row(SupplierRuc, "A-1");
        row[InitialPayableImportColumns.CutoffDate] = "2026-07-31";
        row[InitialPayableImportColumns.IssueDate] = "2026-07-01";
        var batchId = await UploadedBatchAsync(row);

        (await SendAsync(new ValidateImportBatchCommand(batchId))).Value!.ValidRows.Should().Be(0);
        (await IssuesAsync(batchId)).Should().ContainSingle(i => i.Code == "CUTOFF_DATE_MISMATCH");
    }

    [Fact]
    public async Task El_lote_queda_atado_a_la_sucursal_con_la_que_se_valido()
    {
        var batchId = await UploadedBatchAsync(Row(SupplierRuc, "A-1"));
        (await SendAsync(new ValidateImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();
        var mainBranch = _branch;

        _branch = _otherBranch;
        var revalidate = await SendAsync(new ValidateImportBatchCommand(batchId));
        var cancel = await SendAsync(new CancelImportBatchCommand(batchId));

        revalidate.IsSuccess.Should().BeFalse();
        revalidate.Error.Should().Contain("otra sucursal");
        cancel.IsSuccess.Should().BeFalse();
        _branch = mainBranch;
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batchId)))
            .Should().Be(1, "el staging previo se conserva");
        (await SendAsync(new CancelImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Otra_empresa_del_tenant_no_ve_ni_valida_el_lote()
    {
        var batchId = await UploadedBatchAsync(Row(SupplierRuc, "A-1"));
        var mine = _company;

        _company = _otherCompany;
        var result = await SendAsync(new ValidateImportBatchCommand(batchId));
        _company = mine;

        result.IsSuccess.Should().BeFalse();
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batchId))).Should().Be(0);
    }

    [Fact]
    public async Task La_bd_refuerza_la_forma_del_saldo_inicial_y_su_unicidad()
    {
        var payable = await SeedInitialBalanceAsync(_company, _branch, "FAC-500");
        var stored = await QueryAsync(db => db.AccountsPayables.Include(p => p.Installments)
            .SingleAsync(p => p.Id == payable.Id));
        stored.OriginType.Should().Be(AccountsPayableOriginType.InitialBalance);
        stored.DocumentType.Should().Be("01");
        stored.DocumentNumberNormalized.Should().Be("FAC500");
        stored.ImportBatchId.Should().NotBeNull();
        stored.AccountingDate.Should().Be(OpeningBalanceDate);
        stored.Installments.Should().ContainSingle(i => i.Amount == 100m && i.DueDate == new DateOnly(2026, 9, 15));

        // Otra escritura del mismo documento con otro formato (p. ej. una confirmación concurrente que
        // pasó la validación) choca contra el índice único sobre el número normalizado.
        var duplicate = () => SeedInitialBalanceAsync(_company, _branch, "fac 500");
        (await duplicate.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("uq_accounts_payables_initial_balance_document");

        // El mismo número en otra empresa del tenant no colisiona.
        await SeedInitialBalanceAsync(_otherCompany, await OtherCompanyBranchAsync(), "FAC-500");

        // Un saldo inicial sin lote, o una compra con datos de saldo inicial, viola el CHECK.
        var withoutBatch = () => QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE accounts_payables SET import_batch_id = NULL WHERE id = {payable.Id}"));
        (await withoutBatch.Should().ThrowAsync<PostgresException>()).Which.ConstraintName
            .Should().Be("chk_accounts_payables_initial_balance_shape");
        var purchase = await SeedOriginPayableAsync(AccountsPayableOriginType.PurchaseInvoice, "001-001-2");
        var purchaseWithKey = () => QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE accounts_payables SET document_number_normalized = 'X' WHERE id = {purchase.Id}"));
        (await purchaseWithKey.Should().ThrowAsync<PostgresException>()).Which.ConstraintName
            .Should().Be("chk_accounts_payables_initial_balance_shape");
    }

    private async Task<Guid> OtherCompanyBranchAsync() =>
        await QueryAsync(db => db.Branches.IgnoreQueryFilters()
            .Where(b => b.CompanyId == _otherCompany).Select(b => b.Id).SingleAsync());

    private async Task<AccountsPayable> SeedInitialBalanceAsync(Guid companyId, Guid branchId, string document)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, companyId, ImportType.InitialPayables, _user);
        db.ImportBatches.Add(batch);
        var payable = AccountsPayable.CreateInitialBalance(_tenant, companyId, branchId, _supplier.Id, "01", document,
            new DateOnly(2026, 7, 15), new DateOnly(2026, 9, 15), OpeningBalanceDate, 100m, batch.Id, Guid.NewGuid(),
            _user);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return payable;
    }

    // CxP de Compra/Gasto: origin_id no tiene FK, basta como ancla del documento existente.
    private async Task<AccountsPayable> SeedOriginPayableAsync(AccountsPayableOriginType origin, string document)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var payable = AccountsPayable.CreateFromOrigin(_tenant, _company, _branch, _supplier.Id, origin,
            Guid.NewGuid(), "01", document, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), _user);
        payable.AddInstallment(1, new DateOnly(2026, 8, 1), 50m);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return payable;
    }
}
