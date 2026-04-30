namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductLine;

public record UpdateProductLineCommand(Guid Id, string Code, string Name);

