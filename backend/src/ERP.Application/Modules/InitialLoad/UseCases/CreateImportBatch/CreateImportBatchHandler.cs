using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;

/// <summary>
/// Solo permite crear un lote para un <see cref="ImportType"/> que tenga un
/// <see cref="IImportProcessor"/> registrado — el motor no hardcodea qué tipos están
/// disponibles (INITIAL-LOAD-SUPPLIERS-01: antes rechazaba explícitamente todo lo que no fuera
/// Customers; agregar un import type nuevo ahora es solo registrar su processor, sin tocar este
/// handler).
/// </summary>
public sealed class CreateImportBatchHandler
    : IRequestHandler<CreateImportBatchCommand, Result<ImportBatchDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IReadOnlyDictionary<ImportType, IImportProcessor> _processors;
    private readonly IOperationalContext _ctx;

    public CreateImportBatchHandler(
        IImportBatchRepository batchRepo,
        IReadOnlyDictionary<ImportType, IImportProcessor> processors,
        IOperationalContext ctx
    )
    {
        _batchRepo = batchRepo;
        _processors = processors;
        _ctx = ctx;
    }

    public async Task<Result<ImportBatchDto>> Handle(
        CreateImportBatchCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (!_processors.ContainsKey(cmd.ImportType))
            return Result<ImportBatchDto>.ValidationFailure(
                "Este tipo de importación aún no está disponible."
            );

        var batch = ImportBatch.Create(
            _ctx.TenantId,
            _ctx.CompanyId,
            cmd.ImportType,
            _ctx.UserId,
            cmd.Label,
            cmd.AutoCreateCatalogValues
        );

        await _batchRepo.AddAsync(batch, cancellationToken);
        await _batchRepo.SaveChangesAsync(cancellationToken);

        return Result<ImportBatchDto>.Success(ImportBatchDto.From(batch));
    }
}
