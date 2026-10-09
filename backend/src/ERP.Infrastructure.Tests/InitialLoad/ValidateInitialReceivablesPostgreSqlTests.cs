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
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.InitialLoad;
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
/// IL-5A — PostgreSQL 16 + migraciones completas. Validación real de CxC Inicial (handlers,
/// lookups, filtros globales y UoW reales; solo el lector de Excel es fake): 201 filas, duplicados
/// contra CxC existentes de cualquier origen dentro de la empresa, forma de la CxC InitialBalance
/// reforzada en BD, confirmación todavía bloqueada y lote atado a su sucursal.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ValidateInitialReceivablesPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    // AlwaysTodayCompanyClock fija "hoy" en 2026-09-17: la apertura de la empresa es anterior.
    private const string Cutoff = "2026-08-31";
    private static readonly DateOnly OpeningBalanceDate = new(2026, 8, 31);
    private static long _nextTaxNumber = 1790097000;
    private readonly ServiceProvider _services;
    private readonly Mock<IInitialReceivableImportSheetReader> _reader = new();
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _branch;
    private Guid _otherCompany;
    private Guid _otherBranch;
    private BusinessPartner _customer = null!;
    private BusinessPartner _noRole = null!;

    public ValidateInitialReceivablesPostgreSqlTests(InitialLoadPostgresFixture postgres)
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
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(InitialReceivableImportProcessor).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<InitialReceivableImportProcessor>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(branch.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il5a@test" && x.FullName == "IL5A"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<ICompanyClock>(new AlwaysTodayCompanyClock());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<IBusinessPartnerImportLookup, BusinessPartnerImportLookup>();
        services.AddScoped<IInitialReceivableLookup, InitialReceivableLookup>();
        services.AddSingleton(_reader.Object);
        var files = new Mock<ERP.Application.Common.Interfaces.IFileStorage>();
        files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1]));
        services.AddSingleton(files.Object);
        services.AddScoped<InitialReceivableImportProcessor>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IImportBatchRowRepository, ImportBatchRowRepository>();
        services.AddScoped<IImportBatchIssueRepository, ImportBatchIssueRepository>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            new Dictionary<ImportType, IImportProcessor>
            {
                [ImportType.InitialReceivables] = sp.GetRequiredService<InitialReceivableImportProcessor>(),
            });
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL5A", "il5a-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL5A S.A.", createdBy: _user);
        var other = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL5A Otra S.A.", createdBy: _user);
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
        _customer = BusinessPartner.Create(_tenant, "04", "1790016919001", null, "Cliente Uno S.A.", _user);
        _noRole = BusinessPartner.Create(_tenant, "04", "1791352688001", null, "Tercero Sin Rol S.A.", _user);
        db.BusinessPartners.AddRange(_customer, _noRole);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, _customer.Id, RoleType.Customer, _user));
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

    private static Dictionary<string, string?> Row(string number, string document, string balance = "100.00") => new()
    {
        [InitialReceivableImportColumns.IdentificationType] = "04",
        [InitialReceivableImportColumns.IdentificationNumber] = number,
        [InitialReceivableImportColumns.DocumentNumber] = document,
        [InitialReceivableImportColumns.IssueDate] = "2026-07-15",
        [InitialReceivableImportColumns.DueDate] = "2026-09-15",
        [InitialReceivableImportColumns.Balance] = balance,
        [InitialReceivableImportColumns.Currency] = "USD",
        [InitialReceivableImportColumns.CutoffDate] = Cutoff,
    };

    private async Task<Guid> UploadedBatchAsync(params Dictionary<string, string?>[] rows)
    {
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult(rows.Cast<IReadOnlyDictionary<string, string?>>().ToList()));
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.InitialReceivables, _user);
        batch.AttachFile("cxc.xlsx", "cxc.xlsx", 1, _user);
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

    [Fact]
    public async Task Valida_201_filas_sin_escribir_ninguna_cxc()
    {
        var rows = Enumerable.Range(1, 201)
            .Select(i => Row("1790016919001", $"001-001-{i:D9}", (i + 0.25m).ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        var batchId = await UploadedBatchAsync(rows);

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Validated);
        result.Value.TotalRows.Should().Be(201);
        result.Value.ValidRows.Should().Be(201);
        result.Value.IssueRows.Should().Be(0);
        result.Value.WarningRows.Should().Be(0);
        (await IssuesAsync(batchId)).Should().BeEmpty("el corte coincide con Company.OpeningBalanceDate sin depender de IL-4");
        (await QueryAsync(db => db.SalesReceivables.IgnoreQueryFilters().CountAsync(r => r.TenantId == _tenant)))
            .Should().Be(0, "IL-5A solo valida: no crea CxC ni ventas");
        (await QueryAsync(db => db.SalesInvoices.IgnoreQueryFilters().CountAsync(r => r.TenantId == _tenant)))
            .Should().Be(0);
    }

    [Fact]
    public async Task Duplicados_contra_cxc_existentes_de_la_empresa_de_cualquier_origen()
    {
        await SeedInvoiceReceivableAsync("001-001-000000001");
        await SeedInitialBalanceAsync(_company, _branch, "SI-7");
        await SeedInitialBalanceAsync(_otherCompany, await OtherCompanyBranchAsync(), "OTRA-9");
        var batchId = await UploadedBatchAsync(
            Row("1790016919001", "001001000000001"),
            Row("1790016919001", "si 7"),
            Row("1790016919001", "OTRA-9"),
            Row("1791352688001", "X-1"),
            Row("1790012345001", "X-2"));

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        var issues = (await IssuesAsync(batchId)).Where(i => i.Severity == ImportSeverity.Error).ToList();
        issues.Where(i => i.Code == "DOCUMENT_ALREADY_EXISTS").Select(i => i.RowNumber).Should()
            .BeEquivalentTo([1, 2], "la factura y el saldo inicial existentes se comparan normalizados");
        issues.Should().NotContain(i => i.RowNumber == 3, "una CxC de otra empresa no es visible ni duplica");
        issues.Should().ContainSingle(i => i.RowNumber == 4 && i.Code == "CUSTOMER_ROLE_MISSING");
        issues.Should().ContainSingle(i => i.RowNumber == 5 && i.Code == "CUSTOMER_NOT_FOUND");
        result.Value!.ValidRows.Should().Be(1);
    }

    [Fact]
    public async Task Sin_fecha_de_apertura_en_la_empresa_ninguna_fila_es_valida()
    {
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET opening_balance_date = NULL WHERE id = {_company}"));
        var batchId = await UploadedBatchAsync(Row("1790016919001", "A-1"), Row("1790016919001", "A-2"));

        var result = await SendAsync(new ValidateImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ValidRows.Should().Be(0);
        (await IssuesAsync(batchId)).Where(i => i.Code == "OPENING_BALANCE_DATE_NOT_SET").Select(i => i.RowNumber)
            .Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Fecha_de_corte_distinta_de_la_apertura_de_la_empresa_es_error()
    {
        var row = Row("1790016919001", "A-1");
        row[InitialReceivableImportColumns.CutoffDate] = "2026-07-31";
        row[InitialReceivableImportColumns.IssueDate] = "2026-07-01";
        var batchId = await UploadedBatchAsync(row);

        (await SendAsync(new ValidateImportBatchCommand(batchId))).Value!.ValidRows.Should().Be(0);
        (await IssuesAsync(batchId)).Should().ContainSingle(i => i.Code == "CUTOFF_DATE_MISMATCH");
    }

    [Fact]
    public async Task La_confirmacion_esta_bloqueada_y_no_cambia_el_lote()
    {
        var batchId = await UploadedBatchAsync(Row("1790016919001", "A-1"));
        (await SendAsync(new ValidateImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("todavía no está disponible");
        (await QueryAsync(db => db.ImportBatches.SingleAsync(b => b.Id == batchId))).Status
            .Should().Be(ImportStatus.Validated);
        (await QueryAsync(db => db.SalesReceivables.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task El_lote_queda_atado_a_la_sucursal_con_la_que_se_valido()
    {
        var batchId = await UploadedBatchAsync(Row("1790016919001", "A-1"));
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
    public async Task La_bd_refuerza_la_forma_del_saldo_inicial_y_su_unicidad()
    {
        var receivable = await SeedInitialBalanceAsync(_company, _branch, "FAC-500");
        var stored = await QueryAsync(db => db.SalesReceivables.Include(r => r.Installments)
            .SingleAsync(r => r.Id == receivable.Id));
        stored.Origin.Should().Be(SalesReceivableOrigin.InitialBalance);
        stored.InvoiceId.Should().BeNull();
        stored.Installments.Should().ContainSingle(i => i.Amount == 100m && i.DueDate == new DateOnly(2026, 9, 15));

        stored.DocumentNumberNormalized.Should().Be("FAC500");

        // Otra escritura del mismo documento con otro formato (p. ej. una confirmación concurrente que
        // pasó la validación) choca contra el índice único sobre el número normalizado.
        var duplicate = () => SeedInitialBalanceAsync(_company, _branch, "fac 500");
        (await duplicate.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("uq_sales_receivables_initial_balance_document");

        // El mismo número en otra empresa del tenant no colisiona.
        await SeedInitialBalanceAsync(_otherCompany, await OtherCompanyBranchAsync(), "FAC-500");

        // Un saldo inicial sin sucursal/lote, o con factura, viola el CHECK aunque se salte el dominio.
        var shapeViolation = () => QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE sales_receivables SET branch_id = NULL WHERE id = {receivable.Id}
            """));
        (await shapeViolation.Should().ThrowAsync<PostgresException>()).Which.ConstraintName
            .Should().Be("chk_sales_receivables_origin_shape");
    }

    private async Task<Guid> OtherCompanyBranchAsync() =>
        await QueryAsync(db => db.Branches.IgnoreQueryFilters()
            .Where(b => b.CompanyId == _otherCompany).Select(b => b.Id).SingleAsync());

    private async Task<SalesReceivable> SeedInitialBalanceAsync(Guid companyId, Guid branchId, string document)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, companyId, ImportType.InitialReceivables, _user);
        db.ImportBatches.Add(batch);
        var receivable = SalesReceivable.CreateInitialBalance(_tenant, companyId, branchId, _customer.Id, document,
            new DateOnly(2026, 7, 15), new DateOnly(2026, 9, 15), 100m, batch.Id, _user);
        db.SalesReceivables.Add(receivable);
        await db.SaveChangesAsync();
        return receivable;
    }

    private async Task SeedInvoiceReceivableAsync(string invoiceNumber)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var establishment = Establishment.Create(_tenant, branchId: _branch, _company, code: "001", name: "Matriz",
            address: "Av. 1", phone: null, isMain: true, createdBy: _user);
        var cashRegister = CashRegister.Create(_tenant, _company, _branch, "CAJA-01", "Caja", _user);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(_tenant, _company, establishment.Id, code: "001", name: "PE-001",
            emissionType: EmissionType.Electronic, isDefault: true, createdBy: _user);
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        var session = CashSession.Open(_tenant, _company, _branch, _user, cashRegister.Id, "CAJA-01", "Caja",
            emissionPoint.Id, "001", 0m, _user);
        db.CashSessions.Add(session);
        await db.SaveChangesAsync();
        // Factura solo como ancla de la FK de SalesReceivable.InvoiceId (origen Invoice).
        var invoice = SalesInvoice.CreateDraft(_tenant, _company, _branch, _customer.Id,
            CustomerSnapshot.Create("Cliente Uno S.A.", "1790016919001", "04"), invoiceNumber: invoiceNumber,
            issueDate: new DateOnly(2026, 7, 1), createdBy: _user,
            paymentTerm: PaymentTermSnapshot.Create(Guid.NewGuid(), "Crédito", installments: 1, daysBetween: 30),
            cashSessionId: session.Id, emissionType: EmissionType.Physical);
        invoice.ReplaceLines([SalesInvoiceDetail.Create(invoice.Id, _tenant, "Producto", quantity: 1, unitPrice: 100m,
            vatCode: "10", uomCode: "UNIT")], _user);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        db.SalesReceivables.Add(SalesReceivable.Create(_tenant, _company, invoice.Id, _customer.Id, 100m, _user));
        await db.SaveChangesAsync();
    }
}
