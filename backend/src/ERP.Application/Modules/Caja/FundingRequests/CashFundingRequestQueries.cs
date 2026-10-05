using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Caja.FundingRequests;

// ── DTOs (máx. 2: fila de listado + detalle) ─────────────────────────────

/// <summary>ZH-CASH-FUNDING-REQUEST-API-02E-D — fila de la bandeja de caja y de "Mis solicitudes".</summary>
public sealed record CashFundingRequestListItemDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    DateTime RequestedAtUtc,
    Guid RequestedByUserId,
    string RequestedByName,
    Guid CashRegisterId,
    string CashRegisterName,
    decimal CashAmount,
    decimal TotalAmount,
    string Status,
    DateTime? ResolvedAtUtc,
    string? ResolvedByName,
    Guid? SupplierPaymentId
);

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-API-02E-D — detalle de una solicitud de efectivo. Resume la intención de
/// pago guardada (origen efectivo/banco + aplicaciones a CxP) sin exponer jamás el payload crudo ni
/// su huella. <c>CanFulfill/CanReject/CanCancel</c> se derivan en servidor (permiso oficial +
/// control real de la sesión / ser el solicitante); el backend los vuelve a validar al ejecutar.
/// </summary>
public sealed record CashFundingRequestDto(
    Guid Id,
    string Status,
    Guid BranchId,
    string BranchName,
    Guid CashRegisterId,
    string CashRegisterName,
    Guid CashSessionId,
    Guid SupplierId,
    string SupplierName,
    decimal TotalAmount,
    decimal CashAmount,
    Guid RequestedByUserId,
    string RequestedByName,
    DateTime RequestedAtUtc,
    Guid? ResolvedByUserId,
    string? ResolvedByName,
    DateTime? ResolvedAtUtc,
    string? ResolutionReason,
    Guid? SupplierPaymentId,
    DateOnly? PaymentDate,
    string? ReceiptNumber,
    IReadOnlyList<CashFundingRequestSourceLine> Sources,
    IReadOnlyList<CashFundingRequestApplicationLine> Applications,
    bool CanFulfill,
    bool CanReject,
    bool CanCancel
)
{
    /// <summary>
    /// Estado desnudo (sin nombres ni resumen de la intención) — respuesta interna de los comandos.
    /// La API siempre re-lee el detalle completo con <see cref="GetCashFundingRequestByIdQuery"/>.
    /// </summary>
    public static CashFundingRequestDto From(CashFundingRequest r) =>
        CashFundingRequestReadModel.ToDetail(r, CashFundingRequestReadModel.DetailContext.Empty);
}

/// <summary>Origen de fondos del pago solicitado: la caja (<c>Cash</c>) o una cuenta bancaria (<c>Bank</c>).</summary>
public sealed record CashFundingRequestSourceLine(
    string Kind,
    Guid PaymentMethodId,
    string PaymentMethodName,
    Guid? CashRegisterId,
    string? CashRegisterName,
    Guid? CompanyBankAccountId,
    string? BankAccountName,
    decimal Amount,
    DateOnly? TransactionDate,
    string? ReferenceNumber,
    string? CheckNumber
);

/// <summary>Cuota de CxP que el pago solicitado aplicaría (documento y origen Compra/Gasto).</summary>
public sealed record CashFundingRequestApplicationLine(
    Guid AccountsPayableInstallmentId,
    Guid? AccountsPayableId,
    string? DocumentNumber,
    string? OriginType,
    int? InstallmentNumber,
    decimal AmountApplied
);

// ── Queries ──────────────────────────────────────────────────────────────

/// <summary>
/// Bandeja de caja (<c>caja.funding-requests.view</c>): SIEMPRE la empresa operativa + la sucursal
/// activa (validada por <c>BranchScopeBehavior</c>). El contador de pendientes es el
/// <c>TotalCount</c> de esta misma consulta filtrada por <c>Pending</c>.
/// </summary>
public sealed record GetCashFundingRequestListQuery(
    string? Status = null,
    Guid? CashRegisterId = null,
    Guid? RequestedByUserId = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<PagedResult<CashFundingRequestListItemDto>>>, IBranchScopedRequest;

/// <summary>"Mis solicitudes": el solicitante queda forzado al usuario autenticado (nunca del cliente).</summary>
public sealed record GetMyCashFundingRequestsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<PagedResult<CashFundingRequestListItemDto>>>, ICompanyScopedRequest;

/// <summary>
/// Detalle: visible para el solicitante (su propia solicitud, en cualquier sucursal de la empresa)
/// o para quien tenga <c>caja.funding-requests.view</c> sobre la sucursal activa, que debe ser la de
/// la solicitud. Cualquier otro caso → NotFound (fail-closed, no revela existencia).
/// </summary>
public sealed record GetCashFundingRequestByIdQuery(Guid Id)
    : IRequest<Result<CashFundingRequestDto>>,
        ICompanyScopedRequest;

// ── Proyección ───────────────────────────────────────────────────────────

internal static class CashFundingRequestReadModel
{
    public const int MaxPageSize = 100;

    public const string CashSource = "Cash";
    public const string BankSource = "Bank";

    /// <summary>Nombres resueltos en lote para una proyección (nunca N+1).</summary>
    public sealed record DetailContext(
        IReadOnlyDictionary<Guid, string> Suppliers,
        IReadOnlyDictionary<Guid, string> Users,
        IReadOnlyDictionary<Guid, string> CashRegisters,
        IReadOnlyDictionary<Guid, string> Branches,
        IReadOnlyDictionary<Guid, string> BankAccounts,
        IReadOnlyDictionary<Guid, string> PaymentMethods,
        IReadOnlyDictionary<
            Guid,
            (
                Guid AccountsPayableId,
                string DocumentNumber,
                string OriginType,
                int InstallmentNumber
            )
        > Installments,
        CashFundingPaymentSnapshotV1? Snapshot,
        bool CanFulfill,
        bool CanReject,
        bool CanCancel
    )
    {
        private static readonly Dictionary<Guid, string> NoNames = new();

        public static readonly DetailContext Empty = new(
            NoNames,
            NoNames,
            NoNames,
            NoNames,
            NoNames,
            NoNames,
            new Dictionary<Guid, (Guid, string, string, int)>(),
            null,
            false,
            false,
            false
        );
    }

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    /// <summary>Estado de filtro: vacío → sin filtro; valor desconocido → null + inválido (fail-closed en el llamador).</summary>
    public static bool TryParseStatus(string? raw, out CashFundingRequestStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (
            Enum.TryParse<CashFundingRequestStatus>(raw.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && !int.TryParse(raw.Trim(), out _)
        )
        {
            status = parsed;
            return true;
        }
        return false;
    }

    /// <summary>Intención guardada, o null si no es legible (el detalle nunca se rompe por esto).</summary>
    public static CashFundingPaymentSnapshotV1? TryReadSnapshot(CashFundingRequest r)
    {
        try
        {
            return CashFundingPaymentSnapshot.Deserialize(r.PaymentPayload, r.PayloadVersion);
        }
        catch (Exception ex)
            when (ex is InvalidOperationException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static CashFundingRequestListItemDto ToListItem(
        CashFundingRequest r,
        IReadOnlyDictionary<Guid, string> suppliers,
        IReadOnlyDictionary<Guid, string> users,
        IReadOnlyDictionary<Guid, string> cashRegisters
    ) =>
        new(
            r.Id,
            r.SupplierId,
            suppliers.GetValueOrDefault(r.SupplierId, string.Empty),
            r.RequestedAtUtc,
            r.RequestedByUserId,
            users.GetValueOrDefault(r.RequestedByUserId, string.Empty),
            r.CashRegisterId,
            cashRegisters.GetValueOrDefault(r.CashRegisterId, string.Empty),
            r.CashAmount,
            r.TotalAmount,
            r.Status.ToString(),
            r.ResolvedAtUtc,
            r.ResolvedByUserId is { } resolvedBy ? users.GetValueOrDefault(resolvedBy) : null,
            r.SupplierPaymentId
        );

    public static CashFundingRequestDto ToDetail(CashFundingRequest r, DetailContext ctx)
    {
        var snapshot = ctx.Snapshot;
        var sources = snapshot is null
            ? []
            : snapshot
                .MethodLines.Select(m => new CashFundingRequestSourceLine(
                    m.CashRegisterId is not null ? CashSource : BankSource,
                    m.PaymentMethodId,
                    ctx.PaymentMethods.GetValueOrDefault(m.PaymentMethodId, string.Empty),
                    m.CashRegisterId,
                    m.CashRegisterId is { } cr ? ctx.CashRegisters.GetValueOrDefault(cr) : null,
                    m.CompanyBankAccountId,
                    m.CompanyBankAccountId is { } ba
                        ? ctx.BankAccounts.GetValueOrDefault(ba)
                        : null,
                    m.Amount,
                    m.TransactionDate,
                    m.ReferenceNumber,
                    m.CheckNumber
                ))
                .ToList();
        var applications = snapshot is null
            ? []
            : snapshot
                .ApplicationLines.Select(a =>
                {
                    var found = ctx.Installments.TryGetValue(
                        a.AccountsPayableInstallmentId,
                        out var info
                    );
                    return new CashFundingRequestApplicationLine(
                        a.AccountsPayableInstallmentId,
                        found ? info.AccountsPayableId : null,
                        found ? info.DocumentNumber : null,
                        found ? info.OriginType : null,
                        found ? info.InstallmentNumber : null,
                        a.AmountApplied
                    );
                })
                .ToList();

        return new CashFundingRequestDto(
            r.Id,
            r.Status.ToString(),
            r.BranchId,
            ctx.Branches.GetValueOrDefault(r.BranchId, string.Empty),
            r.CashRegisterId,
            ctx.CashRegisters.GetValueOrDefault(r.CashRegisterId, string.Empty),
            r.CashSessionId,
            r.SupplierId,
            ctx.Suppliers.GetValueOrDefault(r.SupplierId, string.Empty),
            r.TotalAmount,
            r.CashAmount,
            r.RequestedByUserId,
            ctx.Users.GetValueOrDefault(r.RequestedByUserId, string.Empty),
            r.RequestedAtUtc,
            r.ResolvedByUserId,
            r.ResolvedByUserId is { } resolvedBy ? ctx.Users.GetValueOrDefault(resolvedBy) : null,
            r.ResolvedAtUtc,
            r.ResolutionReason,
            r.SupplierPaymentId,
            snapshot?.PaymentDate,
            snapshot?.ReceiptNumber,
            sources,
            applications,
            ctx.CanFulfill,
            ctx.CanReject,
            ctx.CanCancel
        );
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(
        IAccessRepository access,
        IEnumerable<Guid> ids,
        CancellationToken ct
    )
    {
        var distinct = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
            return new Dictionary<Guid, string>();
        return (await access.GetUsersByIdsAsync(distinct, ct)).ToDictionary(
            u => u.Id,
            u => u.FullName
        );
    }

    public static IEnumerable<Guid> UserIdsOf(IEnumerable<CashFundingRequest> items) =>
        items.SelectMany(r =>
            r.ResolvedByUserId is { } resolvedBy
                ? new[] { r.RequestedByUserId, resolvedBy }
                : new[] { r.RequestedByUserId }
        );
}

// ── Handlers ─────────────────────────────────────────────────────────────

public sealed class GetCashFundingRequestListHandler
    : IRequestHandler<
        GetCashFundingRequestListQuery,
        Result<PagedResult<CashFundingRequestListItemDto>>
    >
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IBusinessPartnerRepository _partners;
    private readonly IAccessRepository _access;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashFundingRequestListHandler(
        ICashFundingRequestRepository requests,
        ICashRegisterRepository cashRegisters,
        IBusinessPartnerRepository partners,
        IAccessRepository access,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _requests = requests;
        _cashRegisters = cashRegisters;
        _partners = partners;
        _access = access;
        _t = t;
        _b = b;
    }

    public async Task<Result<PagedResult<CashFundingRequestListItemDto>>> Handle(
        GetCashFundingRequestListQuery q,
        CancellationToken ct
    )
    {
        if (_b.BranchId == Guid.Empty)
            return Result<PagedResult<CashFundingRequestListItemDto>>.ValidationFailure(
                "Sucursal activa requerida."
            );
        if (!CashFundingRequestReadModel.TryParseStatus(q.Status, out var status))
            return Result<PagedResult<CashFundingRequestListItemDto>>.ValidationFailure(
                "Estado de solicitud no válido."
            );

        var (page, pageSize) = CashFundingRequestReadModel.Normalize(q.Page, q.PageSize);
        var (items, total) = await _requests.SearchAsync(
            _t.TenantId,
            _b.BranchId,
            q.RequestedByUserId,
            status,
            q.CashRegisterId,
            page,
            pageSize,
            ct
        );

        var suppliers = await _partners.GetNamesByIdsAsync(
            items.Select(x => x.SupplierId).Distinct(),
            ct
        );
        var users = await CashFundingRequestReadModel.UserNamesAsync(
            _access,
            CashFundingRequestReadModel.UserIdsOf(items),
            ct
        );
        var registers =
            items.Count == 0
                ? new Dictionary<Guid, string>()
                : (
                    await _cashRegisters.GetByBranchAsync(_t.TenantId, _b.BranchId, null, ct)
                ).ToDictionary(x => x.Id, x => x.Name);

        var dtos = items
            .Select(r => CashFundingRequestReadModel.ToListItem(r, suppliers, users, registers))
            .ToList();
        return Result<PagedResult<CashFundingRequestListItemDto>>.Success(
            new PagedResult<CashFundingRequestListItemDto>(dtos, page, pageSize, total)
        );
    }
}

public sealed class GetMyCashFundingRequestsHandler
    : IRequestHandler<
        GetMyCashFundingRequestsQuery,
        Result<PagedResult<CashFundingRequestListItemDto>>
    >
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IBusinessPartnerRepository _partners;
    private readonly IAccessRepository _access;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public GetMyCashFundingRequestsHandler(
        ICashFundingRequestRepository requests,
        ICashRegisterRepository cashRegisters,
        IBusinessPartnerRepository partners,
        IAccessRepository access,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _requests = requests;
        _cashRegisters = cashRegisters;
        _partners = partners;
        _access = access;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PagedResult<CashFundingRequestListItemDto>>> Handle(
        GetMyCashFundingRequestsQuery q,
        CancellationToken ct
    )
    {
        var (page, pageSize) = CashFundingRequestReadModel.Normalize(q.Page, q.PageSize);
        // Fail-closed: sin usuario autenticado no hay "mis" solicitudes.
        if (_u.UserId == Guid.Empty)
            return Result<PagedResult<CashFundingRequestListItemDto>>.Success(
                new([], page, pageSize, 0)
            );
        if (!CashFundingRequestReadModel.TryParseStatus(q.Status, out var status))
            return Result<PagedResult<CashFundingRequestListItemDto>>.ValidationFailure(
                "Estado de solicitud no válido."
            );

        var (items, total) = await _requests.SearchAsync(
            _t.TenantId,
            branchId: null,
            requestedByUserId: _u.UserId,
            status,
            cashRegisterId: null,
            page,
            pageSize,
            ct
        );

        var suppliers = await _partners.GetNamesByIdsAsync(
            items.Select(x => x.SupplierId).Distinct(),
            ct
        );
        var users = await CashFundingRequestReadModel.UserNamesAsync(
            _access,
            CashFundingRequestReadModel.UserIdsOf(items),
            ct
        );
        var registers =
            items.Count == 0
                ? new Dictionary<Guid, string>()
                : (
                    await _cashRegisters.GetAllByCompanyAsync(
                        _t.TenantId,
                        _c.CompanyId,
                        null,
                        null,
                        ct
                    )
                ).ToDictionary(x => x.Id, x => x.Name);

        var dtos = items
            .Select(r => CashFundingRequestReadModel.ToListItem(r, suppliers, users, registers))
            .ToList();
        return Result<PagedResult<CashFundingRequestListItemDto>>.Success(
            new PagedResult<CashFundingRequestListItemDto>(dtos, page, pageSize, total)
        );
    }
}

public sealed class GetCashFundingRequestByIdHandler
    : IRequestHandler<GetCashFundingRequestByIdQuery, Result<CashFundingRequestDto>>
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashSessionRepository _sessions;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IBusinessPartnerRepository _partners;
    private readonly IAccessRepository _access;
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly IPaymentMethodRepository _paymentMethods;
    private readonly IAccountsPayableRepository _payables;
    private readonly IRuntimePermissionAuthorizer _authorizer;
    private readonly IBranchAccessGuard _branchAccess;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public GetCashFundingRequestByIdHandler(
        ICashFundingRequestRepository requests,
        ICashSessionRepository sessions,
        ICashRegisterRepository cashRegisters,
        IBusinessPartnerRepository partners,
        IAccessRepository access,
        ICompanyBankAccountRepository bankAccounts,
        IPaymentMethodRepository paymentMethods,
        IAccountsPayableRepository payables,
        IRuntimePermissionAuthorizer authorizer,
        IBranchAccessGuard branchAccess,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _requests = requests;
        _sessions = sessions;
        _cashRegisters = cashRegisters;
        _partners = partners;
        _access = access;
        _bankAccounts = bankAccounts;
        _paymentMethods = paymentMethods;
        _payables = payables;
        _authorizer = authorizer;
        _branchAccess = branchAccess;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    private Task<bool> HasPermissionAsync(string key, CancellationToken ct) =>
        _authorizer.IsAuthorizedAsync(key, _u.UserId, _u.Role ?? string.Empty, ct);

    /// <summary>
    /// Acceso del cajero: sucursal activa = la de la solicitud, con acceso real a esa sucursal, y el
    /// permiso oficial. Los permisos nunca reemplazan el ownership: esto solo habilita VER.
    /// </summary>
    private async Task<bool> CanViewAsCashierAsync(CashFundingRequest r, CancellationToken ct) =>
        _b.BranchId != Guid.Empty
        && r.BranchId == _b.BranchId
        && (await _branchAccess.RequireBranchAsync(r.BranchId, ct)).IsSuccess
        && await HasPermissionAsync(CajaPermissions.FundingRequestsView, ct);

    public async Task<Result<CashFundingRequestDto>> Handle(
        GetCashFundingRequestByIdQuery q,
        CancellationToken ct
    )
    {
        var userId = _u.UserId;
        var r = await _requests.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (r is null || userId == Guid.Empty)
            return Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound);

        var isRequester = r.RequestedByUserId == userId;
        if (!isRequester && !await CanViewAsCashierAsync(r, ct))
            return Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound);

        // Acciones: mismas condiciones que re-validan los comandos (02E-C). Entregar/rechazar exige
        // el permiso Y controlar la sesión abierta de la solicitud en la sucursal activa.
        var canResolve = false;
        if (r.IsPending && r.BranchId == _b.BranchId)
        {
            var session = await _sessions.GetByIdAsync(_t.TenantId, r.CashSessionId, ct);
            canResolve =
                session is not null
                && session.IsControlledBy(userId)
                && await HasPermissionAsync(CajaPermissions.FundingRequestsFulfill, ct);
        }
        var canCancel = r.IsPending && isRequester;

        var snapshot = CashFundingRequestReadModel.TryReadSnapshot(r);
        var userIds = CashFundingRequestReadModel.UserIdsOf([r]);
        var suppliers = await _partners.GetNamesByIdsAsync([r.SupplierId], ct);
        var users = await CashFundingRequestReadModel.UserNamesAsync(_access, userIds, ct);

        var register = await _cashRegisters.GetByIdAsync(_t.TenantId, r.CashRegisterId, ct);
        var registers = new Dictionary<Guid, string>();
        var branches = new Dictionary<Guid, string>();
        if (register is not null)
        {
            registers[register.Id] = register.Name;
            // La caja pertenece a la sucursal de la solicitud (validado al crear): su Branch ya viene cargada.
            if (register.BranchId == r.BranchId && register.Branch is not null)
                branches[r.BranchId] = register.Branch.Name;
        }

        IReadOnlyDictionary<Guid, string> bankAccounts = new Dictionary<Guid, string>();
        IReadOnlyDictionary<Guid, string> methods = new Dictionary<Guid, string>();
        var installments = new Dictionary<Guid, (Guid, string, string, int)>();
        if (snapshot is not null)
        {
            if (snapshot.MethodLines.Any(m => m.CompanyBankAccountId is not null))
                bankAccounts = (
                    await _bankAccounts.GetListAsync(_t.TenantId, null, ct)
                ).ToDictionary(x => x.Id, x => x.DisplayName);
            if (snapshot.MethodLines.Count > 0)
                methods = (
                    await _paymentMethods.ListAsync(_t.TenantId, onlyActive: false, ct)
                ).ToDictionary(x => x.Id, x => x.Name);
            var installmentIds = snapshot
                .ApplicationLines.Select(a => a.AccountsPayableInstallmentId)
                .Distinct()
                .ToList();
            foreach (
                var (id, info) in await _payables.GetInstallmentRefsByIdsAsync(
                    _t.TenantId,
                    _c.CompanyId,
                    installmentIds,
                    ct
                )
            )
                installments[id] = (
                    info.AccountsPayableId,
                    info.DocumentNumber,
                    info.OriginType.ToString(),
                    info.InstallmentNumber
                );
        }

        var ctx = new CashFundingRequestReadModel.DetailContext(
            suppliers,
            users,
            registers,
            branches,
            bankAccounts,
            methods,
            installments,
            snapshot,
            canResolve,
            canResolve,
            canCancel
        );
        return Result<CashFundingRequestDto>.Success(CashFundingRequestReadModel.ToDetail(r, ctx));
    }
}
