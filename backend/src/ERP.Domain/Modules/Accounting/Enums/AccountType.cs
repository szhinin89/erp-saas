namespace ERP.Domain.Modules.Accounting.Enums;

/// <summary>
/// Clasificación contable universal de partida doble (ADR-026 §5) — no es un catálogo
/// tenant-editable, a diferencia de ItemTypeDefinition. Extensión futura únicamente como
/// valor nuevo dentro de este mismo conjunto, nunca como lógica condicional por país/tenant.
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense,

    /// <summary>
    /// ACCOUNTING-FINANCIAL-STATEMENTS-10: Costo de ventas — clasificación estándar del Plan de
    /// Cuentas ecuatoriano (grupo 5, distinto de Gastos/grupo 6), necesaria para separar
    /// "Utilidad bruta" (Ingresos − Costos) de "Utilidad neta" (Utilidad bruta − Gastos) en el
    /// Estado de Resultados. Agregado al FINAL del enum (nunca insertado entre valores
    /// existentes) porque <c>AccountConfiguration</c> mapea este enum a <c>int</c> plano en BD
    /// (<c>HasConversion&lt;int&gt;()</c>, sin CHECK constraint) — insertarlo antes de
    /// <see cref="Expense"/> habría corrido su valor persistido de 4 a 5 y corrompido todo
    /// <c>Account.AccountType</c> ya guardado. Mismo criterio de extensión ya documentado en la
    /// clase: "extensión futura únicamente como valor nuevo dentro de este mismo conjunto".
    /// </summary>
    Cost,
}
