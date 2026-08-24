using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.DownloadImportTemplate;

public sealed class DownloadImportTemplateHandler
    : IRequestHandler<DownloadImportTemplateQuery, Result<ImportTemplateFileDto>>
{
    private readonly IReadOnlyDictionary<ImportType, IImportProcessor> _processors;

    public DownloadImportTemplateHandler(
        IReadOnlyDictionary<ImportType, IImportProcessor> processors
    ) => _processors = processors;

    public async Task<Result<ImportTemplateFileDto>> Handle(
        DownloadImportTemplateQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!_processors.TryGetValue(query.ImportType, out var processor))
            return Result<ImportTemplateFileDto>.ValidationFailure(
                "No hay una plantilla disponible para este tipo de importación."
            );

        var template = await processor.BuildTemplateAsync(cancellationToken);
        return Result<ImportTemplateFileDto>.Success(template);
    }
}
