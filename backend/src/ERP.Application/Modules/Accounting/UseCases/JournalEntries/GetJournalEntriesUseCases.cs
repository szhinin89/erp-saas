using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Queries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.JournalEntries;

// ── Queries ─────────────────────────────────────────────────────────────

/// <summary>
/// ACCOUNTING-LEDGER-VISIBILITY-01: listado paginado de asientos contables, solo lectura — no
/// modifica el motor de contabilización (JournalFactory/PostingPipeline) ni las reglas de
/// contabilización (PostingRule). CompanyId-scoped vía ICompanyScopedRequest, igual que el resto
/// de queries de Accounting (GetAccountsQuery, GetPostingRulesQuery).
/// </summary>
public sealed record GetJournalEntriesQuery(
    int PageNumber = 1,
    int PageSize = 20,
    JournalEntryStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? SourceModule = null
) : IRequest<Result<GetJournalEntriesResponse>>, ICompanyScopedRequest;

public sealed record GetJournalEntryByIdQuery(Guid Id)
    : IRequest<Result<JournalEntryDetailDto>>,
        ICompanyScopedRequest;

/// <summary>
/// ACCOUNTING-LEDGER-VISIBILITY-01: asientos originados por un documento externo específico
/// (p. ej. una SalesInvoice o PurchaseInvoice ya contabilizada). `sourceModule`/`sourceEventId`
/// son los únicos campos de origen que JournalEntry expone hoy — no existe un
/// SourceDocumentNumber legible independiente (ver brecha reportada en el entregable).
/// </summary>
public sealed record GetJournalEntriesBySourceQuery(string SourceModule, Guid SourceEventId)
    : IRequest<Result<IReadOnlyList<JournalEntryListItemDto>>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class GetJournalEntriesQueryValidator : AbstractValidator<GetJournalEntriesQuery>
{
    public GetJournalEntriesQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetJournalEntryByIdQueryValidator : AbstractValidator<GetJournalEntryByIdQuery>
{
    public GetJournalEntryByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetJournalEntriesBySourceQueryValidator
    : AbstractValidator<GetJournalEntriesBySourceQuery>
{
    public GetJournalEntriesBySourceQueryValidator()
    {
        RuleFor(x => x.SourceModule).NotEmpty();
        RuleFor(x => x.SourceEventId).NotEmpty();
    }
}

// ── Query Handlers ──────────────────────────────────────────────────────

public sealed class GetJournalEntriesHandler
    : IRequestHandler<GetJournalEntriesQuery, Result<GetJournalEntriesResponse>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IJournalEntrySourceResolver _sourceResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetJournalEntriesHandler(
        IJournalEntryRepository repo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
    }

    public async Task<Result<GetJournalEntriesResponse>> Handle(
        GetJournalEntriesQuery q,
        CancellationToken ct
    )
    {
        var filter = new JournalEntryListFilter(q.Status, q.FromDate, q.ToDate, q.SourceModule);
        var (items, totalCount) = await _repo.GetPageAsync(
            _t.TenantId,
            _c.CompanyId,
            filter,
            q.PageNumber,
            q.PageSize,
            ct
        );

        var sources = await _sourceResolver.ResolveManyAsync(
            _t.TenantId,
            _c.CompanyId,
            items.Select(Map.ToSourceRequest).ToList(),
            ct
        );

        var response = new GetJournalEntriesResponse(
            items.Select(e => Map.ToListItemDto(e, sources)).ToList(),
            q.PageNumber,
            q.PageSize,
            totalCount
        );
        return Result<GetJournalEntriesResponse>.Success(response);
    }
}

public sealed class GetJournalEntryByIdHandler
    : IRequestHandler<GetJournalEntryByIdQuery, Result<JournalEntryDetailDto>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IAccountRepository _accountRepo;
    private readonly IJournalEntrySourceResolver _sourceResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetJournalEntryByIdHandler(
        IJournalEntryRepository repo,
        IAccountRepository accountRepo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
    }

    public async Task<Result<JournalEntryDetailDto>> Handle(
        GetJournalEntryByIdQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var entry = await _repo.GetByIdAsync(tenantId, companyId, q.Id, ct);
        if (entry is null)
            return Result<JournalEntryDetailDto>.NotFound("Asiento contable no encontrado.");

        var accounts = await _accountRepo.GetByCompanyAsync(tenantId, companyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);

        // ACCOUNTING-REVERSALS-05: resuelve el par de reverso (Original/Reverse) para que el
        // detalle pueda mostrar número/fecha del asiento vinculado sin una segunda llamada del
        // frontend — 0 a 2 lookups por Id adicionales, aceptables en una vista de detalle.
        var originalEntry =
            entry.OriginalJournalEntryId is { } originalId
                ? await _repo.GetByIdAsync(tenantId, companyId, originalId, ct)
                : null;
        var reverseEntry =
            entry.ReverseJournalEntryId is { } reverseId
                ? await _repo.GetByIdAsync(tenantId, companyId, reverseId, ct)
                : null;

        var sourceRequests = new List<JournalEntrySourceRequest> { Map.ToSourceRequest(entry) };
        if (originalEntry is not null)
            sourceRequests.Add(Map.ToSourceRequest(originalEntry));
        var sources = await _sourceResolver.ResolveManyAsync(tenantId, companyId, sourceRequests, ct);

        sources.TryGetValue(entry.Id, out var ownSource);
        // Un asiento de reverso lleva SourceModule="Accounting"/SourceEventType="Reversal" (ver
        // JournalEntry.Reverse()) — nunca resuelve a un documento humano por sí mismo. El origen
        // documental real sigue siendo el del asiento original: si el propio origen no resolvió y
        // hay un original, se hereda el suyo (ACCOUNTING-REVERSALS-05 §8 — "mismo origen
        // documental o una referencia clara").
        var source =
            ownSource
            ?? (originalEntry is not null && sources.TryGetValue(originalEntry.Id, out var inherited)
                ? inherited
                : null);

        return Result<JournalEntryDetailDto>.Success(
            Map.ToDetailDto(entry, accountsById, source, originalEntry, reverseEntry)
        );
    }
}

public sealed class GetJournalEntriesBySourceHandler
    : IRequestHandler<GetJournalEntriesBySourceQuery, Result<IReadOnlyList<JournalEntryListItemDto>>>
{
    private readonly IJournalEntryRepository _repo;
    private readonly IJournalEntrySourceResolver _sourceResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetJournalEntriesBySourceHandler(
        IJournalEntryRepository repo,
        IJournalEntrySourceResolver sourceResolver,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _sourceResolver = sourceResolver;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<JournalEntryListItemDto>>> Handle(
        GetJournalEntriesBySourceQuery q,
        CancellationToken ct
    )
    {
        var entries = await _repo.GetBySourceAsync(
            _t.TenantId,
            _c.CompanyId,
            q.SourceModule,
            q.SourceEventId,
            ct
        );

        var sources = await _sourceResolver.ResolveManyAsync(
            _t.TenantId,
            _c.CompanyId,
            entries.Select(Map.ToSourceRequest).ToList(),
            ct
        );

        return Result<IReadOnlyList<JournalEntryListItemDto>>.Success(
            entries.Select(e => Map.ToListItemDto(e, sources)).ToList()
        );
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static JournalEntrySourceRequest ToSourceRequest(JournalEntry e) =>
        new(e.Id, e.SourceModule, e.SourceEventType, e.SourceEventId);

    public static JournalEntryListItemDto ToListItemDto(
        JournalEntry e,
        IReadOnlyDictionary<Guid, JournalEntrySourceInfo> sources
    )
    {
        sources.TryGetValue(e.Id, out var source);
        return new(
            e.Id,
            e.EntryNumber,
            e.EntryDate,
            e.SourceModule,
            e.SourceEventType,
            e.SourceEventId,
            e.Description,
            e.Lines.Sum(l => l.Debit),
            e.Lines.Sum(l => l.Credit),
            e.Status.ToString(),
            e.CreatedAt,
            source?.SourceDocumentType,
            source?.SourceDocumentNumber,
            source?.SourceDocumentDate,
            source?.SourcePartyName,
            source?.SourceStatus,
            source?.SourceRoute
        );
    }

    public static JournalEntryDetailDto ToDetailDto(
        JournalEntry e,
        IReadOnlyDictionary<Guid, Account> accountsById,
        JournalEntrySourceInfo? source,
        JournalEntry? originalEntry,
        JournalEntry? reverseEntry
    )
    {
        var lines = e
            .Lines.OrderBy(l => l.SortOrder)
            .Select(l =>
            {
                accountsById.TryGetValue(l.AccountId, out var account);
                return new JournalEntryLineDto(
                    l.Id,
                    l.AccountId,
                    account?.Code.Value ?? "—",
                    account?.Name ?? "(cuenta no encontrada)",
                    l.Description,
                    l.Debit,
                    l.Credit,
                    l.SortOrder
                );
            })
            .ToList();

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);

        return new JournalEntryDetailDto(
            e.Id,
            e.EntryNumber,
            e.EntryDate,
            e.AccountingPeriodId,
            e.FiscalYear,
            e.SourceModule,
            e.SourceEventType,
            e.SourceEventId,
            e.Description,
            e.Status.ToString(),
            e.PostedAtUtc,
            e.OriginalJournalEntryId,
            originalEntry?.EntryNumber,
            originalEntry?.EntryDate,
            e.ReverseJournalEntryId,
            reverseEntry?.EntryNumber,
            reverseEntry?.EntryDate,
            e.ReversedAtUtc,
            e.ReverseReason,
            lines,
            totalDebit,
            totalCredit,
            totalDebit == totalCredit,
            e.CreatedAt,
            source?.SourceDocumentType,
            source?.SourceDocumentNumber,
            source?.SourceDocumentDate,
            source?.SourcePartyName,
            source?.SourceStatus,
            source?.SourceRoute
        );
    }
}
