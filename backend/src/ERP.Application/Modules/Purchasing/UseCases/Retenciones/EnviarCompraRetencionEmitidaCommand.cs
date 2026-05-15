using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchasing.UseCases.Retenciones;

public sealed record EnviarCompraRetencionEmitidaCommand(Guid RetencionId) : IRequest<Result<Guid>>;
