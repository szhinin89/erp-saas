using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// FINANCE-RECEIVABLES-LIST-ENTERPRISE-01: DTO enriquecido con los datos de la factura origen
/// que <c>SalesReceivable</c> no tiene por sí sola — antes el frontend mostraba <c>CustomerId</c>
/// crudo (GUID) porque este DTO solo traía columnas propias de la tabla <c>sales_receivables</c>.
/// <c>BranchName</c>/<c>CreatedByName</c> pueden venir null si el registro referenciado fue
/// eliminado — el frontend debe mostrar un fallback funcional, nunca el GUID.
/// </summary>
public sealed record SalesReceivableDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string CustomerIdentification,
    Guid BranchId,
    string? BranchName,
    Guid? CreatedByUserId,
    string? CreatedByName,
    DateOnly InvoiceIssuedAt,
    DateTime InvoiceCreatedAt,
    DateOnly? DueDate,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    string Status,
    string StatusLabel,
    int InstallmentCount,
    int? OverdueDays,
    IReadOnlyList<SalesReceivableInstallmentDto> Installments,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record SalesReceivableInstallmentDto(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    string Status
);

// ── Queries ─────────────────────────────────────────────────────────────

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto (defensa en profundidad) — SalesReceivable
/// deriva de SalesInvoice, que tampoco tiene BranchId de cabecera, así que esto no filtra
/// resultados por sucursal, solo exige sucursal activa autorizada.
/// </summary>
public sealed record GetReceivableByInvoiceQuery(Guid InvoiceId)
    : IRequest<Result<SalesReceivableDto>>,
        IBranchScopedRequest;

public sealed record GetReceivablesListQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<ReceivablesListResponse>>, IBranchScopedRequest;

public sealed record ReceivablesListResponse(
    IReadOnlyList<SalesReceivableDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Mapeo compartido ──────────────────────────────────────────────────────

/// <summary>
/// Único punto de armado de <see cref="SalesReceivableDto"/> — usado tanto por la consulta de
/// una CxC puntual como por el listado paginado, para no duplicar la derivación de
/// estado/vencimiento/mora entre ambos.
/// </summary>
public static class SalesReceivableDtoMapper
{
    public static SalesReceivableDto Build(
        SalesReceivable r,
        (
            string InvoiceNumber,
            string CustomerName,
            string CustomerTaxId,
            string CustomerIdentificationType,
            Guid BranchId,
            Guid CreatedBy,
            DateOnly IssueDate,
            DateTime CreatedAt
        )? invoiceSummary,
        string? branchName,
        string? createdByName,
        DateOnly companyToday
    )
    {
        // Cuota más próxima aún no saldada — es la que define "Vence" y la mora de la fila.
        var nextDueInstallment = r.Installments
            .Where(i => i.PaidAmount < i.Amount)
            .OrderBy(i => i.InstallmentNumber)
            .FirstOrDefault();
        var dueDate = nextDueInstallment?.DueDate;

        int? overdueDays = null;
        if (r.BalanceDue > 0 && dueDate.HasValue && dueDate.Value < companyToday)
            overdueDays = companyToday.DayNumber - dueDate.Value.DayNumber;

        var statusLabel = BuildStatusLabel(r, overdueDays);

        return new SalesReceivableDto(
            r.Id,
            r.InvoiceId,
            invoiceSummary?.InvoiceNumber ?? "—",
            r.CustomerId,
            invoiceSummary?.CustomerName ?? "Cliente no disponible",
            invoiceSummary is null
                ? "—"
                : $"{invoiceSummary.Value.CustomerIdentificationType}-{invoiceSummary.Value.CustomerTaxId}",
            invoiceSummary?.BranchId ?? Guid.Empty,
            branchName,
            invoiceSummary?.CreatedBy,
            createdByName,
            invoiceSummary?.IssueDate ?? default,
            invoiceSummary?.CreatedAt ?? r.CreatedAt,
            dueDate,
            r.OriginalAmount,
            r.PaidAmount,
            r.BalanceDue,
            r.Status,
            statusLabel,
            r.Installments.Count,
            overdueDays,
            r.Installments
                .OrderBy(i => i.InstallmentNumber)
                .Select(i => new SalesReceivableInstallmentDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.DueDate,
                    i.Amount,
                    i.PaidAmount,
                    i.Status
                ))
                .ToList(),
            r.CreatedAt,
            r.UpdatedAt
        );
    }

    /// <summary>
    /// Status persistido solo distingue "pending"/"cancelled" (ver SalesReceivable.cs) — "Pagada"
    /// y "Vencida" se derivan aquí desde saldo/mora, igual que ya hace el frontend de Cuentas por
    /// Pagar (getPayableStatusBadge) pero calculado en backend para que el frontend no tenga que
    /// repetir la regla de negocio.
    /// </summary>
    private static string BuildStatusLabel(SalesReceivable r, int? overdueDays)
    {
        if (r.Status == "cancelled")
            return "Anulada";
        if (r.BalanceDue <= 0)
            return "Pagada";
        if (overdueDays is > 0)
            return "Vencida";
        if (r.PaidAmount > 0)
            return "Parcial";
        return "Pendiente";
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetReceivableByInvoiceHandler
    : IRequestHandler<GetReceivableByInvoiceQuery, Result<SalesReceivableDto>>
{
    private readonly ISalesReceivableRepository _repo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICompanyClock _companyClock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetReceivableByInvoiceHandler(
        ISalesReceivableRepository repo,
        ISalesInvoiceRepository invoiceRepo,
        IBranchRepository branchRepo,
        IAccessRepository accessRepo,
        ICompanyClock companyClock,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _branchRepo = branchRepo;
        _accessRepo = accessRepo;
        _companyClock = companyClock;
        _t = t;
        _c = c;
    }

    public async Task<Result<SalesReceivableDto>> Handle(
        GetReceivableByInvoiceQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var r = await _repo.GetByInvoiceIdAsync(tid, q.InvoiceId, ct);
        if (r is null)
            return Result<SalesReceivableDto>.NotFound("Cuenta por cobrar no encontrada.");

        var summaries = await _invoiceRepo.GetReceivableSummariesByIdsAsync(
            tid,
            new[] { r.InvoiceId },
            ct
        );
        summaries.TryGetValue(r.InvoiceId, out var summary);

        string? branchName = null;
        string? createdByName = null;
        if (summary != default)
        {
            var branch = await _branchRepo.GetByIdForCompanyAsync(
                tid,
                _c.CompanyId,
                summary.BranchId,
                ct
            );
            branchName = branch?.Name;

            var users = await _accessRepo.GetUsersByIdsAsync(new[] { summary.CreatedBy }, ct);
            createdByName = users.FirstOrDefault()?.FullName;
        }

        var companyToday = await _companyClock.TodayAsync(_c.CompanyId, tid, ct);

        return Result<SalesReceivableDto>.Success(
            SalesReceivableDtoMapper.Build(
                r,
                summary == default ? null : summary,
                branchName,
                createdByName,
                companyToday
            )
        );
    }
}

public sealed class GetReceivablesListHandler
    : IRequestHandler<GetReceivablesListQuery, Result<ReceivablesListResponse>>
{
    private readonly ISalesReceivableRepository _repo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICompanyClock _companyClock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetReceivablesListHandler(
        ISalesReceivableRepository repo,
        ISalesInvoiceRepository invoiceRepo,
        IBranchRepository branchRepo,
        IAccessRepository accessRepo,
        ICompanyClock companyClock,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _branchRepo = branchRepo;
        _accessRepo = accessRepo;
        _companyClock = companyClock;
        _t = t;
        _c = c;
    }

    public async Task<Result<ReceivablesListResponse>> Handle(
        GetReceivablesListQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var (items, total) = await _repo.GetPagedAsync(
            tid,
            q.Search,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );

        // Un solo query por cada catálogo de apoyo (factura/sucursal/usuario) para toda la
        // página — nunca N+1 por fila.
        var invoiceIds = items.Select(x => x.InvoiceId).Distinct().ToList();
        var summaries = await _invoiceRepo.GetReceivableSummariesByIdsAsync(tid, invoiceIds, ct);

        // Sucursales de la empresa: sin método batch por id en IBranchRepository — el conjunto es
        // acotado (una empresa opera con pocas sucursales), así que traer todas una vez por
        // página es más barato que N+1 y no requiere tocar el módulo de Sucursales.
        var branches = await _branchRepo.GetByCompanyAsync(
            tid,
            _c.CompanyId,
            activeFilter: null,
            cancellationToken: ct
        );
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);

        var creatorIds = summaries.Values.Select(s => s.CreatedBy).Distinct().ToList();
        var creators = creatorIds.Count == 0
            ? []
            : await _accessRepo.GetUsersByIdsAsync(creatorIds, ct);
        var creatorNames = creators.ToDictionary(u => u.Id, u => u.FullName);

        var companyToday = await _companyClock.TodayAsync(_c.CompanyId, tid, ct);

        var dtos = items
            .Select(r =>
            {
                summaries.TryGetValue(r.InvoiceId, out var summary);
                var hasSummary = summary != default;
                string? branchName =
                    hasSummary && branchNames.TryGetValue(summary.BranchId, out var bn)
                        ? bn
                        : null;
                string? createdByName =
                    hasSummary && creatorNames.TryGetValue(summary.CreatedBy, out var un)
                        ? un
                        : null;

                return SalesReceivableDtoMapper.Build(
                    r,
                    hasSummary ? summary : null,
                    branchName,
                    createdByName,
                    companyToday
                );
            })
            .ToList();

        return Result<ReceivablesListResponse>.Success(
            new ReceivablesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}
