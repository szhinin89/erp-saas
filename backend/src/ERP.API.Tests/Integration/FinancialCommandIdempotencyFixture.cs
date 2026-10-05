using System.Net.Http.Headers;
using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — fixture HTTP real (Program completo + PostgreSQL
/// Testcontainers, mismo patrón que <see cref="CajaVentasFlowFixture"/>) para los tres comandos
/// financieros creadores de dinero: pago a proveedor, cobro de CxC y movimiento manual de caja.
/// Catálogo contable y PostingRules sembrados con el bootstrap oficial (AccountingBootstrapStep),
/// nunca reglas sueltas. Cada escenario pide sus propios documentos/caja/usuario para que los
/// contadores físicos de efectos sean independientes entre tests.
/// </summary>
public sealed class FinancialCommandIdempotencyFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _baseFactory;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid EmissionPointId { get; private set; }
    public Guid CashMethodId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CashAccountId { get; private set; }
    public Guid ManualIncomeReasonId { get; private set; }

    /// <summary>Fecha operativa dentro del período contable que siembra el bootstrap (año en curso).</summary>
    public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.UtcNow);

    private Guid _adminId;
    private Guid _anchorSessionId;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableCompany.CompanyId = CompanyId;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-FinIdem-Test", $"zh-fi-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{TenantId:N}"[..13],
            legalName: "Empresa Idempotencia S.A.",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var branch = Branch.Create(
            TenantId,
            "Matriz",
            "Av. Principal 123",
            "SUC-A",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            isMainBranch: true,
            createdBy: _adminId,
            companyId: CompanyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        BranchId = branch.Id;

        var establishment = Establishment.Create(
            TenantId,
            BranchId,
            CompanyId,
            "001",
            "Matriz",
            "Av. Principal 123",
            null,
            true,
            _adminId
        );
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(
            TenantId,
            CompanyId,
            establishment.Id,
            "001",
            null,
            EmissionType.Physical,
            true,
            _adminId
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        EmissionPointId = emissionPoint.Id;

        var cashMethod = PaymentMethod.Create(
            TenantId,
            "EFEC",
            "Efectivo",
            false,
            false,
            1,
            _adminId,
            affectsPhysicalCash: true
        );
        db.PaymentMethods.Add(cashMethod);

        // Un solo socio con ambos roles (la identificación es única por tenant).
        var partner = BusinessPartner.Create(
            TenantId,
            "05",
            "1710034065",
            1,
            "Socio Idempotencia",
            _adminId
        );
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.AddRange(
            BusinessPartnerRole.Create(TenantId, partner.Id, RoleType.Supplier, _adminId),
            BusinessPartnerRole.Create(TenantId, partner.Id, RoleType.Customer, _adminId)
        );
        CashMethodId = cashMethod.Id;
        SupplierId = partner.Id;
        CustomerId = partner.Id;

        var cashAccount = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]),
            "Caja idempotencia",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _adminId
        );
        db.Accounts.Add(cashAccount);
        await db.SaveChangesAsync();
        CashAccountId = cashAccount.Id;

        await new AccountingBootstrapStep(
            db,
            new AlwaysTodayCompanyClock(),
            NullLogger<AccountingBootstrapStep>.Instance
        ).ExecuteAsync(new CompanyBootstrapContext(TenantId, CompanyId, _adminId));
        await new PrecisionPolicyBootstrapStep(db).ExecuteAsync(
            new CompanyBootstrapContext(TenantId, CompanyId, _adminId)
        );

        var reason = CashMovementReason.Create(
            TenantId,
            CompanyId,
            "IDEM-IN",
            "Ingreso idempotencia",
            CashMovementType.ManualIncome,
            1,
            _adminId
        );
        db.Set<CashMovementReason>().Add(reason);
        await db.SaveChangesAsync();
        ManualIncomeReasonId = reason.Id;

        // Sesión ancla: solo sirve de FK real para la factura que respalda cada CxC de prueba.
        var anchor = await OpenSessionAsync(db, _adminId, "ANCLA", 0m);
        _anchorSessionId = anchor.SessionId;
    }

    /// <summary>Operador con acceso a la sucursal + caja propia con cuenta contable + sesión abierta.</summary>
    public async Task<Operator> CreateOperatorAsync(decimal openingCash = 1000m)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var user = IdentityUser.Create(
            $"op-{Guid.NewGuid():N}",
            "Operador",
            "Idem",
            $"op-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        var membership = CompanyUserMembership.Create(CompanyId, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, BranchId, _adminId)
        );
        await db.SaveChangesAsync();

        var (registerId, sessionId) = await OpenSessionAsync(
            db,
            user.Id,
            $"C{Guid.NewGuid():N}"[..8],
            openingCash
        );
        return new Operator(user.Id, registerId, sessionId);
    }

    private async Task<(Guid RegisterId, Guid SessionId)> OpenSessionAsync(
        ErpDbContext db,
        Guid userId,
        string code,
        decimal openingCash
    )
    {
        var register = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchId,
            code,
            $"Caja {code}",
            _adminId,
            EmissionPointId
        );
        register.SetAccountingAccount(CashAccountId, _adminId);
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();
        var session = CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            userId,
            register.Id,
            register.Code,
            register.Name,
            EmissionPointId,
            "001",
            openingCash,
            _adminId
        );
        db.Set<CashSession>().Add(session);
        await db.SaveChangesAsync();
        return (register.Id, session.Id);
    }

    /// <summary>CxP de Compras con una cuota pendiente (documento independiente por escenario).</summary>
    public async Task<Guid> CreatePayableInstallmentAsync(decimal amount)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            $"001-001-{Random.Shared.Next(1, 999_999_999):D9}",
            Today,
            Today,
            _adminId
        );
        var installment = payable.AddInstallment(1, Today.AddDays(30), amount);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return installment.Id;
    }

    /// <summary>
    /// CxC real (con factura ancla por FK) del cliente de prueba. <paramref name="foreignCompany"/>:
    /// la CxC (y su factura) pertenecen a OTRA empresa del mismo tenant — fuera del alcance del operador.
    /// </summary>
    public async Task<Guid> CreateReceivableAsync(decimal amount, bool foreignCompany = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var companyId = foreignCompany ? await EnsureForeignCompanyAsync(db) : CompanyId;
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            companyId,
            BranchId,
            CustomerId,
            CustomerSnapshot.Create("Cliente Idempotencia", "1710034065", "05"),
            invoiceNumber: $"001-001-{Random.Shared.Next(1, 999_999_999):D9}",
            issueDate: Today,
            createdBy: _adminId,
            paymentTerm: PaymentTermSnapshot.Create(
                Guid.NewGuid(),
                "Crédito",
                installments: 1,
                daysBetween: 30
            ),
            cashSessionId: _anchorSessionId,
            emissionType: EmissionType.Physical
        );
        invoice.ReplaceLines(
            new[]
            {
                SalesInvoiceDetail.Create(
                    invoice.Id,
                    TenantId,
                    "Producto",
                    quantity: 1,
                    unitPrice: amount,
                    vatCode: "0",
                    uomCode: "UNIT"
                ),
            },
            _adminId
        );
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var receivable = SalesReceivable.Create(
            TenantId,
            companyId,
            invoice.Id,
            CustomerId,
            amount,
            _adminId
        );
        db.SalesReceivables.Add(receivable);
        await db.SaveChangesAsync();
        return receivable.Id;
    }

    private Guid? _foreignCompanyId;

    private async Task<Guid> EnsureForeignCompanyAsync(ErpDbContext db)
    {
        if (_foreignCompanyId is { } existing)
            return existing;
        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"099{Guid.NewGuid():N}"[..13],
            legalName: "Empresa Ajena S.A.",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _foreignCompanyId = company.Id;
        return company.Id;
    }

    /// <summary>Caja activa SIN cuenta contable (un cobro que la elige se rechaza) — para probar que un fallo no consume la intención.</summary>
    public async Task<Guid> CreateCashRegisterWithoutAccountAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var code = $"N{Guid.NewGuid():N}"[..8];
        var register = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchId,
            code,
            $"Caja {code}",
            _adminId,
            EmissionPointId
        );
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();
        return register.Id;
    }

    public async Task SetCashRegisterAccountAsync(Guid cashRegisterId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var register = await db
            .CashRegisters.IgnoreQueryFilters()
            .SingleAsync(r => r.Id == cashRegisterId);
        register.SetAccountingAccount(CashAccountId, _adminId);
        await db.SaveChangesAsync();
    }

    /// <summary>Motivo de ingreso manual propio del escenario (habilitable/deshabilitable).</summary>
    public async Task<Guid> CreateManualIncomeReasonAsync(bool active)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var reason = CashMovementReason.Create(
            TenantId,
            CompanyId,
            $"R{Guid.NewGuid():N}"[..8],
            "Ingreso escenario",
            CashMovementType.ManualIncome,
            1,
            _adminId
        );
        if (!active)
            reason.Disable(_adminId);
        db.Set<CashMovementReason>().Add(reason);
        await db.SaveChangesAsync();
        return reason.Id;
    }

    public async Task EnableReasonAsync(Guid reasonId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var reason = await db.Set<CashMovementReason>()
            .IgnoreQueryFilters()
            .SingleAsync(r => r.Id == reasonId);
        reason.Enable(_adminId);
        await db.SaveChangesAsync();
    }

    /// <summary>Usuario NO administrador con un perfil que concede exactamente los permisos indicados.</summary>
    public async Task<Guid> CreateUserWithPermissionsAsync(params string[] permissionKeys)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var profile = AccessProfile.Create(
            TenantId,
            $"Perfil {Guid.NewGuid():N}"[..20],
            null,
            _adminId
        );
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (var key in permissionKeys)
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );
        var user = IdentityUser.Create(
            $"lim-{Guid.NewGuid():N}",
            "Limitado",
            "Idem",
            $"lim-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        var membership = CompanyUserMembership.Create(
            CompanyId,
            user.Id,
            "Cajero",
            profile.Id,
            _adminId
        );
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, BranchId, _adminId)
        );
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Cliente HTTP con JWT + sucursal activa del operador (misma vía que el frontend).</summary>
    public HttpClient CreateClient(Guid userId, string role = "Admin")
    {
        _baseFactory.MutableUser.UserId = userId;
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: role)
        );
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchId.ToString());
        return client;
    }

    public IServiceScope CreateDbScope() => Factory.Services.CreateScope();

    /// <summary>Contadores físicos de efectos económicos (por documento cuando hay FK directa, delta por tenant si no).</summary>
    public async Task<Effects> CountEffectsAsync(
        Guid? installmentId = null,
        Guid? receivableId = null,
        Guid? sessionId = null
    )
    {
        using var scope = CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var supplierPaymentIds = installmentId is null
            ? new List<Guid>()
            : await db
                .SupplierPaymentApplicationLines.IgnoreQueryFilters()
                .Where(l => l.AccountsPayableInstallmentId == installmentId)
                .Select(l => l.SupplierPaymentId)
                .Distinct()
                .ToListAsync();
        var installmentPaid = installmentId is null
            ? 0m
            : await db.Set<AccountsPayableInstallment>()
                .IgnoreQueryFilters()
                .Where(i => i.Id == installmentId)
                .Select(i => i.PaidAmount)
                .SingleAsync();

        var collectionIds = receivableId is null
            ? new List<Guid>()
            : await db
                .Payments.IgnoreQueryFilters()
                .Where(p => p.Lines.Any(l => l.ReceivableId == receivableId))
                .Select(p => p.Id)
                .ToListAsync();
        var receivablePaid = receivableId is null
            ? 0m
            : await db
                .SalesReceivables.IgnoreQueryFilters()
                .Where(r => r.Id == receivableId)
                .Select(r => r.PaidAmount)
                .SingleAsync();

        var movements = sessionId is null
            ? new List<(CashMovementType Type, decimal Amount)>()
            : (
                await db
                    .CashMovements.IgnoreQueryFilters()
                    .Where(m => m.CashSessionId == sessionId)
                    .Select(m => new { m.MovementType, m.Amount })
                    .ToListAsync()
            )
                .Select(m => (Type: m.MovementType, m.Amount))
                .ToList();

        var journalEntries = await db
            .JournalEntries.IgnoreQueryFilters()
            .CountAsync(j => j.TenantId == TenantId);
        var outbox = await db
            .OutboxMessages.IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == TenantId);
        var supplierCredits = await db
            .SupplierCredits.IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == TenantId);

        return new Effects(
            supplierPaymentIds.Count,
            installmentPaid,
            collectionIds.Count,
            receivablePaid,
            movements.Count(m => m.Type is not CashMovementType.Opening),
            movements.Where(m => m.Type is not CashMovementType.Opening).Sum(m => m.Amount),
            journalEntries,
            outbox,
            supplierCredits
        );
    }

    public sealed record Operator(Guid UserId, Guid CashRegisterId, Guid CashSessionId);

    public sealed record Effects(
        int SupplierPayments,
        decimal InstallmentPaid,
        int Collections,
        decimal ReceivablePaid,
        int CashMovements,
        decimal CashMovementsAmount,
        int JournalEntries,
        int OutboxMessages,
        int SupplierCredits
    );
}
