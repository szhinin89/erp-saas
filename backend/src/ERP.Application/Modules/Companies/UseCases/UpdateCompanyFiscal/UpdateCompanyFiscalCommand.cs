using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;

public sealed record UpdateCompanyFiscalCommand(
    string? TaxRegimeCode,
    bool IsAccountingReq,
    string? SpecialTaxpayerNo,
    bool IsForeignTrade,
    bool WithholdsRenta,
    bool WithholdsVat
) : IRequest<Result<CompanyProfileDto>>;
