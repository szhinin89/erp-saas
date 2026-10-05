using ERP.Application.Common;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using System.Globalization;

namespace ERP.Application.Modules.Retentions.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — Retenciones aporta la comunicación de su comprobante autorizado:
/// RETENTION_AUTHORIZED al sujeto retenido (rol Supplier). El correo sale del contacto real del
/// tercero (<see cref="BusinessPartnerContactResolver"/>, misma regla que el snapshot de clientes);
/// si no tiene, se devuelve null y la cola deja evidencia RECIPIENT_MISSING — nunca se inventa.
/// Solo datos: ni asunto, ni HTML, ni adjuntos, ni outbox.
/// </summary>
public sealed class RetentionElectronicDocumentCommunicationContributor : IElectronicDocumentCommunicationContributor
{
    public const string RetentionDocumentSourceType = "RetentionDocument";

    private static readonly IReadOnlyDictionary<ElectronicDocumentType, ElectronicDocumentCommunicationTarget> Routes =
        new Dictionary<ElectronicDocumentType, ElectronicDocumentCommunicationTarget>
        {
            [ElectronicDocumentType.Retention] = new(CommunicationPurposes.RetentionAuthorized, RetentionDocumentSourceType),
        };

    private readonly IRetentionDocumentRepository _retentions;
    private readonly IBusinessPartnerRepository _partners;
    private readonly IBusinessPartnerContactRepository _contacts;
    private readonly IBusinessPartnerLocationRepository _locations;

    public RetentionElectronicDocumentCommunicationContributor(
        IRetentionDocumentRepository retentions,
        IBusinessPartnerRepository partners,
        IBusinessPartnerContactRepository contacts,
        IBusinessPartnerLocationRepository locations
    )
    {
        _retentions = retentions;
        _partners = partners;
        _contacts = contacts;
        _locations = locations;
    }

    public string SourceModule => RetentionElectronicDocumentSource.SourceModule;

    public IReadOnlyDictionary<ElectronicDocumentType, ElectronicDocumentCommunicationTarget> Targets => Routes;

    public async Task<Result<ElectronicDocumentCommunicationContribution>> ContributeAsync(
        ElectronicDocumentCommunicationContext context,
        CancellationToken ct = default
    )
    {
        var document = context.Document;
        var retention = await _retentions.GetByIdAsync(document.TenantId, document.SourceEntityId, ct);
        if (retention is null || retention.CompanyId != document.CompanyId)
            return Result<ElectronicDocumentCommunicationContribution>.Failure(
                "La retención del comprobante no existe en la empresa.",
                ApiResponseCodes.Communications.SourceNotFound
            );

        if (retention.Status != RetentionStatus.Issued || string.IsNullOrWhiteSpace(retention.RetentionNumber))
            return Result<ElectronicDocumentCommunicationContribution>.Failure(
                "La retención no está emitida.",
                ApiResponseCodes.Communications.SourceNotEligible
            );

        var subject = await _partners.GetByIdAsync(retention.SubjectBusinessPartnerId, ct);
        if (subject is null)
            return Result<ElectronicDocumentCommunicationContribution>.Failure(
                "El sujeto retenido no existe.",
                ApiResponseCodes.Communications.SourceNotFound
            );

        var (email, _) = await BusinessPartnerContactResolver.ResolveAsync(_contacts, _locations, subject.Id, ct);
        var supplierName = subject.Name.LegalName;

        return Result<ElectronicDocumentCommunicationContribution>.Success(
            new ElectronicDocumentCommunicationContribution(
                CommunicationPurposes.RetentionAuthorized,
                new CommunicationSource(SourceModule, RetentionDocumentSourceType, retention.Id),
                CommunicationRecipientRole.Supplier,
                supplierName,
                email,
                retention.BranchId,
                retention.RetentionNumber,
                new RetentionAuthorizedTemplateModel(
                    SupplierName: supplierName,
                    RetentionNumber: retention.RetentionNumber,
                    SourceDocumentNumber: retention.SourceDocumentNumber ?? "-",
                    AccessKey: document.AuthorizationNumber!.Value,
                    TotalRetained: retention.TotalRetained.ToString("0.00", CultureInfo.InvariantCulture),
                    IssuerName: context.IssuerName
                )
            )
        );
    }
}
