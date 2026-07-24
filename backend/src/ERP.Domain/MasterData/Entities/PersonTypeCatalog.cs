namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Catálogo global de solo lectura con las etiquetas de <c>ERP.Domain.MasterData.Enums.PersonType</c>
/// — mismo patrón que <c>SriVatRate</c>. El enum sigue siendo la fuente de verdad para
/// almacenamiento y validación (<c>BusinessPartner.PersonType</c>, columna <c>person_type</c>
/// sin cambios); esta tabla existe únicamente para que el frontend obtenga las opciones/etiquetas
/// del <c>&lt;select&gt;</c> de forma dinámica en vez de un array hardcodeado. <c>Code</c> coincide
/// numéricamente con el valor subyacente del enum (1=Natural, 2=Legal, 3=Government, 4=Organization).
/// </summary>
public class PersonTypeCatalog
{
    public short  Code { get; set; }
    public string Name { get; set; } = null!;
}
