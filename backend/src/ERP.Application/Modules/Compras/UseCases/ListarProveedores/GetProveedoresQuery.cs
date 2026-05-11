using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.ListarProveedores;

public sealed record GetProveedoresQuery(
    bool?   ActiveFilter,
    string? Search,
    string? TipoPersona
) : IRequest<Result<IReadOnlyList<ProveedorDto>>>;
