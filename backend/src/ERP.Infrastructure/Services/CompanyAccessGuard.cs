using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Platform.Companies;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CompanyAccessGuard : ICompanyAccessGuard
{
    private readonly IAccessRepository _access;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ISubscriberRepository _subscribers;
    private readonly ISecurityMetrics _metrics;

    public CompanyAccessGuard(
        IAccessRepository access,
        ICompanyRepository companies,
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ISubscriberRepository subscribers,
        ISecurityMetrics metrics)
    {
        _access = access;
        _companies = companies;
        _currentUser = currentUser;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _subscribers = subscribers;
        _metrics = metrics;
    }

    public async Task<Result<Guid>> RequireActiveSubscriberAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<Guid>.Failure("No autenticado.");

        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<Guid>.Failure("Contexto de suscriptor no establecido.");

        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null || !subscriber.IsActive)
            return Result<Guid>.Failure("Suscriptor no válido o inactivo.");

        return Result<Guid>.Success(subscriberId);
    }

    public async Task<Result<CompanyAccessContext>> RequireMembershipAsync(
        Guid companyId,
        bool requireActiveCompany = true,
        CancellationToken ct = default)
    {
        var subResult = await RequireActiveSubscriberAsync(ct);
        if (!subResult.IsSuccess)
            return Result<CompanyAccessContext>.Failure(subResult.Error!);

        var subscriberId = subResult.Value!;

        var company = await _companies.GetByIdAsync(companyId, ct);
        if (company is null || company.SubscriberId != subscriberId)
        {
            _metrics.RecordCrossCompanyDenied();
            return Result<CompanyAccessContext>.Failure("Empresa no encontrada o no pertenece al suscriptor activo.");
        }

        if (requireActiveCompany && !company.IsActive)
            return Result<CompanyAccessContext>.Failure("La empresa está inactiva.");

        if (string.Equals(_currentUser.Role, PlatformAuthConstants.JwtPlatformOperatorRole, StringComparison.OrdinalIgnoreCase))
        {
            var platformSubscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
            return Result<CompanyAccessContext>.Success(new CompanyAccessContext(
                _currentUser.UserId,
                subscriberId,
                companyId,
                PlatformAuthConstants.JwtPlatformOperatorRole,
                platformSubscriber?.IsActive ?? false,
                company.IsActive));
        }

        var membership = await _access.GetCompanyUserMembershipAsync(companyId, _currentUser.UserId, ct);
        if (membership is null || !membership.IsActive)
        {
            _metrics.RecordMembershipValidationFailed();
            return Result<CompanyAccessContext>.Failure("No tiene acceso a esta empresa.");
        }

        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);

        return Result<CompanyAccessContext>.Success(new CompanyAccessContext(
            _currentUser.UserId,
            subscriberId,
            companyId,
            membership.Role,
            subscriber?.IsActive ?? false,
            company.IsActive));
    }

    public Task<Result<CompanyAccessContext>> RequireCurrentCompanyAsync(CancellationToken ct = default)
    {
        if (!_currentCompany.HasCompanyContext)
        {
            _metrics.RecordInvalidCompanyContext();
            return Task.FromResult(Result<CompanyAccessContext>.Failure("No hay empresa operativa seleccionada."));
        }

        return RequireMembershipAsync(_currentCompany.CompanyId, requireActiveCompany: true, ct);
    }
}
