using ERP.Domain.Products.Entities;

namespace ERP.Application.Products.Catalogs.UseCases.CreateTaxRate;

public record CreateTaxRateCommand(
    string Code,
    string Name,
    TaxRateType Type,
    decimal Percentage
);

