using ERP.Application.Common;
using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.DocTypes.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.DocTypes.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// DOCUMENT-FLOW-POLICY-01: representa CÓMO se comporta un tipo de documento para la company —
/// nunca QUIÉN puede ejecutar una acción sobre él. Los permisos (<c>expenses.documents.confirm</c>,
/// <c>expenses.documents.cancel</c>, etc.) se administran por separado en Roles y Permisos.
/// </summary>
public sealed record DocumentFlowPolicyDto(
    Guid Id,
    string DocumentTypeCode,
    string DocumentTypeName,
    bool IsActive,
    CreationMode CreationMode,
    ConfirmationMode ConfirmationMode,
    AuthorizationMode AuthorizationMode,
    PendingDocumentMode PendingDocumentMode,
    CancellationMode CancellationMode,
    bool RequiresCancellationReason,
    bool RequiresAttachment,
    bool RequiresSupplier,
    bool RequiresDueDate,
    PayableGenerationMode PayableGenerationMode,
    AccountingPostingMode AccountingPostingMode,
    InventoryImpactMode InventoryImpactMode,
    NotificationMode NotificationMode
);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetDocumentFlowPoliciesQuery()
    : IRequest<Result<IReadOnlyList<DocumentFlowPolicyDto>>>,
        ICompanyScopedRequest;

public sealed record GetDocumentFlowPolicyByIdQuery(Guid Id)
    : IRequest<Result<DocumentFlowPolicyDto>>,
        ICompanyScopedRequest;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record UpdateDocumentFlowPolicyCommand(
    Guid Id,
    bool IsActive,
    CreationMode CreationMode,
    ConfirmationMode ConfirmationMode,
    AuthorizationMode AuthorizationMode,
    PendingDocumentMode PendingDocumentMode,
    CancellationMode CancellationMode,
    bool RequiresCancellationReason,
    bool RequiresAttachment,
    bool RequiresSupplier,
    bool RequiresDueDate,
    PayableGenerationMode PayableGenerationMode,
    AccountingPostingMode AccountingPostingMode,
    InventoryImpactMode InventoryImpactMode,
    NotificationMode NotificationMode
) : IRequest<Result<DocumentFlowPolicyDto>>, ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class GetDocumentFlowPolicyByIdValidator : AbstractValidator<GetDocumentFlowPolicyByIdQuery>
{
    public GetDocumentFlowPolicyByIdValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class UpdateDocumentFlowPolicyValidator : AbstractValidator<UpdateDocumentFlowPolicyCommand>
{
    public UpdateDocumentFlowPolicyValidator() => RuleFor(x => x.Id).NotEmpty();
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetDocumentFlowPoliciesHandler
    : IRequestHandler<GetDocumentFlowPoliciesQuery, Result<IReadOnlyList<DocumentFlowPolicyDto>>>
{
    private readonly IDocumentFlowPolicyRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetDocumentFlowPoliciesHandler(
        IDocumentFlowPolicyRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<IReadOnlyList<DocumentFlowPolicyDto>>> Handle(
        GetDocumentFlowPoliciesQuery q,
        CancellationToken ct
    )
    {
        var rows = await _repo.ListAsync(_tenant.TenantId, _company.CompanyId, ct);
        var dtos = rows.Select(r => ToDto(r.Policy, r.DocumentTypeName)).ToList();
        return Result<IReadOnlyList<DocumentFlowPolicyDto>>.Success(dtos);
    }

    internal static DocumentFlowPolicyDto ToDto(DocumentFlowPolicy p, string documentTypeName) =>
        new(
            p.Id,
            p.DocumentTypeCode,
            documentTypeName,
            p.IsActive,
            p.CreationMode,
            p.ConfirmationMode,
            p.AuthorizationMode,
            p.PendingDocumentMode,
            p.CancellationMode,
            p.RequiresCancellationReason,
            p.RequiresAttachment,
            p.RequiresSupplier,
            p.RequiresDueDate,
            p.PayableGenerationMode,
            p.AccountingPostingMode,
            p.InventoryImpactMode,
            p.NotificationMode
        );
}

public sealed class GetDocumentFlowPolicyByIdHandler
    : IRequestHandler<GetDocumentFlowPolicyByIdQuery, Result<DocumentFlowPolicyDto>>
{
    private readonly IDocumentFlowPolicyRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetDocumentFlowPolicyByIdHandler(
        IDocumentFlowPolicyRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<DocumentFlowPolicyDto>> Handle(
        GetDocumentFlowPolicyByIdQuery q,
        CancellationToken ct
    )
    {
        var row = await _repo.GetByIdAsync(_tenant.TenantId, _company.CompanyId, q.Id, ct);
        return row is null
            ? Result<DocumentFlowPolicyDto>.NotFound("Política de flujo documental no encontrada.")
            : Result<DocumentFlowPolicyDto>.Success(
                GetDocumentFlowPoliciesHandler.ToDto(row.Value.Policy, row.Value.DocumentTypeName)
            );
    }
}

public sealed class UpdateDocumentFlowPolicyHandler
    : IRequestHandler<UpdateDocumentFlowPolicyCommand, Result<DocumentFlowPolicyDto>>
{
    private readonly IDocumentFlowPolicyRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public UpdateDocumentFlowPolicyHandler(
        IDocumentFlowPolicyRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _tenant = tenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<DocumentFlowPolicyDto>> Handle(
        UpdateDocumentFlowPolicyCommand cmd,
        CancellationToken ct
    )
    {
        var row = await _repo.GetByIdAsync(_tenant.TenantId, _company.CompanyId, cmd.Id, ct);
        if (row is null)
            return Result<DocumentFlowPolicyDto>.NotFound("Política de flujo documental no encontrada.");

        var entity = row.Value.Policy;
        entity.Update(
            cmd.IsActive,
            cmd.CreationMode,
            cmd.ConfirmationMode,
            cmd.AuthorizationMode,
            cmd.PendingDocumentMode,
            cmd.CancellationMode,
            cmd.RequiresCancellationReason,
            cmd.RequiresAttachment,
            cmd.RequiresSupplier,
            cmd.RequiresDueDate,
            cmd.PayableGenerationMode,
            cmd.AccountingPostingMode,
            cmd.InventoryImpactMode,
            cmd.NotificationMode,
            _user.UserId
        );

        await _repo.SaveChangesAsync(ct);

        return Result<DocumentFlowPolicyDto>.Success(
            GetDocumentFlowPoliciesHandler.ToDto(entity, row.Value.DocumentTypeName)
        );
    }
}
