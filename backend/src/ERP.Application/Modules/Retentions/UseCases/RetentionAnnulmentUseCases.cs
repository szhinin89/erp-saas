using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ZH-RETENTION-SRI-ANNULMENT-01/01B — pasos explícitos y auditados de la anulación ante el SRI de una
// retención AUTORIZADA. La solicitud se INICIA desde la anulación de la Compra/Gasto
// (CancelPurchaseCommand/CancelExpenseDocumentCommand con RequestSriAnnulment = true); estos comandos
// registran la presentación (asistida: SRI en Línea) y disparan la VERIFICACIÓN automática en
// ConsultaComprobante (Ficha Técnica v2.34 §8). No existe un comando para declarar ANULADO: el estado
// fiscal lo informa el SRI. Autorización derivada del documento origen (IRetentionSourceAccess),
// server-side; sin acceso al origen → 404.

// ── Commands ─────────────────────────────────────────────────────────────

/// <summary>El usuario presentó la solicitud en SRI en Línea (no significa ANULADO); se verifica en el SRI enseguida.</summary>
public sealed record SubmitRetentionAnnulmentCommand(
    Guid RequestId,
    DateOnly SubmittedOn,
    string? Reference,
    string? Notes
) : IRequest<Result<RetentionAnnulmentRequestDto>>, ICompanyScopedRequest;

/// <summary>Consulta el estado fiscal en ConsultaComprobante y lo aplica (ANULADO → finaliza). El usuario no declara nada.</summary>
public sealed record VerifyRetentionAnnulmentWithSriCommand(Guid RequestId)
    : IRequest<Result<RetentionAnnulmentRequestDto>>,
        ICompanyScopedRequest;

/// <summary>Desistir: antes de presentar, o ya presentada si el SRI confirma que el comprobante sigue AUTORIZADO.</summary>
public sealed record AbandonRetentionAnnulmentCommand(Guid RequestId, string Reason)
    : IRequest<Result<RetentionAnnulmentRequestDto>>,
        ICompanyScopedRequest;

/// <summary>Reintento manual de la finalización del origen tras un ANULADO cuya anulación local falló.</summary>
public sealed record RetryRetentionAnnulmentFinalizationCommand(Guid RequestId)
    : IRequest<Result<RetentionAnnulmentRequestDto>>,
        ICompanyScopedRequest;

// ── Validators ───────────────────────────────────────────────────────────

public sealed class SubmitRetentionAnnulmentValidator : AbstractValidator<SubmitRetentionAnnulmentCommand>
{
    public SubmitRetentionAnnulmentValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.SubmittedOn).NotEmpty().WithMessage("La fecha de presentación es obligatoria.");
        RuleFor(x => x.Reference).MaximumLength(RetentionAnnulmentRequest.ReferenceMaxLen);
        RuleFor(x => x.Notes).MaximumLength(RetentionAnnulmentRequest.NotesMaxLen);
    }
}

public sealed class VerifyRetentionAnnulmentWithSriValidator
    : AbstractValidator<VerifyRetentionAnnulmentWithSriCommand>
{
    public VerifyRetentionAnnulmentWithSriValidator() => RuleFor(x => x.RequestId).NotEmpty();
}

public sealed class AbandonRetentionAnnulmentValidator : AbstractValidator<AbandonRetentionAnnulmentCommand>
{
    public AbandonRetentionAnnulmentValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo del desistimiento es obligatorio.")
            .MaximumLength(RetentionAnnulmentRequest.NotesMaxLen);
    }
}

public sealed class RetryRetentionAnnulmentFinalizationValidator
    : AbstractValidator<RetryRetentionAnnulmentFinalizationCommand>
{
    public RetryRetentionAnnulmentFinalizationValidator() => RuleFor(x => x.RequestId).NotEmpty();
}

// ── Handlers ─────────────────────────────────────────────────────────────

/// <summary>Resolución común de alcance y permiso: tenant/empresa del contexto, sucursal del origen y permiso del origen.</summary>
public sealed class RetentionAnnulmentAccess
{
    private readonly IRetentionAnnulmentRequestRepository _requests;
    private readonly IRetentionDocumentRepository _retentions;
    private readonly IRetentionSourceAccess _access;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICompanyClock _clock;

    public RetentionAnnulmentAccess(
        IRetentionAnnulmentRequestRepository requests,
        IRetentionDocumentRepository retentions,
        IRetentionSourceAccess access,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICompanyClock clock
    )
    {
        _requests = requests;
        _retentions = retentions;
        _access = access;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _clock = clock;
    }

    public Guid TenantId => _tenant.TenantId;
    public Guid CompanyId => _company.CompanyId;

    /// <summary>
    /// La solicitud del tenant/empresa activos cuyo origen pertenece a la sucursal activa y que el
    /// usuario puede operar (permiso de anular el origen). Devuelve el Result de fallo (404 si no existe
    /// o no es visible; 403 si es visible pero falta el permiso de acción).
    /// </summary>
    public async Task<(RetentionAnnulmentRequest? Request, Result<RetentionAnnulmentRequestDto>? Failure)> AuthorizeAsync(
        Guid requestId,
        CancellationToken ct
    )
    {
        var request = await _requests.GetByIdAsync(_tenant.TenantId, _company.CompanyId, requestId, ct);
        if (request is null)
            return (null, NotFound());

        var retention = await _retentions.GetByIdAsync(_tenant.TenantId, request.RetentionDocumentId, ct);
        if (
            retention is null
            || retention.CompanyId != _company.CompanyId
            || (_branch.HasBranchContext && retention.BranchId != _branch.BranchId)
            || !await _access.CanViewAsync(request.SourceDocumentType, ct)
        )
            return (null, NotFound());

        if (!await _access.CanCancelOriginAsync(request.SourceDocumentType, ct))
            return (
                null,
                Result<RetentionAnnulmentRequestDto>.Forbidden(
                    "No tiene permiso para anular el documento origen de esta retención."
                )
            );

        return (request, null);
    }

    public async Task<Result<RetentionAnnulmentRequestDto>> ToDtoAsync(
        Result<RetentionAnnulmentRequest> result,
        CancellationToken ct
    )
    {
        if (!result.IsSuccess)
            return Result<RetentionAnnulmentRequestDto>.Failure(result.Error!, result.Code);
        var today = await _clock.TodayAsync(_company.CompanyId, _tenant.TenantId, ct);
        return Result<RetentionAnnulmentRequestDto>.Success(
            RetentionAnnulmentRequestDto.From(result.Value!, today),
            result.Code
        );
    }

    /// <summary>Relee la solicitud desde la BD (descarta cambios en memoria de una transacción revertida).</summary>
    public Task<RetentionAnnulmentRequest?> ReloadAsync(Guid requestId, CancellationToken ct) =>
        _requests.GetByIdAsync(_tenant.TenantId, _company.CompanyId, requestId, ct);

    private static Result<RetentionAnnulmentRequestDto> NotFound() =>
        Result<RetentionAnnulmentRequestDto>.NotFound("La solicitud de anulación no existe.");
}

public sealed class SubmitRetentionAnnulmentHandler
    : IRequestHandler<SubmitRetentionAnnulmentCommand, Result<RetentionAnnulmentRequestDto>>
{
    private readonly RetentionAnnulmentAccess _access;
    private readonly IRetentionAnnulmentService _service;
    private readonly ICurrentUser _user;

    public SubmitRetentionAnnulmentHandler(
        RetentionAnnulmentAccess access,
        IRetentionAnnulmentService service,
        ICurrentUser user
    )
    {
        _access = access;
        _service = service;
        _user = user;
    }

    public async Task<Result<RetentionAnnulmentRequestDto>> Handle(
        SubmitRetentionAnnulmentCommand cmd,
        CancellationToken ct
    )
    {
        var (_, failure) = await _access.AuthorizeAsync(cmd.RequestId, ct);
        if (failure is not null)
            return failure;

        var submitted = await _service.MarkSubmittedAsync(
            _access.TenantId,
            _access.CompanyId,
            cmd.RequestId,
            cmd.SubmittedOn,
            cmd.Reference,
            cmd.Notes,
            _user.UserId,
            ct
        );
        if (!submitted.IsSuccess)
            return await _access.ToDtoAsync(submitted, ct);

        // 01B — "Ya presenté la solicitud" dispara la consulta automática en ConsultaComprobante. La
        // presentación ya quedó registrada: si la verificación no se pudo aplicar, se informa y el job
        // la reintenta (nunca se pierde la presentación ni se infiere un estado fiscal).
        var verified = await _service.VerifyWithSriAsync(
            _access.TenantId,
            _access.CompanyId,
            cmd.RequestId,
            _user.UserId,
            ct
        );
        if (verified.IsSuccess)
            return await _access.ToDtoAsync(verified, ct);

        var current = await _access.ReloadAsync(cmd.RequestId, ct);
        return await _access.ToDtoAsync(
            current is null
                ? verified
                : Result<RetentionAnnulmentRequest>.Success(current, ApiResponseCodes.Retentions.SriVerificationFailed),
            ct
        );
    }
}

public sealed class VerifyRetentionAnnulmentWithSriHandler
    : IRequestHandler<VerifyRetentionAnnulmentWithSriCommand, Result<RetentionAnnulmentRequestDto>>
{
    private readonly RetentionAnnulmentAccess _access;
    private readonly IRetentionAnnulmentService _service;
    private readonly ICurrentUser _user;

    public VerifyRetentionAnnulmentWithSriHandler(
        RetentionAnnulmentAccess access,
        IRetentionAnnulmentService service,
        ICurrentUser user
    )
    {
        _access = access;
        _service = service;
        _user = user;
    }

    public async Task<Result<RetentionAnnulmentRequestDto>> Handle(
        VerifyRetentionAnnulmentWithSriCommand cmd,
        CancellationToken ct
    )
    {
        var (_, failure) = await _access.AuthorizeAsync(cmd.RequestId, ct);
        if (failure is not null)
            return failure;

        var result = await _service.VerifyWithSriAsync(
            _access.TenantId,
            _access.CompanyId,
            cmd.RequestId,
            _user.UserId,
            ct
        );
        return await _access.ToDtoAsync(result, ct);
    }
}

public sealed class AbandonRetentionAnnulmentHandler
    : IRequestHandler<AbandonRetentionAnnulmentCommand, Result<RetentionAnnulmentRequestDto>>
{
    private readonly RetentionAnnulmentAccess _access;
    private readonly IRetentionAnnulmentService _service;
    private readonly ICurrentUser _user;

    public AbandonRetentionAnnulmentHandler(
        RetentionAnnulmentAccess access,
        IRetentionAnnulmentService service,
        ICurrentUser user
    )
    {
        _access = access;
        _service = service;
        _user = user;
    }

    public async Task<Result<RetentionAnnulmentRequestDto>> Handle(
        AbandonRetentionAnnulmentCommand cmd,
        CancellationToken ct
    )
    {
        var (_, failure) = await _access.AuthorizeAsync(cmd.RequestId, ct);
        if (failure is not null)
            return failure;

        var result = await _service.AbandonAsync(
            _access.TenantId,
            _access.CompanyId,
            cmd.RequestId,
            cmd.Reason,
            _user.UserId,
            ct
        );
        return await _access.ToDtoAsync(result, ct);
    }
}

public sealed class RetryRetentionAnnulmentFinalizationHandler
    : IRequestHandler<RetryRetentionAnnulmentFinalizationCommand, Result<RetentionAnnulmentRequestDto>>
{
    private readonly RetentionAnnulmentAccess _access;
    private readonly IRetentionAnnulmentService _service;
    private readonly ICurrentUser _user;

    public RetryRetentionAnnulmentFinalizationHandler(
        RetentionAnnulmentAccess access,
        IRetentionAnnulmentService service,
        ICurrentUser user
    )
    {
        _access = access;
        _service = service;
        _user = user;
    }

    public async Task<Result<RetentionAnnulmentRequestDto>> Handle(
        RetryRetentionAnnulmentFinalizationCommand cmd,
        CancellationToken ct
    )
    {
        var (_, failure) = await _access.AuthorizeAsync(cmd.RequestId, ct);
        if (failure is not null)
            return failure;

        var result = await _service.FinalizeAsync(
            _access.TenantId,
            _access.CompanyId,
            cmd.RequestId,
            _user.UserId,
            ct
        );
        return await _access.ToDtoAsync(result, ct);
    }
}
