namespace ERP.Application.Modules.ElectronicDocuments.Services;

public interface ISourceDocumentSummaryProviderResolver
{
    /// <summary>Devuelve el proveedor del módulo, o <c>null</c> si ningún módulo lo ha registrado todavía.</summary>
    ISourceDocumentSummaryProvider? Resolve(string sourceModule);
}
