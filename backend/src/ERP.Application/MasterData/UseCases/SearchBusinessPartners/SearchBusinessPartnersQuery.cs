using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

public sealed record SearchBusinessPartnersQuery(
    string? Query      = null,
    bool?   IsActive   = true,
    bool?   IsCustomer = null,
    bool?   IsSupplier = null,
    int     Skip       = 0,
    int     Take       = 50) : IRequest<Result<IReadOnlyList<BusinessPartnerDto>>>, ISubscriberOnlyRequest;
