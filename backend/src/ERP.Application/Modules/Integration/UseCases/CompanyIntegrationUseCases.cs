using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.Integration.UseCases;

public sealed record CreateIntegrationCompanyCommand(
    IntegrationCompanyCreateRequest Request) : IRequest<Result<IntegrationCompanyStatusDto>>;

public sealed record GetIntegrationCompanyStatusQuery(Guid Id) : IRequest<Result<IntegrationCompanyStatusDto>>;

public sealed record ActivateIntegrationCompanyCommand(Guid Id) : IRequest<Result<IntegrationCompanyStatusDto>>;

public sealed record SuspendIntegrationCompanyCommand(Guid Id) : IRequest<Result<IntegrationCompanyStatusDto>>;

public sealed class CreateIntegrationCompanyHandler
    : IRequestHandler<CreateIntegrationCompanyCommand, Result<IntegrationCompanyStatusDto>>
{
    private readonly ICompanyProvisioningService _provisioning;

    public CreateIntegrationCompanyHandler(ICompanyProvisioningService provisioning)
        => _provisioning = provisioning;

    public async Task<Result<IntegrationCompanyStatusDto>> Handle(
        CreateIntegrationCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        try
        {
            var company = await _provisioning.CreateManagedCompanyAsync(
                request.TenantId,
                request.TaxId,
                request.LegalName,
                request.MainAddress,
                request.CreatedByUserId,
                request.CreatorRole,
                request.TradeName,
                request.Email,
                request.Phone,
                request.CountryCode,
                request.Timezone,
                request.CurrencyCode,
                request.BrandingJson,
                cancellationToken: cancellationToken);

            return Result<IntegrationCompanyStatusDto>.Success(ToDto(company));
        }
        catch (InvalidOperationException ex)
        {
            return Result<IntegrationCompanyStatusDto>.ValidationFailure(ex.Message);
        }
    }

    internal static IntegrationCompanyStatusDto ToDto(CompanyEntity company) =>
        new(
            company.Id,
            company.TenantId,
            company.LegalName,
            company.TaxIdentificationNumber,
            company.IsActive,
            company.OnboardingCompleted,
            company.OperationalStatus,
            company.CreatedAt,
            company.UpdatedAt);
}

public sealed class GetIntegrationCompanyStatusHandler
    : IRequestHandler<GetIntegrationCompanyStatusQuery, Result<IntegrationCompanyStatusDto>>
{
    private readonly ICompanyRepository _companies;

    public GetIntegrationCompanyStatusHandler(ICompanyRepository companies) => _companies = companies;

    public async Task<Result<IntegrationCompanyStatusDto>> Handle(
        GetIntegrationCompanyStatusQuery query,
        CancellationToken cancellationToken)
    {
        var company = await _companies.GetTrackedByIdForIntegrationAsync(query.Id, cancellationToken);
        return company is null
            ? Result<IntegrationCompanyStatusDto>.NotFound("Empresa no encontrada.")
            : Result<IntegrationCompanyStatusDto>.Success(CreateIntegrationCompanyHandler.ToDto(company));
    }
}

public sealed class ActivateIntegrationCompanyHandler
    : IRequestHandler<ActivateIntegrationCompanyCommand, Result<IntegrationCompanyStatusDto>>
{
    private readonly ICompanyRepository _companies;

    public ActivateIntegrationCompanyHandler(ICompanyRepository companies) => _companies = companies;

    public async Task<Result<IntegrationCompanyStatusDto>> Handle(
        ActivateIntegrationCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var company = await _companies.GetTrackedByIdForIntegrationAsync(command.Id, cancellationToken);
        if (company is null)
            return Result<IntegrationCompanyStatusDto>.NotFound("Empresa no encontrada.");

        company.MarkOperational();
        company.IsActive = true;
        await _companies.SaveChangesAsync(cancellationToken);
        return Result<IntegrationCompanyStatusDto>.Success(CreateIntegrationCompanyHandler.ToDto(company));
    }
}

public sealed class SuspendIntegrationCompanyHandler
    : IRequestHandler<SuspendIntegrationCompanyCommand, Result<IntegrationCompanyStatusDto>>
{
    private readonly ICompanyRepository _companies;

    public SuspendIntegrationCompanyHandler(ICompanyRepository companies) => _companies = companies;

    public async Task<Result<IntegrationCompanyStatusDto>> Handle(
        SuspendIntegrationCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var company = await _companies.GetTrackedByIdForIntegrationAsync(command.Id, cancellationToken);
        if (company is null)
            return Result<IntegrationCompanyStatusDto>.NotFound("Empresa no encontrada.");

        company.SuspendOperations();
        await _companies.SaveChangesAsync(cancellationToken);
        return Result<IntegrationCompanyStatusDto>.Success(CreateIntegrationCompanyHandler.ToDto(company));
    }
}
