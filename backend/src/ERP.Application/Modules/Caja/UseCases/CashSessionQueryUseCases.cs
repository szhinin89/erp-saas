using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Queries ────────────────────────────────────────────────────────────

public sealed record GetCashSessionByIdQuery(Guid Id)
    : IRequest<Result<CashSessionDto>>,
        IBranchScopedRequest;

public sealed record GetCashSessionListQuery(
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<CashSessionListResponse>>, IBranchScopedRequest;

public sealed record GetMyCashSessionQuery()
    : IRequest<Result<CashSessionDto?>>,
        IBranchScopedRequest;

/// <summary>
/// CASH-SESSION-COLLECTION-SUMMARY-01 — resumen informativo de ventas/cobros del turno, separado
/// del efectivo físico de <see cref="CashSessionDto"/> (TotalIncome/CurrentBalance no cambian).
/// </summary>
public sealed record GetCashSessionCollectionSummaryQuery(Guid CashSessionId)
    : IRequest<Result<CashSessionCollectionSummaryDto>>,
        IBranchScopedRequest;

// ── Handlers ───────────────────────────────────────────────────────────

public sealed class GetCashSessionByIdHandler
    : IRequestHandler<GetCashSessionByIdQuery, Result<CashSessionDto>>
{
    private readonly ICashSessionRepository _repo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ICashRegisterRepository _crRepo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashSessionByIdHandler(
        ICashSessionRepository repo,
        IEmissionPointRepository epRepo,
        ICashRegisterRepository crRepo,
        IAccessRepository accessRepo,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _repo = repo;
        _epRepo = epRepo;
        _crRepo = crRepo;
        _accessRepo = accessRepo;
        _t = t;
        _b = b;
    }

    public async Task<Result<CashSessionDto>> Handle(
        GetCashSessionByIdQuery q,
        CancellationToken ct
    )
    {
        var session = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (session is null || session.BranchId != _b.BranchId)
            return Result<CashSessionDto>.NotFound("Sesión de caja no encontrada.");

        var ep = await _epRepo.GetByIdAsync(session.EmissionPointId, _t.TenantId, ct);
        var register = await _crRepo.GetByIdAsync(_t.TenantId, session.CashRegisterId, ct);

        // TREASURY-CASH-MANUAL-MOVEMENTS-01 — un solo query para todos los CreatedBy distintos de
        // los movimientos ("usuario" en el listado de Movimientos), nunca N+1 por movimiento.
        var userIds = session.Movements.Select(m => m.CreatedBy).Distinct().ToList();
        var userNameById = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _accessRepo.GetUsersByIdsAsync(userIds, ct)).ToDictionary(u => u.Id, u => u.FullName);

        return Result<CashSessionDto>.Success(
            CajaMapper.ToDto(
                session,
                ep?.EmissionType.ToString(),
                (register?.DefaultWarehouseId, register?.DefaultWarehouse?.Name),
                (register?.DefaultCustomerId, register?.DefaultCustomer?.Name.LegalName),
                userNameById
            )
        );
    }
}

/// <summary>
/// CASH-SESSION-LIST-SUMMARY-01 — arma el listado de turnos con información operativa (cajero,
/// facturas, cobros por forma) además del efectivo físico ya existente. Un solo query extra por
/// página para SalesInvoice+SalesInvoicePayment (todas las sesiones de la página a la vez, vía
/// <see cref="ISalesInvoiceRepository.GetCollectionSummaryByCashSessionsAsync"/>), uno para el
/// catálogo de PaymentMethod y uno para los nombres de usuario (cajero/cerrado por) — nunca N+1
/// por fila. Nunca toca CashSession/CashMovement más allá de leerlos (ya lo hacía antes).
/// </summary>
public sealed class GetCashSessionListHandler
    : IRequestHandler<GetCashSessionListQuery, Result<CashSessionListResponse>>
{
    private readonly ICashSessionRepository _repo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashSessionListHandler(
        ICashSessionRepository repo,
        ISalesInvoiceRepository invoiceRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IAccessRepository accessRepo,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _accessRepo = accessRepo;
        _t = t;
        _b = b;
    }

    public async Task<Result<CashSessionListResponse>> Handle(
        GetCashSessionListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            _b.BranchId,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );

        if (items.Count == 0)
            return Result<CashSessionListResponse>.Success(
                new CashSessionListResponse([], total, q.PageNumber, q.PageSize)
            );

        var sessionIds = items.Select(s => s.Id).ToList();
        var rows = await _invoiceRepo.GetCollectionSummaryByCashSessionsAsync(
            _t.TenantId,
            _b.BranchId,
            sessionIds,
            ct
        );
        var rowsBySession = rows
            .GroupBy(r => r.CashSessionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SalesInvoiceCashSessionPaymentRow>)g.ToList());

        var methods = await _paymentMethodRepo.ListAsync(_t.TenantId, onlyActive: false, ct);
        var methodById = methods.ToDictionary(m => m.Id, m => m);

        var userIds = items.Select(s => s.UserId)
            .Concat(items.Where(s => s.ClosedBy.HasValue).Select(s => s.ClosedBy!.Value))
            .Distinct()
            .ToList();
        var users = await _accessRepo.GetUsersByIdsAsync(userIds, ct);
        var userNameById = users.ToDictionary(u => u.Id, u => u.FullName);

        var emptyRows = Array.Empty<SalesInvoiceCashSessionPaymentRow>();
        var dtos = items
            .Select(s => CajaMapper.ToListDto(
                s,
                userNameById.GetValueOrDefault(s.UserId),
                s.ClosedBy.HasValue ? userNameById.GetValueOrDefault(s.ClosedBy.Value) : null,
                rowsBySession.GetValueOrDefault(s.Id, emptyRows),
                methodById
            ))
            .ToList();

        return Result<CashSessionListResponse>.Success(
            new CashSessionListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}

public sealed class GetMyCashSessionHandler
    : IRequestHandler<GetMyCashSessionQuery, Result<CashSessionDto?>>
{
    private readonly ICashSessionRepository _repo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ICashRegisterRepository _crRepo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public GetMyCashSessionHandler(
        ICashSessionRepository repo,
        IEmissionPointRepository epRepo,
        ICashRegisterRepository crRepo,
        IAccessRepository accessRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _epRepo = epRepo;
        _crRepo = crRepo;
        _accessRepo = accessRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<CashSessionDto?>> Handle(GetMyCashSessionQuery q, CancellationToken ct)
    {
        var session = await _repo.GetOpenByUserAsync(_t.TenantId, _u.UserId, ct);
        if (session is null)
            return Result<CashSessionDto?>.Success(null);

        var full = await _repo.GetByIdAsync(_t.TenantId, session.Id, ct);
        if (full is null)
            return Result<CashSessionDto?>.Success(null);

        var ep = await _epRepo.GetByIdAsync(full.EmissionPointId, _t.TenantId, ct);
        var register = await _crRepo.GetByIdAsync(_t.TenantId, full.CashRegisterId, ct);

        var userIds = full.Movements.Select(m => m.CreatedBy).Distinct().ToList();
        var userNameById = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _accessRepo.GetUsersByIdsAsync(userIds, ct)).ToDictionary(u => u.Id, u => u.FullName);

        return Result<CashSessionDto?>.Success(
            CajaMapper.ToDto(
                full,
                ep?.EmissionType.ToString(),
                (register?.DefaultWarehouseId, register?.DefaultWarehouse?.Name),
                (register?.DefaultCustomerId, register?.DefaultCustomer?.Name.LegalName),
                userNameById
            )
        );
    }
}

/// <summary>
/// CASH-SESSION-COLLECTION-SUMMARY-01/UX-02 — arma el resumen de ventas/cobros del turno leyendo
/// exclusivamente SalesInvoice+SalesInvoicePayment (proyección liviana, sin N+1), el catálogo de
/// PaymentMethod (una sola consulta, tabla pequeña) para resolver IsCreditAllowed/DetailType/
/// Destino, y — solo si hubo pagos por Transferencia en el turno — el catálogo de bancos y cuentas
/// bancarias de la empresa (dos consultas más, igual de pequeñas) para mostrar banco real +
/// cuenta enmascarada en el detalle. Nunca toca CashSession/CashMovement, que siguen siendo la
/// única fuente de efectivo físico; nunca recalcula CashApplied/PhysicalCashApplied ni el posting.
/// </summary>
public sealed class GetCashSessionCollectionSummaryHandler
    : IRequestHandler<GetCashSessionCollectionSummaryQuery, Result<CashSessionCollectionSummaryDto>>
{
    private readonly ICashSessionRepository _cashRepo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly ICompanyBankAccountRepository _bankAccountRepo;
    private readonly IBankRepository _bankRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashSessionCollectionSummaryHandler(
        ICashSessionRepository cashRepo,
        ISalesInvoiceRepository invoiceRepo,
        IPaymentMethodRepository paymentMethodRepo,
        ICompanyBankAccountRepository bankAccountRepo,
        IBankRepository bankRepo,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _cashRepo = cashRepo;
        _invoiceRepo = invoiceRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _bankAccountRepo = bankAccountRepo;
        _bankRepo = bankRepo;
        _t = t;
        _b = b;
    }

    public async Task<Result<CashSessionCollectionSummaryDto>> Handle(
        GetCashSessionCollectionSummaryQuery q,
        CancellationToken ct
    )
    {
        // Fail-closed: misma verificación tenant+branch que GetCashSessionByIdHandler — sin esto,
        // cualquier CashSessionId adivinado de otra sucursal filtraría sus ventas.
        var session = await _cashRepo.GetByIdAsync(_t.TenantId, q.CashSessionId, ct);
        if (session is null || session.BranchId != _b.BranchId)
            return Result<CashSessionCollectionSummaryDto>.NotFound("Sesión de caja no encontrada.");

        var rows = await _invoiceRepo.GetCollectionSummaryByCashSessionAsync(
            _t.TenantId,
            _b.BranchId,
            session.Id,
            ct
        );

        if (rows.Count == 0)
            return Result<CashSessionCollectionSummaryDto>.Success(
                new CashSessionCollectionSummaryDto(0, 0m, 0m, 0m, [])
            );

        // Catálogo completo (activos e inactivos: una forma deshabilitada después de la venta no
        // debe desaparecer del histórico del turno) — tabla pequeña, un solo query. AUDIT-CASH-
        // SESSION-COLLECTION-SUMMARY-MISMATCH-01: un PaymentMethodId del pago puede no existir ya
        // en este catálogo (huérfano/legacy) — GetValueOrDefault(null) maneja ese caso sin fallar.
        var methods = await _paymentMethodRepo.ListAsync(_t.TenantId, onlyActive: false, ct);
        var methodById = methods.ToDictionary(m => m.Id, m => m);

        var invoiceCount = rows.Select(r => r.InvoiceId).Distinct().Count();
        var totalInvoiced = rows.GroupBy(r => r.InvoiceId).Sum(g => g.First().GrandTotal);

        var totalCollected = rows
            .Where(r => !IsCreditAllowed(r.PaymentMethodId, methodById))
            .Sum(r => r.Amount);
        var totalCredit = rows
            .Where(r => IsCreditAllowed(r.PaymentMethodId, methodById))
            .Sum(r => r.Amount);

        // "Completa" vs "Mixta": cuenta las LÍNEAS de pago de la factura (cualquier forma), no las
        // formas distintas — 2 pagos en Efectivo también es una factura "mixta" a nivel de pagos.
        var paymentLineCountByInvoice = rows
            .GroupBy(r => r.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Solo se resuelven banco/cuenta si hubo al menos una Transferencia — evita 2 queries
        // extra en el caso común (turno sin transferencias).
        var transferBankAccountIds = rows
            .Where(r => r.TransferCompanyBankAccountId is not null)
            .Select(r => r.TransferCompanyBankAccountId!.Value)
            .Distinct()
            .ToList();
        var bankAccountById = new Dictionary<Guid, CompanyBankAccount>();
        var bankNameById = new Dictionary<Guid, string>();
        if (transferBankAccountIds.Count > 0)
        {
            var bankAccounts = await _bankAccountRepo.GetListAsync(_t.TenantId, isActive: null, ct);
            bankAccountById = bankAccounts
                .Where(a => transferBankAccountIds.Contains(a.Id))
                .ToDictionary(a => a.Id, a => a);
            var banks = await _bankRepo.ListAsync(_t.TenantId, onlyActive: false, cancellationToken: ct);
            bankNameById = banks.ToDictionary(bk => bk.Id, bk => bk.Name);
        }

        var byMethod = rows
            .GroupBy(r => r.PaymentMethodId)
            .Select(g =>
            {
                methodById.TryGetValue(g.Key, out var method);
                return new CashSessionCollectionByMethodDto(
                    g.Key,
                    g.First().PaymentMethodCode,
                    g.First().PaymentMethodName,
                    method?.IsCreditAllowed ?? false,
                    g.Select(r => r.InvoiceId).Distinct().Count(),
                    g.Count(),
                    g.Sum(r => r.Amount),
                    ResolveDestinationLabel(method),
                    g.OrderByDescending(r => r.AuthorizedAt)
                        .Select(r => MapDetail(
                            r,
                            paymentLineCountByInvoice[r.InvoiceId] > 1,
                            bankAccountById,
                            bankNameById
                        ))
                        .ToList()
                );
            })
            .OrderBy(m => PaymentMethodDisplayOrder.Rank(methodById.GetValueOrDefault(m.PaymentMethodId)))
            .ThenBy(m => m.PaymentMethodName)
            .ToList();

        return Result<CashSessionCollectionSummaryDto>.Success(
            new CashSessionCollectionSummaryDto(
                invoiceCount,
                totalInvoiced,
                totalCollected,
                totalCredit,
                byMethod
            )
        );
    }

    private static bool IsCreditAllowed(
        Guid paymentMethodId,
        IReadOnlyDictionary<Guid, PaymentMethod> methodById
    ) => methodById.TryGetValue(paymentMethodId, out var m) && m.IsCreditAllowed;

    private static CashSessionCollectionDetailDto MapDetail(
        SalesInvoiceCashSessionPaymentRow r,
        bool isMixedPayment,
        IReadOnlyDictionary<Guid, CompanyBankAccount> bankAccountById,
        IReadOnlyDictionary<Guid, string> bankNameById
    )
    {
        string? destinationBankName = null;
        string? destinationAccountMasked = null;
        if (r.TransferCompanyBankAccountId is { } bankAccountId)
        {
            if (bankAccountById.TryGetValue(bankAccountId, out var account))
            {
                destinationBankName = bankNameById.GetValueOrDefault(account.BankId);
                destinationAccountMasked = $"{account.DisplayName} ({MaskAccountNumber(account.AccountNumber)})";
            }
        }
        // Legacy: transferencias autorizadas antes de SALES-TRANSFER-BANK-ACCOUNT-01, sin
        // CompanyBankAccountId — el único dato real disponible es el texto libre histórico.
        destinationBankName ??= r.TransferLegacyBankName;

        return new CashSessionCollectionDetailDto(
            r.InvoiceId,
            r.InvoiceNumber,
            r.AuthorizedAt,
            r.CustomerName,
            r.GrandTotal,
            r.Amount,
            isMixedPayment,
            r.TransferReceiptNumber ?? r.Reference,
            destinationBankName,
            destinationAccountMasked,
            r.TransferReceiptNumber,
            r.TransferDate
        );
    }

    /// <summary>Últimos 4 dígitos visibles — nunca expone el número completo en un resumen operativo.</summary>
    private static string MaskAccountNumber(string accountNumber)
    {
        var trimmed = accountNumber.Trim();
        return trimmed.Length <= 4 ? trimmed : $"****{trimmed[^4..]}";
    }

    /// <summary>
    /// Etiqueta contable genérica (categoría, no cuenta real) — ver doc del DTO. Reutiliza los
    /// mismos flags que <see cref="DisplayRank"/>, nunca una segunda fuente de verdad.
    /// </summary>
    private static string ResolveDestinationLabel(PaymentMethod? method)
    {
        if (method is null)
            return "Sin clasificar";
        if (method.IsCreditAllowed)
            return "Cuentas por Cobrar";
        return method.DetailType switch
        {
            PaymentMethodDetailType.Transfer => "Cuenta bancaria",
            PaymentMethodDetailType.Card or PaymentMethodDetailType.Check => "Cuenta configurada",
            PaymentMethodDetailType.None when method.AffectsPhysicalCash => "Caja física",
            _ => "Sin clasificar",
        };
    }
}
