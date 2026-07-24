namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>Rutas opacas devueltas por <c>IFileStorage.SaveAsync</c> — nunca construidas manualmente.</summary>
public sealed record ElectronicDocumentStoredXmlPaths(string DraftXmlPath, string SignedXmlPath);
