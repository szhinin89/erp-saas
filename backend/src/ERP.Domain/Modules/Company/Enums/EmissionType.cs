namespace ERP.Domain.Modules.Company.Enums;

/// <summary>
/// Tipo de canal de emisión de un punto de emisión SRI.
/// </summary>
public enum EmissionType
{
    /// <summary>Emisión electrónica — documentos enviados al SRI por internet.</summary>
    Electronic = 1,

    /// <summary>Emisión física — documentos preimpresos o manuales.</summary>
    Physical = 2,
}
