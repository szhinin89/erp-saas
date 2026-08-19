using ERP.Application.Common;
using ERP.Application.Modules.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Configuration.UseCases.GetConfigurationChangeLog;

public sealed class GetConfigurationChangeLogQueryHandler
    : IRequestHandler<GetConfigurationChangeLogQuery, Result<ConfigurationChangeLogPageDto>>
{
    private readonly IConfigurationChangeLogQueryRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetConfigurationChangeLogQueryHandler(
        IConfigurationChangeLogQueryRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ConfigurationChangeLogPageDto>> Handle(
        GetConfigurationChangeLogQuery request,
        CancellationToken cancellationToken
    )
    {
        var (items, total) = await _repo.GetAsync(
            new ConfigurationChangeLogQuery(
                TenantId: _currentTenant.TenantId,
                CompanyId: _currentCompany.CompanyId,
                EntityType: request.EntityType,
                EntityId: request.EntityId,
                Key: request.Key,
                Scope: request.Scope,
                FromUtc: request.FromUtc,
                ToUtc: request.ToUtc,
                Page: request.Page,
                PageSize: request.PageSize
            ),
            cancellationToken
        );

        return Result<ConfigurationChangeLogPageDto>.Success(
            new ConfigurationChangeLogPageDto(
                items.Select(l => new ConfigurationChangeLogDto(
                        l.Id,
                        l.Scope.ToString(),
                        l.ScopeId,
                        l.Key,
                        l.EntityType,
                        l.EntityId,
                        l.FieldName,
                        l.OldValue,
                        l.NewValue,
                        l.ValueType.ToString(),
                        l.ChangedBy,
                        l.ChangedAtUtc,
                        l.Source.ToString(),
                        l.Reason,
                        l.IsSensitive
                    ))
                    .ToList(),
                total,
                request.Page,
                request.PageSize
            )
        );
    }
}
