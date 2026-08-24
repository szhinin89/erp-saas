using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;

/// <summary>
/// Rechaza explícitamente cualquier <see cref="ImportType"/> distinto de
/// <see cref="ImportType.Customers"/> — único punto server-side que mantiene "solo Clientes
/// disponible" en esta entrega aunque el enum ya reserve valores para futuros import types.
/// </summary>
public sealed class CreateImportBatchHandler
    : IRequestHandler<CreateImportBatchCommand, Result<ImportBatchDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IOperationalContext _ctx;

    public CreateImportBatchHandler(IImportBatchRepository batchRepo, IOperationalContext ctx)
    {
        _batchRepo = batchRepo;
        _ctx = ctx;
    }

    public async Task<Result<ImportBatchDto>> Handle(
        CreateImportBatchCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (cmd.ImportType != ImportType.Customers)
            return Result<ImportBatchDto>.ValidationFailure(
                "Este tipo de importación aún no está disponible."
            );

        var batch = ImportBatch.Create(
            _ctx.TenantId,
            _ctx.CompanyId,
            cmd.ImportType,
            _ctx.UserId,
            cmd.Label
        );

        await _batchRepo.AddAsync(batch, cancellationToken);
        await _batchRepo.SaveChangesAsync(cancellationToken);

        return Result<ImportBatchDto>.Success(ImportBatchDto.From(batch));
    }
}
