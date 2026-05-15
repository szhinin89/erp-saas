using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearCompra;

public sealed class CrearPurchaseCommandValidator : AbstractValidator<CrearPurchaseCommand>
{
    public CrearPurchaseCommandValidator()
    {
        // ── XML ──────────────────────────────────────────────────────────
        When(x => x.Modo == ModoCreacionPurchase.Xml, () =>
        {
            RuleFor(x => x.XmlContent)
                .NotNull().WithMessage("El contenido del archivo XML es requerido en modo XML.")
                .Must(b => b is { Length: > 0 }).WithMessage("El archivo XML no puede estar vacío.");
        });

        // ── Manual ───────────────────────────────────────────────────────
        When(x => x.Modo == ModoCreacionPurchase.Manual, () =>
        {
            RuleFor(x => x.SupplierId)
                .NotEmpty().WithMessage("El Supplier es obligatorio en modo manual.");

            RuleFor(x => x.InvoiceNumber)
                .NotEmpty().WithMessage("El número de factura es obligatorio en modo manual.")
                .MaximumLength(50);

            RuleFor(x => x.InvoiceDate)
                .NotNull().WithMessage("La fecha de factura es obligatoria en modo manual.");

            RuleFor(x => x.PaymentTerms)
                .NotEmpty().WithMessage("La condición de pago es obligatoria en modo manual.")
                .MaximumLength(30);

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("Debe ingresar al menos un detalle en modo manual.");

            RuleForEach(x => x.Lines).ChildRules(d =>
            {
                d.RuleFor(l => l.Description)
                    .NotEmpty().WithMessage("La descripción del detalle es obligatoria.");
                d.RuleFor(l => l.Quantity)
                    .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
                d.RuleFor(l => l.UnitPrice)
                    .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
            });
        });
    }
}
