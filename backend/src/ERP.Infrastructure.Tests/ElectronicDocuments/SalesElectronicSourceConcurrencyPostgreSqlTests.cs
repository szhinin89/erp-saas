using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.ElectronicDocuments;

/// <summary>
/// R12 Fase A — concurrencia fiscal entre el comprobante de Factura 01 / Nota de Crédito 04 y su origen
/// de Ventas, contra PostgreSQL real: gate <see cref="SalesElectronicSourceLifecycleGuard"/> (lock de la
/// factura/devolución), reclamo de envío revalidado bajo ese lock, anulación de la factura según el estado
/// fiscal (<see cref="IElectronicDocumentSourceCancellation"/>) y regresión de la primera emisión (Signed,
/// sin Dispatching, job de reintentos y consulta por clave intactos). Solo se simula la frontera SRI.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesElectronicSourceConcurrencyPostgreSqlTests : IAsyncLifetime
{
    private const string SalesSource = "Sales";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sales_electronic_concurrency_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _userId;
    private int _sequence;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _userId = Guid.NewGuid();
        var tenant = Tenant.Create("R12 Fase A", $"r12a-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "R12 S.A.", createdBy: _userId);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, true, _userId, companyId: company.Id
        );
        var customer = BusinessPartner.Create(tenant.Id, "05", "1710034065", 1, "Cliente R12", _userId);
        var establishment = Establishment.Create(
            tenant.Id, branchId: branch.Id, company.Id, code: "001", name: "Matriz",
            address: "Av. Principal 123", phone: null, isMain: true, createdBy: _userId
        );
        var cashRegister = CashRegister.Create(tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja", _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id, company.Id, establishment.Id, code: "001", name: "PE-001",
            emissionType: EmissionType.Electronic, isDefault: true, createdBy: _userId
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        var cashSession = CashSession.Open(
            tenant.Id, company.Id, branch.Id, _userId, cashRegister.Id, "CAJA-01", "Caja",
            emissionPoint.Id, "001", 0m, _userId
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    // ── 1. Primera emisión de 01/04: sin cambio de comportamiento ────────────────────────────

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice)]
    [InlineData(ElectronicDocumentType.CreditNote)]
    public async Task Primera_emision_persiste_Signed_antes_del_envio_y_autoriza_sin_Dispatching(
        ElectronicDocumentType type
    )
    {
        var sourceId = await SeedSourceAsync(type);
        var sri = new SriBoundaryDouble(_companyId, type)
        {
            HoldSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        var registerTask = RegisterAsync(type, sourceId, sri);
        await sri.SendEntered.Task.WaitAsync(Wait);
        (await ReadElectronicAsync(sourceId))!
            .CurrentState.Should()
            .Be(
                ElectronicDocumentState.Signed,
                "Ventas conserva el checkpoint v1.0: Signed persistido antes de llamar al SRI, nunca Dispatching"
            );

        sri.HoldSend.SetResult();
        var result = await registerTask.WaitAsync(Wait);

        result.IsSuccess.Should().BeTrue(result.Error);
        var document = await ReadElectronicAsync(sourceId);
        document!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        document.DocumentType.Should().Be(type);
        document.PreDeadLetterState.Should().BeNull();
        sri.SendCalls.Should().Be(1);
        sri.AuthorizationCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice)]
    [InlineData(ElectronicDocumentType.CreditNote)]
    public async Task Fallo_de_transporte_en_primera_emision_queda_Signed_candidato_del_job_y_el_reintento_solo_consulta(
        ElectronicDocumentType type
    )
    {
        var sourceId = await SeedSourceAsync(type);
        var sri = new SriBoundaryDouble(_companyId, type) { ReceptionTransportFailure = true };

        (await RegisterAsync(type, sourceId, sri)).IsSuccess.Should().BeTrue();

        var document = await ReadElectronicAsync(sourceId);
        document!.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        ElectronicDocumentRetryPolicy
            .AutomaticRetryCandidate(DateTime.UtcNow.AddHours(1))
            .Compile()(document)
            .Should()
            .BeTrue("el job de reintentos (R11) sigue atendiendo el Signed de Ventas");

        sri.ReceptionTransportFailure = false;
        (await RetryAsync(document.Id, sri)).IsSuccess.Should().BeTrue();

        sri.SendCalls.Should().Be(1, "Caso #32: un Signed nunca se reenvía, solo se consulta");
        sri.AuthorizationCalls.Should().Be(1);
        (await ReadElectronicAsync(sourceId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Authorized);
    }

    // ── 2. Carreras emisión ↔ anulación de la factura ────────────────────────────────────────

    [Fact]
    public async Task Anulacion_durante_el_pipeline_antes_del_reclamo_deja_Discarded_y_el_XML_nunca_sale()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var sri = new SriBoundaryDouble(_companyId, ElectronicDocumentType.Invoice)
        {
            HoldSign = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        // La emisión ya validó la factura Authorized, registró el Draft y está firmando…
        var registerTask = RegisterAsync(ElectronicDocumentType.Invoice, invoiceId, sri);
        await sri.SignEntered.Task.WaitAsync(Wait);

        // …y entonces se anula la factura: el comprobante nunca tuvo intento externo → Discarded.
        var cancel = await CancelAsync(invoiceId).WaitAsync(Wait);
        cancel.IsSuccess.Should().BeTrue(cancel.Error);

        sri.HoldSign.SetResult();
        var result = await registerTask.WaitAsync(Wait);

        result.IsSuccess.Should().BeFalse("el reclamo revalida la factura bajo su lock y ya está anulada");
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SendCalls.Should().Be(0, "el XML jamás sale después de anular la factura");
        var document = await ReadElectronicAsync(invoiceId);
        document!.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        document.AccessKey.Should().BeNull();
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Cancelled);
    }

    [Fact]
    public async Task Reclamo_de_envio_primero_bloquea_la_anulacion_y_tras_AUTORIZADO_sigue_bloqueada()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var sri = new SriBoundaryDouble(_companyId, ElectronicDocumentType.Invoice)
        {
            HoldSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        var registerTask = RegisterAsync(ElectronicDocumentType.Invoice, invoiceId, sri);
        await sri.SendEntered.Task.WaitAsync(Wait);

        var inProcess = await CancelAsync(invoiceId).WaitAsync(Wait);
        inProcess.IsSuccess.Should().BeFalse();
        inProcess.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess);
        inProcess.Error.Should().Be(CancelSalesInvoiceHandler.ElectronicDocumentInProcessMessage);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Authorized);

        sri.HoldSend.SetResult();
        (await registerTask.WaitAsync(Wait)).IsSuccess.Should().BeTrue();
        (await ReadElectronicAsync(invoiceId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Authorized);

        var authorized = await CancelAsync(invoiceId);
        authorized.IsSuccess.Should().BeFalse();
        authorized
            .Code.Should()
            .Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment);
        authorized.Error.Should().Be(CancelSalesInvoiceHandler.AuthorizedElectronicDocumentMessage);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Authorized);
        sri.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task El_reclamo_espera_el_lock_de_la_anulacion_en_curso_y_luego_no_transmite()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var sri = new SriBoundaryDouble(_companyId, ElectronicDocumentType.Invoice);

        // Una anulación en curso ya tomó el lock de la factura (mismo FOR UPDATE que CancelSalesInvoice).
        await using var cancelDb = CreateContext();
        var cancelUow = new UnitOfWork(cancelDb);
        await cancelUow.BeginTransactionAsync();
        var locked = await new SalesInvoiceRepository(cancelDb, ScopeOf(_companyId))
            .GetByIdForUpdateAsync(_tenantId, invoiceId);
        locked.Should().NotBeNull();

        var registerTask = RegisterAsync(ElectronicDocumentType.Invoice, invoiceId, sri);
        await WaitForLockWaiterAsync();
        registerTask.IsCompleted.Should().BeFalse("el reclamo de envío espera el lock de la factura");
        sri.SendCalls.Should().Be(0);

        locked!.Cancel("Anulación concurrente", _userId);
        await cancelDb.SaveChangesAsync();
        await cancelUow.CommitAsync();

        var result = await registerTask.WaitAsync(Wait);
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SendCalls.Should().Be(0);
        (await ReadElectronicAsync(invoiceId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Draft, "el reclamo se revirtió sin escrituras");
    }

    // ── 3. Anulación de la factura según el estado fiscal del comprobante ────────────────────

    [Fact]
    public async Task Sin_comprobante_la_anulacion_se_permite_sin_crear_ninguno()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();

        (await CancelAsync(invoiceId)).IsSuccess.Should().BeTrue();

        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Cancelled);
        (await ReadElectronicAsync(invoiceId)).Should().BeNull();
    }

    [Theory]
    [InlineData(SeededState.Draft)]
    [InlineData(SeededState.Failed)]
    [InlineData(SeededState.DeadLetterFromFailed)]
    public async Task Sin_transmision_posible_la_anulacion_se_permite_y_el_comprobante_queda_Discarded(
        SeededState state
    )
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        await SeedElectronicDocumentAsync(invoiceId, ElectronicDocumentType.Invoice, state);

        var cancel = await CancelAsync(invoiceId);

        cancel.IsSuccess.Should().BeTrue(cancel.Error);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Cancelled);
        var document = await ReadElectronicAsync(invoiceId);
        document!.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        document.LastError.Should().Contain("Factura anulada").And.Contain("Motivo R12");
        cancel.Value!.ElectronicStatus.Should().Be(nameof(ElectronicDocumentState.Discarded));
    }

    [Fact]
    public async Task Rechazado_por_el_SRI_la_anulacion_se_permite_sin_tocar_el_comprobante()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        await SeedElectronicDocumentAsync(invoiceId, ElectronicDocumentType.Invoice, SeededState.Rejected);

        (await CancelAsync(invoiceId)).IsSuccess.Should().BeTrue();

        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Cancelled);
        (await ReadElectronicAsync(invoiceId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Rejected);
    }

    [Theory]
    [InlineData(SeededState.Signed)]
    [InlineData(SeededState.Received)]
    [InlineData(SeededState.DeadLetterFromSigned)]
    public async Task Resultado_SRI_incierto_bloquea_la_anulacion_sin_efectos(SeededState state)
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var documentId = await SeedElectronicDocumentAsync(invoiceId, ElectronicDocumentType.Invoice, state);
        var before = await ReadElectronicAsync(invoiceId);

        var cancel = await CancelAsync(invoiceId);

        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Authorized);
        var after = await ReadElectronicAsync(invoiceId);
        after!.Id.Should().Be(documentId);
        after.CurrentState.Should().Be(before!.CurrentState);
        after.RequiresSriReconciliation.Should().BeTrue();
    }

    [Fact]
    public async Task Autorizado_por_el_SRI_bloquea_la_anulacion_local()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        await SeedElectronicDocumentAsync(invoiceId, ElectronicDocumentType.Invoice, SeededState.Authorized);

        var cancel = await CancelAsync(invoiceId);

        cancel
            .Code.Should()
            .Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Authorized);
        (await ReadElectronicAsync(invoiceId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Authorized);
    }

    // ── 4. Aislamiento tenant / empresa ──────────────────────────────────────────────────────

    [Fact]
    public async Task Gate_y_emision_fuera_del_alcance_tenant_empresa_fallan_cerrados()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var returnId = await SeedAuthorizedReturnAsync(invoiceId);
        var foreignCompany = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            var guard = Guard(db, _companyId);
            foreach (var source in new[] { invoiceId, returnId })
            {
                (await guard.EvaluateAsync(_tenantId, _companyId, source, false))
                    .AllowsProcessing.Should()
                    .BeTrue();
                (await guard.EvaluateAsync(_tenantId, foreignCompany, source, false))
                    .AllowsProcessing.Should()
                    .BeFalse();
                (await guard.EvaluateAsync(foreignTenant, _companyId, source, false))
                    .AllowsProcessing.Should()
                    .BeFalse();
            }
        }

        var sri = new SriBoundaryDouble(foreignCompany, ElectronicDocumentType.Invoice);
        await using (var db = CreateContext())
        {
            var result = await Issuer(db, sri)
                .RegisterAsync(
                    new RegisterElectronicDocumentRequest(
                        _tenantId,
                        foreignCompany,
                        ElectronicDocumentType.Invoice,
                        SalesSource,
                        invoiceId,
                        _userId
                    )
                );
            result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        }
        sri.SignCalls.Should().Be(0);
        sri.SendCalls.Should().Be(0);
        (await ReadElectronicAsync(invoiceId)).Should().BeNull();
    }

    [Fact]
    public async Task Anulacion_desde_otra_empresa_no_encuentra_la_factura_ni_toca_su_comprobante()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        await SeedElectronicDocumentAsync(invoiceId, ElectronicDocumentType.Invoice, SeededState.Failed);

        var cancel = await CancelAsync(invoiceId, companyId: Guid.NewGuid());

        cancel.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(SalesInvoiceStatus.Authorized);
        (await ReadElectronicAsync(invoiceId))!
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Failed);
    }

    [Fact]
    public async Task Nota_de_credito_solo_procesa_con_devolucion_autorizada()
    {
        var invoiceId = await SeedAuthorizedInvoiceAsync();
        var draftReturnId = await SeedAuthorizedReturnAsync(invoiceId, authorize: false);
        var sri = new SriBoundaryDouble(_companyId, ElectronicDocumentType.CreditNote);

        var result = await RegisterAsync(ElectronicDocumentType.CreditNote, draftReturnId, sri);

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SignCalls.Should().Be(0);
        (await ReadElectronicAsync(draftReturnId)).Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    public enum SeededState
    {
        Draft,
        Failed,
        DeadLetterFromFailed,
        Signed,
        Received,
        DeadLetterFromSigned,
        Rejected,
        Authorized,
    }

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            ScopeOf(_companyId)
        );

    private static FixedCurrentCompany ScopeOf(Guid companyId) => new(companyId);

    private static SalesElectronicSourceLifecycleGuard Guard(ErpDbContext db, Guid companyId) =>
        new(
            new SalesInvoiceRepository(db, ScopeOf(companyId)),
            new SalesReturnRepository(db, ScopeOf(companyId))
        );

    private static IElectronicDocumentSourceLifecycleGuardResolver Guards(
        ErpDbContext db,
        Guid companyId
    ) => new ElectronicDocumentSourceLifecycleGuardResolver([Guard(db, companyId)]);

    private ElectronicDocumentIssuer Issuer(ErpDbContext db, SriBoundaryDouble sri) =>
        new(
            new ElectronicDocumentRepository(db, new CompanyClock(db)),
            sri.Suppliers.Object,
            sri.Validators.Object,
            sri.Signing.Object,
            sri.Storage.Object,
            sri.Reception.Object,
            sri.Authorization.Object,
            new PostgresDatabaseExceptionTranslator(),
            Guards(db, _companyId),
            ERP.Infrastructure.Tests.TestData.EnabledElectronicEmissionSwitch.Instance,
            new UnitOfWork(db),
            NullLogger<ElectronicDocumentIssuer>.Instance
        );

    private async Task<Result<ElectronicDocumentDto>> RegisterAsync(
        ElectronicDocumentType type,
        Guid sourceId,
        SriBoundaryDouble sri
    )
    {
        await using var db = CreateContext();
        return await Issuer(db, sri)
            .RegisterAsync(
                new RegisterElectronicDocumentRequest(
                    _tenantId,
                    _companyId,
                    type,
                    SalesSource,
                    sourceId,
                    _userId
                )
            );
    }

    private async Task<Result<ElectronicDocumentDto>> RetryAsync(
        Guid documentId,
        SriBoundaryDouble sri
    )
    {
        await using var db = CreateContext();
        return await Issuer(db, sri).RetryAsync(_tenantId, documentId, _userId);
    }

    /// <summary>CancelSalesInvoiceHandler real (repositorios, lock, transacción y decisión fiscal reales).</summary>
    private async Task<Result<ERP.Application.Modules.Sales.DTOs.SalesInvoiceDto>> CancelAsync(
        Guid invoiceId,
        Guid? companyId = null
    )
    {
        var company = ScopeOf(companyId ?? _companyId);
        await using var db = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            company
        );
        var edocRepo = new ElectronicDocumentRepository(db, new CompanyClock(db));
        var branch = new Mock<ICurrentBranch>();
        branch.SetupGet(b => b.BranchId).Returns(_branchId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(_userId);
        var handler = new CancelSalesInvoiceHandler(
            new SalesInvoiceRepository(db, company),
            new SalesReceivableRepository(db, company),
            new SalesReturnRepository(db, company),
            new StockRepository(
                db,
                company,
                new PostgresDatabaseExceptionTranslator(),
                StandardPrecisionPolicyProvider.Instance
            ),
            edocRepo,
            new ElectronicDocumentSourceCancellation(
                edocRepo,
                Guards(db, company.CompanyId),
                new UnitOfWork(db)
            ),
            new FixedCurrentTenant(_tenantId),
            company,
            branch.Object,
            user.Object,
            new CompanyClock(db),
            new UnitOfWork(db)
        );
        return await handler.Handle(
            new CancelSalesInvoiceCommand(invoiceId, "Motivo R12"),
            CancellationToken.None
        );
    }

    /// <summary>Espera a que otra sesión de PostgreSQL quede bloqueada esperando un lock de fila.</summary>
    private async Task WaitForLockWaiterAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'",
                connection
            );
            if ((long)(await command.ExecuteScalarAsync())! > 0)
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Ninguna sesión quedó esperando el lock de la factura.");
    }

    private Task<Guid> SeedSourceAsync(ElectronicDocumentType type) =>
        type == ElectronicDocumentType.Invoice
            ? SeedAuthorizedInvoiceAsync()
            : SeedReturnForNewInvoiceAsync();

    private async Task<Guid> SeedReturnForNewInvoiceAsync() =>
        await SeedAuthorizedReturnAsync(await SeedAuthorizedInvoiceAsync());

    /// <summary>Factura electrónica autorizada, sin ítems de inventario (la anulación no mueve Kardex).</summary>
    private async Task<Guid> SeedAuthorizedInvoiceAsync()
    {
        await using var db = CreateContext();
        var number = Interlocked.Increment(ref _sequence);
        var invoice = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            CustomerSnapshot.Create("Cliente R12", "1710034065", "05"),
            invoiceNumber: $"001-001-{number:D9}",
            issueDate: new DateOnly(2026, 10, 31),
            createdBy: _userId,
            paymentTerm: PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0),
            cashSessionId: _cashSessionId,
            emissionType: EmissionType.Electronic
        );
        var line = SalesInvoiceDetail.Create(
            invoice.Id, _tenantId, "Servicio R12", quantity: 2m, unitPrice: 10m, vatCode: "0", uomCode: "UNIT"
        );
        invoice.ReplaceLines(new[] { line }, _userId);
        invoice.ReplacePayments(
            new[] { SalesInvoicePayment.Create(invoice.Id, _tenantId, Guid.NewGuid(), "01", "Efectivo", 20m) },
            _userId
        );
        invoice.Authorize(_userId);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task<Guid> SeedAuthorizedReturnAsync(Guid invoiceId, bool authorize = true)
    {
        await using var db = CreateContext();
        var invoice = await db.SalesInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
        var number = Interlocked.Increment(ref _sequence);
        var salesReturn = SalesReturn.CreateDraft(
            _tenantId, _companyId, invoiceId, _customerId, $"DEV-{number:D6}", "Devolución R12", _userId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id, _tenantId, invoice.Lines[0].Id, "Servicio R12", 1m, 10m,
                discountPct: 0m, vatCode: "0", vatRate: 0m, uomCode: "UNIT"
            ),
            _userId
        );
        if (authorize)
        {
            salesReturn.AddRefundAllocation(
                SalesReturnRefundAllocation.Create(salesReturn.Id, _tenantId, SalesReturnRefundMethod.Cash, 10m),
                _userId
            );
            salesReturn.Authorize(_userId);
        }
        db.SalesReturns.Add(salesReturn);
        await db.SaveChangesAsync();
        return salesReturn.Id;
    }

    /// <summary>Siembra un comprobante en el estado pedido, solo vía transiciones de dominio.</summary>
    private async Task<Guid> SeedElectronicDocumentAsync(
        Guid sourceId,
        ElectronicDocumentType type,
        SeededState state
    )
    {
        await using var db = CreateContext();
        var document = ElectronicDocument.Create(_tenantId, _companyId, type, SalesSource, sourceId, _userId);
        if (state is SeededState.Failed or SeededState.DeadLetterFromFailed)
            document.MarkFailed("Fallo previo a la firma (test)", _userId);
        if (state == SeededState.DeadLetterFromFailed)
            document.MarkDeadLetter("Reintentos agotados (test)", _userId);
        if (state is not (SeededState.Draft or SeededState.Failed or SeededState.DeadLetterFromFailed))
        {
            var key = SriBoundaryDouble.NewAccessKey();
            document.MarkXmlGenerated("sales/draft.xml", "1.1.0", "1.1.0", _userId);
            document.MarkSigned("sales/signed.xml", AccessKey.Create(key), _userId);
            if (state is SeededState.Received or SeededState.Rejected or SeededState.Authorized)
            {
                document.MarkSent(_userId);
                document.MarkReceived(_userId);
            }
            if (state == SeededState.DeadLetterFromSigned)
                document.MarkDeadLetter("Reintentos agotados (test)", _userId);
            if (state == SeededState.Rejected)
                document.MarkRejected("NO AUTORIZADO (test)", _userId);
            if (state == SeededState.Authorized)
                document.MarkAuthorized(AuthorizationNumber.Create(key), DateTime.UtcNow, null, _userId);
        }
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private async Task<ElectronicDocument?> ReadElectronicAsync(Guid sourceId)
    {
        await using var db = CreateContext();
        return await db
            .ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.SourceModule == SalesSource && d.SourceEntityId == sourceId);
    }

    private async Task<SalesInvoiceStatus> ReadInvoiceStatusAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        return await db
            .SalesInvoices.AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.Status)
            .SingleAsync();
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
