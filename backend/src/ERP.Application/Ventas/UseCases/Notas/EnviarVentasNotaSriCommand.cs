using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Ventas.UseCases.Notas;

public sealed record EnviarVentasNotaSriCommand(Guid NotaId) : IRequest<Result<Guid>>;
