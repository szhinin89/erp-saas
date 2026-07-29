using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsDashboard;

public sealed class GetElectronicDocumentsDashboardQueryHandler
    : IRequestHandler<GetElectronicDocumentsDashboardQuery, Result<ElectronicDocumentsDashboardDto>>
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentsDashboardQueryHandler(
        IElectronicDocumentRepository repository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ElectronicDocumentsDashboardDto>> Handle(
        GetElectronicDocumentsDashboardQuery query,
        CancellationToken cancellationToken
    )
    {
        var companyId = _currentCompany.CompanyId;
        var tenantId = _currentTenant.TenantId;

        var stateCounts = await _repository.GetStateCountsAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        var authorizedToday = await _repository.CountAuthorizedTodayAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        var errorsToday = await _repository.CountErrorsTodayAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        var averageAuthorizationMinutes = await _repository.GetAverageAuthorizationMinutesAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        var pendingRetries = await _repository.CountPendingRetriesAsync(
            tenantId,
            companyId,
            cancellationToken
        );
        var totalToday = await _repository.CountCreatedTodayAsync(
            tenantId,
            companyId,
            cancellationToken
        );

        int CountOf(params ElectronicDocumentState[] states) =>
            states.Sum(s => stateCounts.TryGetValue(s, out var c) ? c : 0);

        var dto = new ElectronicDocumentsDashboardDto(
            Pending: CountOf(ElectronicDocumentState.Draft, ElectronicDocumentState.XmlGenerated),
            Signed: CountOf(ElectronicDocumentState.Signed),
            Sent: CountOf(ElectronicDocumentState.Sent, ElectronicDocumentState.Received),
            Authorized: CountOf(ElectronicDocumentState.Authorized),
            Rejected: CountOf(ElectronicDocumentState.Rejected),
            Error: CountOf(ElectronicDocumentState.DeadLetter, ElectronicDocumentState.Failed),
            AuthorizedToday: authorizedToday,
            ErrorsToday: errorsToday,
            AverageAuthorizationMinutes: averageAuthorizationMinutes,
            PendingRetries: pendingRetries,
            TotalToday: totalToday
        );

        return Result<ElectronicDocumentsDashboardDto>.Success(dto);
    }
}
