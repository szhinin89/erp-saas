using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;

public sealed class GetElectronicDocumentXmlQueryHandler
    : IRequestHandler<GetElectronicDocumentXmlQuery, Result<string>>
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentXmlQueryHandler(
        IElectronicDocumentRepository repository,
        IFileStorage fileStorage,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<string>> Handle(
        GetElectronicDocumentXmlQuery query,
        CancellationToken cancellationToken
    )
    {
        var document = await _repository.GetBySourceAsync(
            _currentTenant.TenantId,
            query.SourceModule,
            query.SourceEntityId,
            cancellationToken
        );
        if (document is null)
            return Result<string>.NotFound("El documento electrónico no existe.");

        // ERP-CORE-CLOSEOUT-07: GetBySourceAsync solo filtra por TenantId — sin este chequeo,
        // cualquier usuario del tenant podía leer el XML comercial completo de otra empresa
        // adivinando su sourceEntityId. Mismo patrón ya usado por GetElectronicDocumentDetail/
        // Timeline/RetryElectronicDocument.
        if (_currentCompany.HasCompanyContext && document.CompanyId != _currentCompany.CompanyId)
            return Result<string>.NotFound("El documento electrónico no existe.");

        var storedPath = query.Variant switch
        {
            ElectronicDocumentXmlVariant.Draft => document.XmlDraftPath,
            ElectronicDocumentXmlVariant.Signed => document.SignedXmlPath,
            ElectronicDocumentXmlVariant.Authorized => document.AuthorizedXmlPath,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(storedPath))
            return Result<string>.NotFound(
                $"El documento electrónico todavía no tiene un XML '{query.Variant}' almacenado."
            );

        await using var stream = await _fileStorage.GetAsync(storedPath, cancellationToken);
        if (stream is null)
            return Result<string>.NotFound(
                "El archivo XML registrado ya no está disponible en el almacenamiento."
            );

        using var reader = new StreamReader(stream);
        var xml = await reader.ReadToEndAsync(cancellationToken);
        return Result<string>.Success(xml);
    }
}
