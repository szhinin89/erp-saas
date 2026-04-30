namespace ERP.Application.Products.Catalogs.UseCases.CreateProductSubcategory;

public record CreateProductSubcategoryCommand(string Code, string Name, Guid CategoryId);

