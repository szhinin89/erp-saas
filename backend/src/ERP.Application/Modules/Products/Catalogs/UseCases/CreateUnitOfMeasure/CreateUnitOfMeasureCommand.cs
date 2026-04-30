namespace ERP.Application.Products.Catalogs.UseCases.CreateUnitOfMeasure;

public record CreateUnitOfMeasureCommand(string Code, string Name, string? Symbol = null);

