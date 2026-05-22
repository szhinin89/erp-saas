using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.ListCompanies;

public sealed class ListCompaniesHandler : IRequestHandler<ListCompaniesQuery, Result<IReadOnlyList<CompanyListItemDto>>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly IAccessRepository _access;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;

    public ListCompaniesHandler(
        ICompanyAccessGuard accessGuard,
        IAccessRepository access,
        ICompanyRepository companies,
        ICurrentUser currentUser)
    {
        _accessGuard = accessGuard;
        _access = access;
        _companies = companies;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CompanyListItemDto>>> Handle(ListCompaniesQuery request, CancellationToken ct)
    {
        var subResult = await _accessGuard.RequireActiveSubscriberAsync(ct);
        if (!subResult.IsSuccess)
            return Result<IReadOnlyList<CompanyListItemDto>>.Failure(subResult.Error!);

        var subscriberId = subResult.Value!;
        if (string.Equals(_currentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            var adminRows = await _companies.GetActiveBySubscriberIdAsync(subscriberId, ct);
            var adminItems = adminRows
                .Where(c => !request.ActiveOnly || c.IsActive)
                .Select(c => new CompanyListItemDto(
                    c.Id,
                    c.SubscriberId,
                    c.LegalName,
                    c.TradeName,
                    c.Ruc,
                    c.CountryCode,
                    c.Timezone,
                    c.CurrencyCode,
                    c.IsActive,
                    "SuperAdmin"))
                .ToList();

            return Result<IReadOnlyList<CompanyListItemDto>>.Success(adminItems);
        }

        var memberships = await _access.GetActiveCompanyUserMembershipsForUserSystemAsync(_currentUser.UserId, ct);
        if (memberships.Count == 0)
            return Result<IReadOnlyList<CompanyListItemDto>>.Success(Array.Empty<CompanyListItemDto>());

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var rows = await _companies.GetByIdsForManagementAsync(companyIds, subscriberId, ct);
        var roleByCompany = memberships.ToDictionary(m => m.CompanyId, m => m.Role);

        var items = rows
            .Where(c => !request.ActiveOnly || c.IsActive)
            .Select(c => new CompanyListItemDto(
                c.Id,
                c.SubscriberId,
                c.LegalName,
                c.TradeName,
                c.Ruc,
                c.CountryCode,
                c.Timezone,
                c.CurrencyCode,
                c.IsActive,
                roleByCompany.GetValueOrDefault(c.Id, "User")))
            .ToList();

        return Result<IReadOnlyList<CompanyListItemDto>>.Success(items);
    }
}
