using MediatR;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.RetryElectronicDocument;

/// <summary>
/// Delega en <see cref="IElectronicDocumentIssuer"/> — mismo servicio que usa el job automático
/// de reintentos, para que el comportamiento sea idéntico sin importar si el reintento lo
/// dispara este comando (Monitor) o el job Hangfire. Antes de delegar, valida Company Scope
/// explícitamente: <see cref="IElectronicDocumentIssuer.RetryAsync"/> solo filtra por TenantId
/// (lo comparte con el job Hangfire, que es cross-company por diseño), así que el guard de
/// empresa activa vive aquí — mismo patrón que <c>GetElectronicDocumentDetailQueryHandler</c>.
/// Devuelve el mismo <see cref="ElectronicDocumentDetailDto"/> completo que el detalle de
/// Monitor (vía <see cref="ElectronicDocumentDiagnosticAssembler"/>) — antes devolvía
/// <see cref="ElectronicDocumentDto"/>, una proyección mínima sin diagnóstico ni timeline, lo
/// que rompía el contrato que el frontend esperaba tras un reintento (ver ADR-024).
/// </summary>
public sealed class RetryElectronicDocumentCommandHandler
    : IRequestHandler<RetryElectronicDocumentCommand, Result<ElectronicDocumentDetailDto>>
{
    private const int TimelineTake = 20;

    private readonly IElectronicDocumentIssuer _issuer;
    private readonly IElectronicDocumentRepository _repository;
    private readonly ISourceDocumentSummaryProviderResolver _summaryResolver;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAuditReader<ElectronicDocumentAudit> _auditReader;
    private readonly IAuditReader<ElectronicDocumentSriMessage> _sriMessageReader;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public RetryElectronicDocumentCommandHandler(
        IElectronicDocumentIssuer issuer,
        IElectronicDocumentRepository repository,
        ISourceDocumentSummaryProviderResolver summaryResolver,
        ICompanyRepository companyRepository,
        IAuditReader<ElectronicDocumentAudit> auditReader,
        IAuditReader<ElectronicDocumentSriMessage> sriMessageReader,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser)
    {
        _issuer = issuer;
        _repository = repository;
        _summaryResolver = summaryResolver;
        _companyRepository = companyRepository;
        _auditReader = auditReader;
        _sriMessageReader = sriMessageReader;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<ElectronicDocumentDetailDto>> Handle(
        RetryElectronicDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(_currentTenant.TenantId, command.ElectronicDocumentId, cancellationToken);
        if (document is null)
            return Result<ElectronicDocumentDetailDto>.NotFound("El documento electrónico no existe.");

        if (_currentCompany.HasCompanyContext && document.CompanyId != _currentCompany.CompanyId)
            return Result<ElectronicDocumentDetailDto>.NotFound("El documento electrónico no existe.");

        var retryResult = await _issuer.RetryAsync(
            _currentTenant.TenantId, command.ElectronicDocumentId, _currentUser.UserId, cancellationToken);
        if (!retryResult.IsSuccess)
            return Result<ElectronicDocumentDetailDto>.Failure(retryResult.Error!, retryResult.Code);

        // El mismo DbContext scoped resuelve `document` a la instancia trackeada que el issuer
        // acaba de mutar (identity map de EF Core) — no hace falta releer de la base de datos.
        var company = await _companyRepository.GetByIdForTenantAsync(
            document.CompanyId, _currentTenant.TenantId, cancellationToken);

        string? documentNumber = null;
        string? counterpartyName = null;
        var summaryProvider = _summaryResolver.Resolve(document.SourceModule);
        if (summaryProvider is not null)
        {
            var summaries = await summaryProvider.GetSummariesAsync(
                _currentTenant.TenantId, new[] { document.SourceEntityId }, cancellationToken);
            if (summaries.TryGetValue(document.SourceEntityId, out var summary))
            {
                documentNumber = summary.DocumentNumber;
                counterpartyName = summary.CounterpartyName;
            }
        }

        var auditRecords = await ElectronicDocumentTimelineBuilder.FetchRecordsAsync(
            _auditReader, _currentTenant.TenantId, document.Id, TimelineTake, cancellationToken);
        var lastReason = auditRecords.FirstOrDefault()?.Reason;

        var diagnostic = await ElectronicDocumentDiagnosticAssembler.BuildAsync(
            document, auditRecords, _sriMessageReader, cancellationToken);

        var dto = new ElectronicDocumentDetailDto(
            document.Id,
            document.CompanyId,
            company?.TradeName ?? company?.LegalName ?? "—",
            company?.TaxIdentificationNumber ?? "—",
            document.DocumentType.ToString(),
            document.SourceModule,
            document.SourceEntityId,
            documentNumber,
            counterpartyName,
            document.CreatedAt,
            document.UpdatedAt,
            lastReason,
            diagnostic);

        return Result<ElectronicDocumentDetailDto>.Success(dto);
    }
}
