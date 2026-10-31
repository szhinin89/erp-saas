using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;

/// <summary>
/// Pestaña "Fiscal" de Configuración → Empresa. Las designaciones por resolución (Anexos 21 y 24)
/// viajan como pares número + vigencia: ambos vacíos retiran la designación.
/// </summary>
public sealed record UpdateCompanyFiscalCommand(
    string? TaxRegimeCode,
    bool IsAccountingReq,
    string? SpecialTaxpayerNo,
    bool IsForeignTrade,
    bool WithholdsRenta,
    bool WithholdsVat,
    string? RetentionAgentResolutionNo = null,
    DateOnly? RetentionAgentEffectiveFrom = null,
    string? LargeTaxpayerResolutionNo = null,
    DateOnly? LargeTaxpayerEffectiveFrom = null
) : IRequest<Result<CompanyProfileDto>>;
