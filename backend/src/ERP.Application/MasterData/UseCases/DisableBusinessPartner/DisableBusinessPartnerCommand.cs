using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.DisableBusinessPartner;

public sealed record DisableBusinessPartnerCommand(Guid Id)
    : IRequest<Result<bool>>, ISubscriberOnlyRequest;
