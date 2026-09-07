using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SetSupplierRetentionDefaultState;

public sealed class SetSupplierRetentionDefaultStateHandler
    : IRequestHandler<SetSupplierRetentionDefaultStateCommand, Result<SupplierRetentionDefaultDto>>
{
    private readonly ISupplierRetentionDefaultRepository _repo;
    private readonly ISriCatalogLookupRepository _catalogRepo;
    private readonly IOperationalContext _ctx;

    public SetSupplierRetentionDefaultStateHandler(
        ISupplierRetentionDefaultRepository repo,
        ISriCatalogLookupRepository catalogRepo,
        IOperationalContext ctx
    )
    {
        _repo = repo;
        _catalogRepo = catalogRepo;
        _ctx = ctx;
    }

    public async Task<Result<SupplierRetentionDefaultDto>> Handle(
        SetSupplierRetentionDefaultStateCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var entry = await _repo.GetByIdAsync(cmd.Id, cancellationToken);
        if (entry is null)
            return Result<SupplierRetentionDefaultDto>.NotFound(
                "Retención predeterminada no encontrada."
            );

        if (cmd.IsActive)
            entry.Activate(_ctx.UserId);
        else
            entry.Deactivate(_ctx.UserId);
        entry.SetDisplayOrder(cmd.DisplayOrder, _ctx.UserId);

        await _repo.SaveChangesAsync(cancellationToken);

        var catalogCode = await _catalogRepo.GetRetentionCodeByIdAsync(
            entry.SriRetentionCodeId,
            cancellationToken
        );
        return Result<SupplierRetentionDefaultDto>.Success(
            SupplierRetentionDefaultDto.From(entry, catalogCode)
        );
    }
}
