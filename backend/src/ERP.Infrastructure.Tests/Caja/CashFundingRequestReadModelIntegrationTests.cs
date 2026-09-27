using System.Text.Json;
using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Access.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-API-02E-D — modelo de lectura sobre PostgreSQL real (mismo fixture que
/// 02E-C): bandeja del cajero (empresa + sucursal activa), "Mis solicitudes", detalle con la regla
/// "solicitante O view" resuelta con el autorizador, acciones derivadas en servidor, nombres en lote
/// y resumen seguro de la intención (sin payload ni huella).
/// </summary>
public sealed partial class CashFundingRequestWorkflowIntegrationTests
{
    private static readonly string[] NoPermissions = [];
    private static readonly string[] ViewOnly = [CajaPermissions.FundingRequestsView];
    private static readonly string[] ViewAndFulfill = [CajaPermissions.FundingRequestsView, CajaPermissions.FundingRequestsFulfill];

    private async Task SeedUsersAsync(ErpDbContext db)
    {
        foreach (var (id, username, first, last) in new[]
                 {
                     (_cashier, "cajero", "Carla", "Cajera"),
                     (_requester, "solicitante", "Sergio", "Compras"),
                     (_stranger, "extrano", "Esteban", "Ajeno"),
                 })
        {
            var user = IdentityUser.Create(username, first, last, $"{username}@example.com", "hash", id);
            typeof(IdentityUser).GetProperty(nameof(IdentityUser.Id))!.SetValue(user, id);
            db.IdentityUsers.Add(user);
        }
        await db.SaveChangesAsync();
    }

    private sealed class FakeAuthorizer(IReadOnlyCollection<string> keys) : IRuntimePermissionAuthorizer
    {
        public Task<bool> IsAuthorizedAsync(string permissionKey, Guid userId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(keys.Contains(permissionKey));
    }

    private async Task<Result<PagedResult<CashFundingRequestListItemDto>>> ListAsync(
        Actor actor, string? status = null, Guid? cashRegisterId = null, Guid? requestedBy = null, int page = 1, int pageSize = 25)
    {
        await using var db = PlainContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new GetCashFundingRequestListHandler(
                new CashFundingRequestRepository(db, company), new CashRegisterRepository(db, company),
                new BusinessPartnerRepository(db), new AccessRepository(db), new FixedCurrentTenant(() => _tenantId),
                new FixedBranch(actor.BranchId))
            .Handle(new GetCashFundingRequestListQuery(status, cashRegisterId, requestedBy, page, pageSize), CancellationToken.None);
    }

    private async Task<Result<PagedResult<CashFundingRequestListItemDto>>> MineAsync(Actor actor, string? status = null, int page = 1, int pageSize = 25)
    {
        await using var db = PlainContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new GetMyCashFundingRequestsHandler(
                new CashFundingRequestRepository(db, company), new CashRegisterRepository(db, company),
                new BusinessPartnerRepository(db), new AccessRepository(db), new FixedCurrentTenant(() => _tenantId),
                company, new FixedUser(actor.UserId))
            .Handle(new GetMyCashFundingRequestsQuery(status, page, pageSize), CancellationToken.None);
    }

    private async Task<Result<CashFundingRequestDto>> DetailAsync(Actor actor, Guid id, IReadOnlyCollection<string> permissions)
    {
        await using var db = PlainContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new GetCashFundingRequestByIdHandler(
                new CashFundingRequestRepository(db, company), new CashSessionRepository(db, company),
                new CashRegisterRepository(db, company), new BusinessPartnerRepository(db), new AccessRepository(db),
                new CompanyBankAccountRepository(db, company), new PaymentMethodRepository(db), new AccountsPayableRepository(db),
                new FakeAuthorizer(permissions), new AllowBranches(_tenantId, actor.CompanyId, actor.UserId, _branchId),
                new FixedCurrentTenant(() => _tenantId), company, new FixedBranch(actor.BranchId), new FixedUser(actor.UserId))
            .Handle(new GetCashFundingRequestByIdQuery(id), CancellationToken.None);
    }

    private Actor Stranger => new(_stranger, _branchId, _companyId);

    // ── 1–3: solicitante ────────────────────────────────────────────────────

    [Fact]
    public async Task RM01_02_solicitante_crea_y_Mis_solicitudes_solo_trae_las_suyas()
    {
        var mine = await PendingAsync(10m);
        var installment = await SeedInstallmentAsync(15m);
        var others = await CreateAsync(Stranger, Payment(installment, 15m));
        others.IsSuccess.Should().BeTrue(others.Error);

        var result = await MineAsync(Requester);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Items.Select(i => i.Id).Should().Equal(mine);
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().RequestedByUserId.Should().Be(_requester);
    }

    [Fact]
    public async Task RM03_solicitante_no_ve_la_solicitud_de_otro_usuario()
    {
        var id = await PendingAsync(10m);

        (await DetailAsync(Stranger, id, NoPermissions)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        // Ni siquiera con el permiso de crear pagos: solo solicitante o `caja.funding-requests.view`.
        (await DetailAsync(Stranger, id, [SupplierPaymentsPermissions.Create])).Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── 4–6: bandeja del cajero y aislamiento ───────────────────────────────

    [Fact]
    public async Task RM04_cajero_con_view_lista_la_sucursal_activa()
    {
        var first = await PendingAsync(10m);
        var installment = await SeedInstallmentAsync(15m);
        var second = (await CreateAsync(Stranger, Payment(installment, 15m))).Value!.Id;

        var result = await ListAsync(Cashier);

        result.Value!.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { first, second });
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task RM05_otra_empresa_no_ve_nada()
    {
        var id = await PendingAsync(10m);
        var outsider = new Actor(_cashier, _branchId, _otherCompanyId);

        (await ListAsync(outsider)).Value!.TotalCount.Should().Be(0);
        (await MineAsync(new Actor(_requester, _branchId, _otherCompanyId))).Value!.TotalCount.Should().Be(0);
        (await DetailAsync(outsider, id, ViewAndFulfill)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await DetailAsync(new Actor(_requester, _branchId, _otherCompanyId), id, NoPermissions))
            .Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task RM06_otra_sucursal_no_ve_la_bandeja_ni_el_detalle()
    {
        var id = await PendingAsync(10m);
        var otherBranchCashier = new Actor(_cashier, _otherBranchId, _companyId);

        (await ListAsync(otherBranchCashier)).Value!.TotalCount.Should().Be(0);
        (await DetailAsync(otherBranchCashier, id, ViewAndFulfill)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── 7–8: detalle y acciones derivadas ───────────────────────────────────

    [Fact]
    public async Task RM07_detalle_para_el_solicitante_solo_permite_cancelar()
    {
        var id = await PendingAsync(10m);

        var detail = await DetailAsync(Requester, id, NoPermissions);

        detail.IsSuccess.Should().BeTrue(detail.Error);
        (detail.Value!.CanCancel, detail.Value.CanFulfill, detail.Value.CanReject).Should().Be((true, false, false));
        // El solicitante ve la suya aunque la sucursal activa sea otra de la empresa.
        (await DetailAsync(new Actor(_requester, _otherBranchId, _companyId), id, NoPermissions)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RM08_detalle_para_el_cajero_acciones_exigen_permiso_y_control_de_la_sesion()
    {
        var id = await PendingAsync(10m);

        var withFulfill = (await DetailAsync(Cashier, id, ViewAndFulfill)).Value!;
        (withFulfill.CanFulfill, withFulfill.CanReject, withFulfill.CanCancel).Should().Be((true, true, false));

        var viewOnly = (await DetailAsync(Cashier, id, ViewOnly)).Value!;
        (viewOnly.CanFulfill, viewOnly.CanReject).Should().Be((false, false));

        // Permisos completos sin controlar la sesión: puede ver, nunca entregar (permiso ≠ ownership).
        var notOwner = await DetailAsync(Stranger, id, ViewAndFulfill);
        notOwner.IsSuccess.Should().BeTrue(notOwner.Error);
        (notOwner.Value!.CanFulfill, notOwner.Value.CanReject).Should().Be((false, false));

        // Sin view ni solicitante: invisible.
        (await DetailAsync(Cashier, id, [CajaPermissions.FundingRequestsFulfill])).Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task RM08b_resuelta_no_ofrece_acciones()
    {
        var id = await PendingAsync(10m);
        (await RejectAsync(Cashier, id)).IsSuccess.Should().BeTrue();

        var detail = (await DetailAsync(Cashier, id, ViewAndFulfill)).Value!;
        (detail.CanFulfill, detail.CanReject, detail.CanCancel).Should().Be((false, false, false));
        (await DetailAsync(Requester, id, NoPermissions)).Value!.CanCancel.Should().BeFalse();
    }

    // ── 12: filtros, estado y paginación ────────────────────────────────────

    [Fact]
    public async Task RM12_filtros_estado_y_paginacion_con_orden_estable()
    {
        var a = await PendingAsync(5m);
        var b = await PendingAsync(6m);
        var c = await PendingAsync(7m);
        (await RejectAsync(Cashier, b)).IsSuccess.Should().BeTrue();

        var pending = (await ListAsync(Cashier, status: "Pending")).Value!;
        pending.TotalCount.Should().Be(2, "el contador de pendientes es el TotalCount filtrado");
        pending.Items.Select(i => i.Id).Should().Equal(c, a);

        var page1 = (await ListAsync(Cashier, pageSize: 2, page: 1)).Value!;
        var page2 = (await ListAsync(Cashier, pageSize: 2, page: 2)).Value!;
        page1.TotalCount.Should().Be(3);
        page1.Items.Concat(page2.Items).Select(i => i.Id).Should().Equal(c, b, a);

        (await ListAsync(Cashier, cashRegisterId: Guid.NewGuid())).Value!.TotalCount.Should().Be(0);
        (await ListAsync(Cashier, requestedBy: _requester)).Value!.TotalCount.Should().Be(3);
        (await ListAsync(Cashier, requestedBy: _stranger)).Value!.TotalCount.Should().Be(0);
        (await MineAsync(Requester, status: "Rejected")).Value!.Items.Select(i => i.Id).Should().Equal(b);
        (await ListAsync(Cashier, status: "NoExiste")).Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    // ── 13–15: forma del DTO ────────────────────────────────────────────────

    [Fact]
    public async Task RM13_dtos_no_exponen_payload_ni_huella()
    {
        foreach (var type in new[] { typeof(CashFundingRequestListItemDto), typeof(CashFundingRequestDto) })
            type.GetProperties().Select(p => p.Name).Should()
                .NotContain(n => n.Contains("Payload") || n.Contains("Hash") || n == "ClientRequestId");

        var id = await PendingAsync(10m);
        string hash;
        await using (var db = PlainContext())
            hash = (await db.CashFundingRequests.AsNoTracking().SingleAsync(r => r.Id == id)).PayloadHash;

        var json = JsonSerializer.Serialize((await DetailAsync(Requester, id, NoPermissions)).Value)
            + JsonSerializer.Serialize((await MineAsync(Requester)).Value);
        json.Should().NotContain(hash).And.NotContain("payment_payload").And.NotContain("PaymentPayload");
    }

    [Fact]
    public async Task RM14_nombres_de_proveedor_caja_y_usuarios_legibles()
    {
        var id = await PendingAsync(10m);
        (await RejectAsync(Cashier, id)).IsSuccess.Should().BeTrue();

        var row = (await ListAsync(Cashier)).Value!.Items.Single();
        (row.SupplierName, row.CashRegisterName, row.RequestedByName, row.ResolvedByName)
            .Should().Be(("Proveedor Test", "Caja Principal", "Sergio Compras", "Carla Cajera"));
        (row.Status, row.CashAmount, row.TotalAmount).Should().Be(("Rejected", 10m, 10m));

        var mineRow = (await MineAsync(Requester)).Value!.Items.Single();
        (mineRow.SupplierName, mineRow.CashRegisterName).Should().Be(("Proveedor Test", "Caja Principal"));

        var detail = (await DetailAsync(Requester, id, NoPermissions)).Value!;
        (detail.SupplierName, detail.CashRegisterName, detail.RequestedByName, detail.ResolvedByName, detail.ResolutionReason)
            .Should().Be(("Proveedor Test", "Caja Principal", "Sergio Compras", "Carla Cajera", "Sin efectivo"));
    }

    [Fact]
    public async Task RM15_detalle_muestra_origen_bancario_y_CxP()
    {
        var id = await PendingAsync(cash: 30m, bank: 20m);
        string documentNumber;
        Guid payableId;
        await using (var db = PlainContext())
        {
            var payable = await db.AccountsPayables.AsNoTracking().SingleAsync();
            (documentNumber, payableId) = (payable.DocumentNumber, payable.Id);
        }

        var detail = (await DetailAsync(Cashier, id, ViewOnly)).Value!;

        (detail.PaymentDate, detail.BranchId, detail.BranchName, detail.CashSessionId)
            .Should().Be((Today, _branchId, "Sucursal 001", _cashSessionId));
        var cash = detail.Sources.Single(s => s.Kind == "Cash");
        (cash.CashRegisterId, cash.CashRegisterName, cash.PaymentMethodName, cash.Amount)
            .Should().Be((_cashRegisterId, "Caja Principal", "Efectivo", 30m));
        var bank = detail.Sources.Single(s => s.Kind == "Bank");
        (bank.CompanyBankAccountId, bank.BankAccountName, bank.PaymentMethodName, bank.Amount, bank.ReferenceNumber, bank.TransactionDate)
            .Should().Be((_bankAccountId, "Banco Pichincha CTE", "Transferencia", 20m, "OP-7788", Today));

        var application = detail.Applications.Single();
        (application.AccountsPayableId, application.DocumentNumber, application.OriginType, application.InstallmentNumber, application.AmountApplied)
            .Should().Be((payableId, documentNumber, "PurchaseInvoice", 1, 50m));
    }

    [Fact]
    public async Task RM_estado_de_la_solicitud_tras_entregar_enlaza_el_pago()
    {
        var id = await PendingAsync(10m);
        var fulfilled = await FulfillAsync(Cashier, id);
        fulfilled.IsSuccess.Should().BeTrue(fulfilled.Error);

        var row = (await ListAsync(Cashier, status: nameof(CashFundingRequestStatus.Fulfilled))).Value!.Items.Single();
        (row.Id, row.SupplierPaymentId, row.ResolvedByName).Should().Be((id, fulfilled.Value!.SupplierPaymentId, "Carla Cajera"));
    }

    // ── 02E-EF: listado de cajas con la sesión abierta (pago directo vs. solicitud) ──

    [Fact]
    public async Task Listado_de_cajas_indica_si_la_sesion_abierta_es_propia_o_ajena_y_quien_la_opera()
    {
        async Task<ERP.Application.Modules.Caja.DTOs.CashRegisterDto> RegisterAs(Guid userId)
        {
            await using var db = PlainContext();
            var company = new FixedCurrentCompany(() => _companyId);
            var sessions = new CashSessionRepository(db, company);
            var result = await new GetCashRegistersByCurrentBranchHandler(
                    new CashRegisterRepository(db, company), new CashRegisterUsageGuard(sessions), sessions,
                    new AccessRepository(db), new FixedCurrentTenant(() => _tenantId), new FixedBranch(_branchId),
                    new FixedUser(userId))
                .Handle(new GetCashRegistersByCurrentBranchQuery(true), CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
            return result.Value!.Single(r => r.Id == _cashRegisterId);
        }

        var asCashier = await RegisterAs(_cashier);
        (asCashier.HasOpenSession, asCashier.OpenSessionControlledByCurrentUser, asCashier.OpenSessionUserName)
            .Should().Be((true, true, "Carla Cajera"));

        var asRequester = await RegisterAs(_requester);
        (asRequester.HasOpenSession, asRequester.OpenSessionControlledByCurrentUser, asRequester.OpenSessionUserName)
            .Should().Be((true, false, "Carla Cajera"));

        (await CloseAsync(100m)).IsSuccess.Should().BeTrue();
        var closed = await RegisterAs(_requester);
        (closed.HasOpenSession, closed.OpenSessionControlledByCurrentUser, closed.OpenSessionUserName)
            .Should().Be((false, false, (string?)null));
    }
}
