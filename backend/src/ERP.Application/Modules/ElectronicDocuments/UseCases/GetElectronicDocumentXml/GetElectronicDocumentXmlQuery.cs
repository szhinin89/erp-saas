using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;

/// <summary>
/// Recupera el contenido del XML (borrador o firmado) de un documento electrónico ya
/// persistido, por referencia al documento de origen. La lectura siempre pasa por
/// <c>IFileStorage</c> — nunca accede al filesystem directamente.
/// </summary>
public sealed record GetElectronicDocumentXmlQuery(
    string SourceModule,
    Guid SourceEntityId,
    ElectronicDocumentXmlVariant Variant
) : IRequest<Result<string>>, ICompanyScopedRequest;
