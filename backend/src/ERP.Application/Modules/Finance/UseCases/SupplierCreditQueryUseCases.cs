using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>P0-02 Fase 11 — resultado paginado de <c>GetSupplierCreditListQuery</c>.</summary>
public sealed record SupplierCreditListResultDto(
    IReadOnlyList<SupplierCreditListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetSupplierCreditByIdQuery(Guid Id)
    : IRequest<Result<SupplierCreditDto>>,
        ICompanyScopedRequest;

/// <summary>ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — filtros opcionales, todos ejecutados en BD.</summary>
public sealed record GetSupplierCreditListQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? SupplierId = null,
    SupplierCreditSourceType? SourceType = null,
    bool? IsOpen = null
) : IRequest<Result<SupplierCreditListResultDto>>, ICompanyScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — detalle enriquecido con un número FIJO de consultas por
/// crédito (nunca por movimiento): crédito + movimientos, origen (2 + zona horaria), nombre del
/// proveedor, CxP destino de las aplicaciones (1), transacciones de reembolso (1), autores (1) y,
/// solo si hay reembolsos, el catálogo oficial de formas de pago (1, incluye inactivas — son
/// históricas) para el nombre legible (ZH-SUPPLIER-BALANCES-UX-02D-E).
/// </summary>
public sealed class GetSupplierCreditByIdHandler
    : IRequestHandler<GetSupplierCreditByIdQuery, Result<SupplierCreditDto>>
{
    private readonly ISupplierCreditRepository _credits;
    private readonly ISupplierCreditRefundTransactionRepository _refunds;
    private readonly IAccountsPayableRepository _payables;
    private readonly IBusinessPartnerRepository _partners;
    private readonly IAccessRepository _access;
    private readonly ICompanyRepository _companies;
    private readonly IPaymentMethodRepository _paymentMethods;
    private readonly ICurrentTenant _t;

    public GetSupplierCreditByIdHandler(
        ISupplierCreditRepository credits,
        ISupplierCreditRefundTransactionRepository refunds,
        IAccountsPayableRepository payables,
        IBusinessPartnerRepository partners,
        IAccessRepository access,
        ICompanyRepository companies,
        IPaymentMethodRepository paymentMethods,
        ICurrentTenant t
    )
    {
        _credits = credits;
        _refunds = refunds;
        _paymentMethods = paymentMethods;
        _payables = payables;
        _partners = partners;
        _access = access;
        _companies = companies;
        _t = t;
    }

    public async Task<Result<SupplierCreditDto>> Handle(
        GetSupplierCreditByIdQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        // Fail-closed: GetByIdAsync ya filtra tenant + empresa operativa.
        var credit = await _credits.GetByIdAsync(tid, q.Id, ct);
        if (credit is null)
            return Result<SupplierCreditDto>.NotFound("Crédito de proveedor no encontrado.");

        var sources = await SupplierCreditReadModel.ResolveSourcesAsync(
            _credits,
            _companies,
            tid,
            credit.CompanyId,
            [credit.Id],
            ct
        );
        var names = await _partners.GetNamesByIdsAsync([credit.SupplierId], ct);
        var payableIds = credit
            .Movements.Where(m => m.TargetPurchasePayableId is not null)
            .Select(m => m.TargetPurchasePayableId!.Value)
            .Distinct()
            .ToList();
        var payables = await _payables.GetDocumentRefsByIdsAsync(tid, credit.CompanyId, payableIds, ct);
        var refunds = await _refunds.ListBySupplierCreditIdAsync(tid, credit.Id, ct);
        var paymentMethodNames = refunds.Count == 0
            ? new Dictionary<string, string>()
            : (await _paymentMethods.ListAsync(tid, onlyActive: false, ct))
                .GroupBy(m => m.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
        var users = await _access.GetUsersByIdsAsync(
            credit.Movements.Select(m => m.CreatedByUserId).Distinct().ToList(),
            ct
        );

        return Result<SupplierCreditDto>.Success(
            SupplierCreditReadModel.ToDetail(
                credit,
                new SupplierCreditReadContext(
                    names.GetValueOrDefault(credit.SupplierId),
                    sources.GetValueOrDefault(credit.Id),
                    payables,
                    refunds.ToDictionary(r => r.SupplierCreditMovementId),
                    users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim()),
                    paymentMethodNames
                )
            )
        );
    }
}

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — listado paginado con filtros en BD; por página: conteo +
/// página (sin movimientos) + origen (2 + zona horaria si hay devoluciones) + nombres (1).
/// </summary>
public sealed class GetSupplierCreditListHandler
    : IRequestHandler<GetSupplierCreditListQuery, Result<SupplierCreditListResultDto>>
{
    private readonly ISupplierCreditRepository _credits;
    private readonly IBusinessPartnerRepository _partners;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetSupplierCreditListHandler(
        ISupplierCreditRepository credits,
        IBusinessPartnerRepository partners,
        ICompanyRepository companies,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _credits = credits;
        _partners = partners;
        _companies = companies;
        _t = t;
        _c = c;
    }

    public async Task<Result<SupplierCreditListResultDto>> Handle(
        GetSupplierCreditListQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var page = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize is < 1 or > 200 ? 20 : q.PageSize;

        var (items, total) = await _credits.SearchAsync(
            tid,
            new SupplierCreditSearchCriteria(q.SupplierId, q.SourceType, q.IsOpen),
            page,
            pageSize,
            ct
        );
        var sources = await SupplierCreditReadModel.ResolveSourcesAsync(
            _credits,
            _companies,
            tid,
            _c.CompanyId,
            items.Select(c => c.Id).ToList(),
            ct
        );
        var names = await _partners.GetNamesByIdsAsync(items.Select(c => c.SupplierId).Distinct(), ct);

        return Result<SupplierCreditListResultDto>.Success(
            new SupplierCreditListResultDto(
                items
                    .Select(c =>
                        SupplierCreditReadModel.ToListItem(
                            c,
                            names.GetValueOrDefault(c.SupplierId),
                            sources.GetValueOrDefault(c.Id)
                        )
                    )
                    .ToList(),
                total,
                page,
                pageSize
            )
        );
    }
}
