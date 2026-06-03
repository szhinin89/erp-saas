using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Entities;

namespace ERP.Application.Products.UseCases.CreateTaxRate;

public record CreateTaxRateCommand(
    string  Code,
    string  Name,
    TaxRateType Type,
    /// <summary>Código de global.sri_vat_rate.Code (requerido si Type==VAT).</summary>
    string? SriVatCode,
    /// <summary>Código de global.sri_ice_rate.Code (requerido si Type==Excise).</summary>
    string? SriIceCode
) : IRequest<Result<TaxRateDto>>, ICompanyScopedRequest;

