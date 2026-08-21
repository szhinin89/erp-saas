using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesReceiptPrintPayload;

public sealed class GetSalesReceiptPrintPayloadQueryHandler
    : IRequestHandler<GetSalesReceiptPrintPayloadQuery, Result<SalesReceiptPrintPayloadDto>>
{
    private const string SalesSourceModule = "Sales";

    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly IElectronicDocumentRepository _electronicDocuments;
    private readonly ICompanyRepository _companies;
    private readonly IBranchRepository _branches;
    private readonly ICashSessionRepository _cashSessions;
    private readonly IEmissionPointRepository _emissionPoints;
    private readonly ICompanyBrandingResolver _branding;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentBranch _currentBranch;

    public GetSalesReceiptPrintPayloadQueryHandler(
        ISalesInvoiceRepository salesInvoices,
        IElectronicDocumentRepository electronicDocuments,
        ICompanyRepository companies,
        IBranchRepository branches,
        ICashSessionRepository cashSessions,
        IEmissionPointRepository emissionPoints,
        ICompanyBrandingResolver branding,
        ICurrentTenant currentTenant,
        ICurrentBranch currentBranch
    )
    {
        _salesInvoices = salesInvoices;
        _electronicDocuments = electronicDocuments;
        _companies = companies;
        _branches = branches;
        _cashSessions = cashSessions;
        _emissionPoints = emissionPoints;
        _branding = branding;
        _currentTenant = currentTenant;
        _currentBranch = currentBranch;
    }

    public async Task<Result<SalesReceiptPrintPayloadDto>> Handle(
        GetSalesReceiptPrintPayloadQuery query,
        CancellationToken cancellationToken
    )
    {
        var invoice = await _salesInvoices.GetByIdAsync(
            _currentTenant.TenantId,
            query.InvoiceId,
            cancellationToken
        );
        if (
            invoice is null
            || invoice.BranchId != _currentBranch.BranchId
            || invoice.Status != SalesInvoiceStatus.Authorized
        )
            return Result<SalesReceiptPrintPayloadDto>.NotFound("Factura no encontrada.");

        var company = await _companies.GetByIdAsync(invoice.CompanyId, cancellationToken);
        if (company is null || company.TenantId != _currentTenant.TenantId)
            return Result<SalesReceiptPrintPayloadDto>.NotFound("Factura no encontrada.");

        var branch = await _branches.GetByIdAsync(
            _currentTenant.TenantId,
            invoice.BranchId,
            cancellationToken
        );
        if (branch is null || branch.CompanyId != invoice.CompanyId)
            return Result<SalesReceiptPrintPayloadDto>.NotFound("Factura no encontrada.");

        var electronicDocument = await _electronicDocuments.GetBySourceAsync(
            _currentTenant.TenantId,
            SalesSourceModule,
            invoice.Id,
            cancellationToken
        );
        if (electronicDocument?.CompanyId != invoice.CompanyId)
            electronicDocument = null;

        var cashSession = await _cashSessions.GetByIdAsync(
            _currentTenant.TenantId,
            invoice.CashSessionId,
            cancellationToken
        );
        var emissionPoint =
            invoice.EmissionPointId is null
                ? null
                : await _emissionPoints.GetByIdAsync(
                    invoice.EmissionPointId.Value,
                    _currentTenant.TenantId,
                    cancellationToken
                );
        var branding = await _branding.GetAsync(
            _currentTenant.TenantId,
            invoice.CompanyId,
            cancellationToken
        );

        var (establishmentCode, emissionPointCode) = SalesReceiptPrintPayloadMapper.ResolveSriCodes(
            invoice,
            cashSession?.EmissionPointCodeSnapshot,
            emissionPoint?.Establishment.Code,
            emissionPoint?.Code
        );

        return Result<SalesReceiptPrintPayloadDto>.Success(
            new SalesReceiptPrintPayloadDto(
                TenantId: invoice.TenantId,
                CompanyId: invoice.CompanyId,
                BranchId: invoice.BranchId,
                CompanyName: company.LegalName,
                TradeName: company.TradeName,
                Ruc: company.TaxIdentificationNumber,
                BranchName: branch.Name,
                EstablishmentCode: establishmentCode,
                EmissionPointCode: emissionPointCode,
                CashRegisterName: cashSession?.CashRegisterNameSnapshot,
                CashSessionId: invoice.CashSessionId,
                InvoiceId: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                IssuedAt: invoice.IssueDate,
                CustomerName: invoice.Customer.Name,
                CustomerIdentification: invoice.Customer.TaxId,
                CustomerEmail: invoice.Customer.Email,
                DocumentType: invoice.DocTypeCode,
                IsElectronic: invoice.EmissionType == EmissionType.Electronic,
                ElectronicStatus: electronicDocument?.CurrentState.ToString(),
                AccessKey: electronicDocument?.AccessKey?.Value,
                AuthorizationNumber: electronicDocument?.AuthorizationNumber?.Value,
                AuthorizationDate: electronicDocument?.AuthorizationDate,
                Lines: invoice.Lines.OrderBy(line => line.SortOrder)
                    .Select(SalesReceiptPrintPayloadMapper.MapLine)
                    .ToArray(),
                Totals: new SalesReceiptTotalsDto(
                    invoice.Subtotal,
                    invoice.TotalDiscount,
                    invoice.TotalVat,
                    invoice.GrandTotal
                ),
                Payments: invoice.Payments.OrderBy(payment => payment.CreatedAt)
                    .Select(SalesReceiptPrintPayloadMapper.MapPayment)
                    .ToArray(),
                CashReceived: null,
                CashChange: null,
                FooterMessage: branding.DocumentFooterText ?? company.ExtraLegend
            )
        );
    }
}
