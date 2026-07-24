using ERP.Domain.Access.Enums;
using FluentValidation;

namespace ERP.Application.Access.UseCases.GetUserSessionsPaged;

public sealed class GetUserSessionsPagedValidator : AbstractValidator<GetUserSessionsPagedQuery>
{
    public GetUserSessionsPagedValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser mayor o igual a 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200)
            .WithMessage("El tamaño de página debe estar entre 1 y 200.");
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || Enum.TryParse<UserSessionStatus>(s, true, out _))
            .WithMessage("El estado indicado no es válido.");
        RuleFor(x => x)
            .Must(x => x.FromUtc is null || x.ToUtc is null || x.FromUtc <= x.ToUtc)
            .WithMessage("La fecha inicial no puede ser posterior a la fecha final.")
            .OverridePropertyName("FromUtc");
    }
}
