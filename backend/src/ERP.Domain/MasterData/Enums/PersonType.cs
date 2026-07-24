namespace ERP.Domain.MasterData.Enums;

/// <summary>
/// Tipo de persona del BusinessPartner.
/// Determina el algoritmo de validación del RUC (Módulo 10 vs Módulo 11)
/// y el tratamiento tributario en documentos SRI.
/// </summary>
public enum PersonType : short
{
    Natural      = 1,
    Legal        = 2,
    Government   = 3,
    Organization = 4,
}
