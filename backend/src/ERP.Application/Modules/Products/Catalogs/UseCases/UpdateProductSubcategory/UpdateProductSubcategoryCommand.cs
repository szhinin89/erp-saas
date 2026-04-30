namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductSubcategory;

public record UpdateProductSubcategoryCommand(Guid Id, string Code, string Name, Guid CategoryId);
