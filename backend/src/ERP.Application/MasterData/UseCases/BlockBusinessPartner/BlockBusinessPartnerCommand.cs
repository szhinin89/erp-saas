using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BlockBusinessPartner;

/// <summary>Bloquea operativamente al BP en la empresa activa. Motivo obligatorio.</summary>
public sealed record BlockBusinessPartnerCommand(
    Guid   BusinessPartnerId,
    string Reason)
    : IRequest<Result<bool>>, ICompanyScopedRequest;
