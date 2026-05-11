using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

public sealed class DeleteConfiguracionGastoCategoriaCommandHandler
    : IRequestHandler<DeleteConfiguracionGastoCategoriaCommand, Result<Unit>>
{
    private readonly IConfiguracionContableRepository _repo;

    public DeleteConfiguracionGastoCategoriaCommandHandler(IConfiguracionContableRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<Unit>> Handle(DeleteConfiguracionGastoCategoriaCommand request, CancellationToken ct)
    {
        var row = await _repo.GetGastoCategoriaByIdAsync(request.Id, ct);
        if (row is null)
            return Result<Unit>.Failure("El mapeo no existe.");

        _repo.RemoveGastoCategoria(row);
        await _repo.SaveChangesAsync(ct);
        return Result<Unit>.Success(default);
    }
}
