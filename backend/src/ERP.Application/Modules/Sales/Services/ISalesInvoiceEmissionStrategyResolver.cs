using ERP.Domain.Modules.Company.Enums;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Resuelve la <see cref="ISalesInvoiceEmissionStrategy"/> correspondiente al
/// <see cref="EmissionType"/> del Punto de Emisión — único lugar donde se mapea tipo→estrategia.
/// </summary>
public interface ISalesInvoiceEmissionStrategyResolver
{
    ISalesInvoiceEmissionStrategy Resolve(EmissionType emissionType);
}

public sealed class SalesInvoiceEmissionStrategyResolver : ISalesInvoiceEmissionStrategyResolver
{
    private readonly IReadOnlyDictionary<EmissionType, ISalesInvoiceEmissionStrategy> _strategies;

    public SalesInvoiceEmissionStrategyResolver(IEnumerable<ISalesInvoiceEmissionStrategy> strategies)
        => _strategies = strategies.ToDictionary(s => s.SupportedType);

    public ISalesInvoiceEmissionStrategy Resolve(EmissionType emissionType)
        => _strategies.TryGetValue(emissionType, out var strategy)
            ? strategy
            : throw new InvalidOperationException(
                $"No hay estrategia de emisión de Ventas registrada para el tipo '{emissionType}'.");
}
