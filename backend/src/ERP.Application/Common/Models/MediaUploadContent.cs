namespace ERP.Application.Common.Models;

/// <summary>
/// Contenido de un archivo a subir, desacoplado de <c>IFormFile</c> para que
/// la capa Application no dependa de ASP.NET Core.
/// </summary>
public sealed record MediaUploadContent(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes
);
