using ERP.Application.Common;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-APPLICATION-01C — busca la retención ACTIVA (<c>Status != Cancelled</c>) sobre un
/// documento origen dado. Solo lectura, respeta scope tenant/company/branch vía el repositorio
/// (nunca <c>IgnoreQueryFilters</c>). "No encontrada" no es un error de negocio — es un estado
/// normal (el documento origen simplemente no tiene retención activa todavía) — por eso devuelve
/// <c>Result&lt;RetentionDocumentDto?&gt;.Success(null)</c> en vez de <c>NotFound</c> ("get by
/// origin, puede no existir").
/// </summary>
public sealed record GetRetentionBySourceQuery(
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId
) : IRequest<Result<RetentionDocumentDto?>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class GetRetentionBySourceValidator : AbstractValidator<GetRetentionBySourceQuery>
{
    public GetRetentionBySourceValidator()
    {
        RuleFor(x => x.SourceDocumentId).NotEmpty();
        RuleFor(x => x.SourceDocumentType).IsInEnum();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class GetRetentionBySourceHandler
    : IRequestHandler<GetRetentionBySourceQuery, Result<RetentionDocumentDto?>>
{
    private readonly IRetentionDocumentRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;

    public GetRetentionBySourceHandler(
        IRetentionDocumentRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch
    )
    {
        _repo = repo;
        _tenant = tenant;
        _company = company;
        _branch = branch;
    }

    public async Task<Result<RetentionDocumentDto?>> Handle(GetRetentionBySourceQuery q, CancellationToken ct)
    {
        var retention = await _repo.GetBySourceAsync(
            _tenant.TenantId,
            _company.CompanyId,
            q.SourceDocumentType,
            q.SourceDocumentId,
            ct
        );

        // GetBySourceAsync ya filtra tenant+company; el branch se valida explícitamente porque el
        // repositorio no lo filtra — mismo patrón fail-closed que el resto de handlers de este
        // módulo. Una retención de otra sucursal se trata igual que "no existe" (no NotFound —
        // ver comentario del tipo de la query).
        if (retention is null || retention.BranchId != _branch.BranchId)
            return Result<RetentionDocumentDto?>.Success(null);

        return Result<RetentionDocumentDto?>.Success(RetentionDocumentMapper.ToDto(retention));
    }
}
