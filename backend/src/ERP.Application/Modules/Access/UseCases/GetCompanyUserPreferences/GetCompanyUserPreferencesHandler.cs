using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserPreferences;

public sealed class GetCompanyUserPreferencesHandler
    : IRequestHandler<GetCompanyUserPreferencesQuery, Result<CompanyUserPreferencesDto?>>
{
    private readonly ICompanyUserPreferencesRepository _preferencesRepository;

    public GetCompanyUserPreferencesHandler(ICompanyUserPreferencesRepository preferencesRepository)
    {
        _preferencesRepository = preferencesRepository;
    }

    public async Task<Result<CompanyUserPreferencesDto?>> Handle(
        GetCompanyUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await _preferencesRepository.GetByMembershipAsync(
            request.CompanyUserMembershipId, cancellationToken);

        var dto = preferences is null ? null : CompanyUserPreferencesMapper.ToDto(preferences);
        return Result<CompanyUserPreferencesDto?>.Success(dto);
    }
}
