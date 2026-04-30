namespace ERP.Application.Products.Catalogs.UseCases.CreateProductCategory;

public record CreateProductCategoryCommand(string Code, string Name, Guid LineId);

