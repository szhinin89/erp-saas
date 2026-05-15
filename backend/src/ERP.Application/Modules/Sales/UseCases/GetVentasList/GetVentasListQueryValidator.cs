using FluentValidation;

namespace ERP.Application.Sales.UseCases.GetVentasList;

public sealed class GetVentasListQueryValidator : AbstractValidator<GetVentasListQuery>
{
    public GetVentasListQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("El número de página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("La fecha hasta debe ser mayor o igual a la fecha desde.");
    }
}
