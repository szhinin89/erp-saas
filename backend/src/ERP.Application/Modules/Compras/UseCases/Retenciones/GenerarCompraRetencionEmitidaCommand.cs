using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Compras.UseCases.Retenciones;

public sealed record GenerarCompraRetencionEmitidaCommand(Guid CompraFacturaId) : IRequest<Result<Guid>>;
