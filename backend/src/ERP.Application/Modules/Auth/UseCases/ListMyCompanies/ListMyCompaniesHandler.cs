using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.ListMyCompanies;

public sealed class ListMyCompaniesHandler : IRequestHandler<ListMyCompaniesQuery, Result<IReadOnlyList<AccessibleCompanyDto>>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;

    public ListMyCompaniesHandler(
        IAccessRepository accessRepository,
        ICompanyRepository companyRepository,
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber)
    {
        _accessRepository = accessRepository;
        _companyRepository = companyRepository;
        _currentUser = currentUser;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<AccessibleCompanyDto>>> Handle(ListMyCompaniesQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<IReadOnlyList<AccessibleCompanyDto>>.Failure("No autenticado.");

        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<IReadOnlyList<AccessibleCompanyDto>>.Failure("Contexto de suscriptor no establecido.");

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(_currentUser.UserId, ct);
        if (memberships.Count == 0)
            return Result<IReadOnlyList<AccessibleCompanyDto>>.Success(Array.Empty<AccessibleCompanyDto>());

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, ct);
        var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);

        var items = companies
            .Where(c => c.SubscriberId == subscriberId)
            .Select(c =>
            {
                var m = membershipByCompany[c.Id];
                return new AccessibleCompanyDto(
                    c.Id,
                    c.SubscriberId,
                    c.LegalName,
                    c.TradeName ?? c.LegalName,
                    c.Ruc,
                    m.Role);
            })
            .OrderBy(x => x.LegalName)
            .ToList();

        return Result<IReadOnlyList<AccessibleCompanyDto>>.Success(items);
    }
}
