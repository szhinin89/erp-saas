using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-6B — confirmación atómica de CxP Inicial sobre PostgreSQL real: una AccountsPayable
/// InitialBalance por fila con su cuota (OriginId = fila del lote, AccountingDate = apertura), todo el
/// lote en una transacción, revalidación contra el estado actual y rollback total ante cualquier
/// fallo. Nunca crea compras ni gastos.
/// </summary>
public sealed partial class ValidateInitialPayablesPostgreSqlTests
{
    private int? _failOnAdd;

    private async Task<Guid> ValidatedBatchAsync(params Dictionary<string, string?>[] rows)
    {
        var batchId = await UploadedBatchAsync(rows);
        var validation = await SendAsync(new ValidateImportBatchCommand(batchId));
        validation.IsSuccess.Should().BeTrue(validation.Error);
        validation.Value!.IssueRows.Should().Be(0);
        return batchId;
    }

    private Task<int> InitialPayablesAsync() =>
        QueryAsync(db => db.AccountsPayables.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == _tenant && p.OriginType == AccountsPayableOriginType.InitialBalance));

    private Task<int> InstallmentsAsync() =>
        QueryAsync(db => db.AccountsPayableInstallments.IgnoreQueryFilters().CountAsync(i => i.TenantId == _tenant));

    private Task<int> OutboxAsync() =>
        QueryAsync(db => db.OutboxMessages.IgnoreQueryFilters().CountAsync(o => o.TenantId == _tenant));

    private async Task AssertNoPurchasesOrExpensesAsync()
    {
        (await QueryAsync(db => db.PurchaseInvoices.IgnoreQueryFilters().CountAsync(p => p.TenantId == _tenant)))
            .Should().Be(0, "nunca se crea una compra histórica ficticia");
        (await QueryAsync(db => db.ExpenseDocuments.IgnoreQueryFilters().CountAsync(e => e.TenantId == _tenant)))
            .Should().Be(0, "nunca se crea un gasto ficticio");
    }

    private async Task AssertRolledBackAsync(Guid batchId, int payablesBefore, int installmentsBefore, int outboxBefore)
    {
        (await InitialPayablesAsync()).Should().Be(payablesBefore, "ninguna CxP parcial");
        (await QueryAsync(db => db.AccountsPayables.CountAsync(p => p.ImportBatchId == batchId))).Should().Be(0);
        (await InstallmentsAsync()).Should().Be(installmentsBefore, "ninguna cuota parcial");
        (await OutboxAsync()).Should().Be(outboxBefore, "sin Outbox parcial");
        var batch = await QueryAsync(db => db.ImportBatches.SingleAsync(b => b.Id == batchId));
        batch.Status.Should().Be(ImportStatus.Validated);
        batch.ImportedRows.Should().Be(0);
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batchId && r.IsImported)))
            .Should().Be(0);
        await AssertNoPurchasesOrExpensesAsync();
    }

    [Fact]
    public async Task Confirma_una_fila_como_CxP_InitialBalance_con_la_fila_como_origen()
    {
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "001-001-000000123", "150.75", docType: "03"));

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        result.Value.ImportedRows.Should().Be(1);
        var row = await QueryAsync(db => db.ImportBatchRows.SingleAsync(r => r.ImportBatchId == batchId));
        var stored = await QueryAsync(db => db.AccountsPayables.Include(p => p.Installments)
            .SingleAsync(p => p.ImportBatchId == batchId));
        stored.OriginType.Should().Be(AccountsPayableOriginType.InitialBalance);
        stored.OriginId.Should().Be(row.Id, "OriginId = ImportBatchRow.Id");
        stored.SupplierId.Should().Be(_supplier.Id);
        stored.DocumentType.Should().Be("03");
        stored.DocumentNumber.Should().Be("001-001-000000123");
        stored.DocumentNumberNormalized.Should().Be("001001000000123");
        stored.IssueDate.Should().Be(new DateOnly(2026, 7, 15));
        stored.AccountingDate.Should().Be(OpeningBalanceDate);
        stored.BranchId.Should().Be(_branch);
        stored.CompanyId.Should().Be(_company);
        stored.Status.Should().Be(AccountsPayableStatus.Pending);
        stored.Installments.Should().ContainSingle(i => i.DueDate == new DateOnly(2026, 9, 15) && i.Amount == 150.75m);
        stored.OutstandingAmount.Should().Be(150.75m);
        stored.PaidAmount.Should().Be(0m);
        stored.RetainedAmount.Should().Be(0m);
        row.IsImported.Should().BeTrue();
        await AssertNoPurchasesOrExpensesAsync();
    }

    [Fact]
    public async Task Confirma_varias_filas_y_varios_proveedores_en_una_transaccion()
    {
        var other = await SeedSupplierAsync("1792146739001", "Proveedor Dos S.A.");
        var batchId = await ValidatedBatchAsync(
            Row(SupplierRuc, "A-1", "10.00"), Row(SupplierRuc, "A-2", "20.00"), Row(other, "A-1", "30.00"));

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ImportedRows.Should().Be(3);
        (await InitialPayablesAsync()).Should().Be(3);
        (await InstallmentsAsync()).Should().Be(3);
        (await QueryAsync(db => db.AccountsPayables.Where(p => p.ImportBatchId == batchId)
            .Select(p => p.SupplierId).Distinct().CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task Confirma_201_filas_en_una_transaccion()
    {
        var rows = Enumerable.Range(1, 201)
            .Select(i => Row(SupplierRuc, $"001-002-{i:D9}", $"{i}.50"))
            .ToArray();
        var batchId = await ValidatedBatchAsync(rows);

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ImportedRows.Should().Be(201);
        (await InitialPayablesAsync()).Should().Be(201);
        (await InstallmentsAsync()).Should().Be(201);
        (await QueryAsync(db => db.AccountsPayableInstallments
                .Where(i => db.AccountsPayables.Any(p => p.Id == i.AccountsPayableId && p.ImportBatchId == batchId))
                .SumAsync(i => i.Amount)))
            .Should().Be(Enumerable.Range(1, 201).Sum(i => i + 0.50m));
        (await QueryAsync(db => db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batchId && r.IsImported)))
            .Should().Be(201);
        await AssertNoPurchasesOrExpensesAsync();
    }

    [Fact]
    public async Task Duplicado_aparecido_despues_del_preview_hace_rollback_total()
    {
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "A-1"), Row(SupplierRuc, "A-2"));
        // Una compra confirmada después del preview ya generó la CxP "a 2" del mismo proveedor.
        await SeedOriginPayableAsync(AccountsPayableOriginType.PurchaseInvoice, "a 2");
        var (payables, installments, outbox) = (await InitialPayablesAsync(), await InstallmentsAsync(), await OutboxAsync());

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya tiene una cuenta por pagar");
        await AssertRolledBackAsync(batchId, payables, installments, outbox);
    }

    [Fact]
    public async Task Proveedor_inactivado_despues_del_preview_hace_rollback_total()
    {
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "A-1"), Row(SupplierRuc, "A-2"));
        var (payables, installments, outbox) = (await InitialPayablesAsync(), await InstallmentsAsync(), await OutboxAsync());
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE master_business_partners SET is_active = false WHERE id = {_supplier.Id}"));

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactivado");
        await AssertRolledBackAsync(batchId, payables, installments, outbox);
    }

    [Fact]
    public async Task Tipo_de_documento_inactivado_despues_del_preview_hace_rollback_total()
    {
        // El contenedor es propio de la clase; 05 no lo usa ningún otro escenario.
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "A-1", docType: "05"));
        var (payables, installments, outbox) = (await InitialPayablesAsync(), await InstallmentsAsync(), await OutboxAsync());
        await QueryAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE global.sri_doc_type SET is_active = false WHERE code = '05'"));
        try
        {
            var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("ya no está activo");
            await AssertRolledBackAsync(batchId, payables, installments, outbox);
        }
        finally
        {
            await QueryAsync(db => db.Database.ExecuteSqlRawAsync(
                "UPDATE global.sri_doc_type SET is_active = true WHERE code = '05'"));
        }
    }

    [Fact]
    public async Task Fallo_intermedio_al_escribir_hace_rollback_total_y_el_lote_sigue_confirmable()
    {
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "A-1"), Row(SupplierRuc, "A-2"), Row(SupplierRuc, "A-3"));
        var (payables, installments, outbox) = (await InitialPayablesAsync(), await InstallmentsAsync(), await OutboxAsync());
        _failOnAdd = 2;

        var result = await SendAsync(new ConfirmImportBatchCommand(batchId));

        _failOnAdd = null;
        result.IsSuccess.Should().BeFalse();
        await AssertRolledBackAsync(batchId, payables, installments, outbox);
        var retry = await SendAsync(new ConfirmImportBatchCommand(batchId));
        retry.IsSuccess.Should().BeTrue(retry.Error);
        (await QueryAsync(db => db.AccountsPayables.CountAsync(p => p.ImportBatchId == batchId))).Should().Be(3);
    }

    [Fact]
    public async Task Listado_y_detalle_muestran_el_saldo_inicial_sin_depender_de_compra_o_gasto()
    {
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "001-001-000000777", "80.00"));
        (await SendAsync(new ConfirmImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();

        var list = await SendAsync(new GetAccountsPayablesListQuery(OriginType: "InitialBalance"));

        list.IsSuccess.Should().BeTrue(list.Error);
        var item = list.Value!.Items.Should().ContainSingle().Subject;
        item.OriginType.Should().Be("InitialBalance");
        item.SupplierName.Should().Be("Proveedor Uno S.A.");
        item.DocumentType.Should().Be("01");
        item.DocumentNumber.Should().Be("001-001-000000777");
        item.IssueDate.Should().Be(new DateOnly(2026, 7, 15));
        item.DueDate.Should().Be(new DateOnly(2026, 9, 15));
        item.TotalAmount.Should().Be(80m);
        item.OutstandingAmount.Should().Be(80m);

        var detail = await SendAsync(new GetAccountsPayableByIdQuery(item.Id));

        detail.IsSuccess.Should().BeTrue(detail.Error);
        detail.Value!.OriginType.Should().Be("InitialBalance");
        detail.Value.AccountingDate.Should().Be(OpeningBalanceDate);
        detail.Value.Installments.Should().ContainSingle(i => i.Amount == 80m && i.OutstandingAmount == 80m);
        await AssertNoPurchasesOrExpensesAsync();
    }

    [Fact]
    public async Task CxP_inicial_confirmada_protege_la_fecha_de_apertura_y_no_admite_anulacion_generica()
    {
        var batchId = await ValidatedBatchAsync(Row(SupplierRuc, "A-1"));
        (await SendAsync(new ConfirmImportBatchCommand(batchId))).IsSuccess.Should().BeTrue();

        // La CxP inicial confirmada cuenta como carga inicial con corte = apertura (sin depender de la
        // fecha vigente que se pasa, que es como se cuentan las CxC iniciales).
        await using (var scope = _services.CreateAsyncScope())
        {
            var constraints = await scope.ServiceProvider.GetRequiredService<IOpeningBalanceConstraintsReader>()
                .GetAsync(null, CancellationToken.None);
            constraints.ConfirmedOpeningDates.Should().Contain(OpeningBalanceDate);
            var company = await scope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Persistence.ErpDbContext>()
                .Companies.SingleAsync(c => c.Id == _company);
            var move = () => company.SetOpeningBalanceDate(new DateOnly(2026, 7, 31), constraints, _user);
            move.Should().Throw<DomainRuleViolationException>("una CxP inicial confirmada protege la fecha de apertura");
        }

        var stored = await QueryAsync(db => db.AccountsPayables.Include(p => p.Installments)
            .SingleAsync(p => p.ImportBatchId == batchId));
        var cancel = () => stored.Cancel(_user);
        cancel.Should().Throw<DomainRuleViolationException>();
    }

    private async Task<string> SeedSupplierAsync(string ruc, string name)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Persistence.ErpDbContext>();
        var partner = ERP.Domain.MasterData.Entities.BusinessPartner.Create(_tenant, "04", ruc, null, name, _user);
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.Add(ERP.Domain.MasterData.Entities.BusinessPartnerRole.Create(
            _tenant, partner.Id, ERP.Domain.MasterData.Enums.RoleType.Supplier, _user));
        await db.SaveChangesAsync();
        return ruc;
    }

    /// <summary>Repositorio real que falla en la N-ésima alta para probar el rollback a mitad del lote.</summary>
    private sealed class FailingPayableRepository(IAccountsPayableRepository inner, Func<int?> failOnAdd)
        : IAccountsPayableRepository
    {
        private int _adds;

        public async Task AddAsync(AccountsPayable payable, CancellationToken ct = default)
        {
            _adds++;
            if (failOnAdd() is { } n && _adds == n)
                throw new InvalidOperationException("Fallo inyectado a mitad del lote.");
            await inner.AddAsync(payable, ct);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);

        public Task<AccountsPayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            inner.GetByIdAsync(tenantId, id, ct);

        public Task<AccountsPayable?> GetByIdForCompanyAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default) =>
            inner.GetByIdForCompanyAsync(tenantId, companyId, id, ct);

        public Task<AccountsPayable?> GetByOriginAsync(Guid tenantId, Guid companyId, AccountsPayableOriginType originType,
            Guid originId, CancellationToken ct = default) =>
            inner.GetByOriginAsync(tenantId, companyId, originType, originId, ct);

        public Task<IReadOnlyDictionary<Guid, (string DocumentNumber, AccountsPayableOriginType OriginType)>> GetDocumentRefsByIdsAsync(
            Guid tenantId, Guid companyId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            inner.GetDocumentRefsByIdsAsync(tenantId, companyId, ids, ct);

        public Task<IReadOnlyDictionary<Guid, (Guid AccountsPayableId, string DocumentNumber, AccountsPayableOriginType OriginType,
            int InstallmentNumber)>> GetInstallmentRefsByIdsAsync(Guid tenantId, Guid companyId,
            IReadOnlyCollection<Guid> installmentIds, CancellationToken ct = default) =>
            inner.GetInstallmentRefsByIdsAsync(tenantId, companyId, installmentIds, ct);

        public Task<(AccountsPayableOriginType OriginType, Guid OriginId)?> GetOriginAsync(Guid tenantId, Guid id,
            CancellationToken ct = default) =>
            inner.GetOriginAsync(tenantId, id, ct);

        public Task<(IReadOnlyList<AccountsPayable> Items, int Total)> SearchAsync(Guid tenantId, Guid companyId,
            AccountsPayableOriginType? originType, AccountsPayableStatus? status, Guid? supplierId, DateOnly? dueDateFrom,
            DateOnly? dueDateTo, string? search, int page, int pageSize, CancellationToken ct = default) =>
            inner.SearchAsync(tenantId, companyId, originType, status, supplierId, dueDateFrom, dueDateTo, search, page,
                pageSize, ct);

        public Task<AccountsPayable?> GetByInstallmentIdAsync(Guid tenantId, Guid installmentId, CancellationToken ct = default) =>
            inner.GetByInstallmentIdAsync(tenantId, installmentId, ct);
    }
}
