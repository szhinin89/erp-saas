using FluentValidation;

namespace ERP.Application.Modules.Compras.UseCases.CrearCompra;

public sealed class CrearCompraCommandValidator : AbstractValidator<CrearCompraCommand>
{
    public CrearCompraCommandValidator()
    {
        // ── XML ──────────────────────────────────────────────────────────
        When(x => x.Modo == ModoCreacionCompra.Xml, () =>
        {
            RuleFor(x => x.XmlContent)
                .NotNull().WithMessage("El contenido del archivo XML es requerido en modo XML.")
                .Must(b => b is { Length: > 0 }).WithMessage("El archivo XML no puede estar vacío.");
        });

        // ── Manual ───────────────────────────────────────────────────────
        When(x => x.Modo == ModoCreacionCompra.Manual, () =>
        {
            RuleFor(x => x.ProveedorId)
                .NotEmpty().WithMessage("El proveedor es obligatorio en modo manual.");

            RuleFor(x => x.NumeroFactura)
                .NotEmpty().WithMessage("El número de factura es obligatorio en modo manual.")
                .MaximumLength(50);

            RuleFor(x => x.FechaFactura)
                .NotNull().WithMessage("La fecha de factura es obligatoria en modo manual.");

            RuleFor(x => x.CondicionPago)
                .NotEmpty().WithMessage("La condición de pago es obligatoria en modo manual.")
                .MaximumLength(30);

            RuleFor(x => x.Detalles)
                .NotEmpty().WithMessage("Debe ingresar al menos un detalle en modo manual.");

            RuleForEach(x => x.Detalles).ChildRules(d =>
            {
                d.RuleFor(l => l.Descripcion)
                    .NotEmpty().WithMessage("La descripción del detalle es obligatoria.");
                d.RuleFor(l => l.Cantidad)
                    .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
                d.RuleFor(l => l.PrecioUnitario)
                    .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
            });
        });
    }
}
