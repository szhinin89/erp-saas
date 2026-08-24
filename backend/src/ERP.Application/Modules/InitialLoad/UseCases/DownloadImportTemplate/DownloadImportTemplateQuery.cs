using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.DownloadImportTemplate;

public sealed record DownloadImportTemplateQuery(ImportType ImportType)
    : IRequest<Result<ImportTemplateFileDto>>,
        ITenantScopedRequest;
