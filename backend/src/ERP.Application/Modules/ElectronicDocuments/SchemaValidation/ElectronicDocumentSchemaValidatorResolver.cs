using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

public sealed class ElectronicDocumentSchemaValidatorResolver : IElectronicDocumentSchemaValidatorResolver
{
    private readonly IReadOnlyDictionary<ElectronicDocumentType, IElectronicDocumentSchemaValidator> _validators;

    public ElectronicDocumentSchemaValidatorResolver(IEnumerable<IElectronicDocumentSchemaValidator> validators)
    {
        _validators = validators.ToDictionary(v => v.DocumentType);
    }

    public IElectronicDocumentSchemaValidator? Resolve(ElectronicDocumentType documentType)
        => _validators.TryGetValue(documentType, out var validator) ? validator : null;
}
