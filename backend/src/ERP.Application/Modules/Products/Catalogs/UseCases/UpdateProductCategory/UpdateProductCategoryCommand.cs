namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductCategory;

public record UpdateProductCategoryCommand(Guid Id, string Code, string Name, Guid LineId);
