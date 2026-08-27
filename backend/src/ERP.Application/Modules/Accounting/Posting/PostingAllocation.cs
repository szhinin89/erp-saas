using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// EXPENSES-POSTING-ALLOCATIONS-06 — línea dinámica de asiento con cuenta propia, para hechos
/// contables cuya cardinalidad de cuentas no se conoce en tiempo de configuración de
/// <see cref="Entities.PostingRule"/> (p. ej. Gastos: N <c>ExpenseLine</c>, cada una con su propia
/// <c>AccountingAccountId</c> de subcategoría). Complementa, sin reemplazar, el mecanismo existente
/// de <see cref="Entities.PostingRuleLine"/> (cuentas fijas, 1 por <c>AmountKind</c>) — un mismo
/// <see cref="PostingFact"/> puede combinar ambos: líneas fijas (p. ej. IVA acreditable, cuenta por
/// pagar) vía <c>PostingRule.Lines</c>, y líneas dinámicas (p. ej. una por cuenta de gasto) vía
/// <see cref="PostingFact.Allocations"/>. Puramente mecánico (AccountingAccountId + Amount +
/// Nature) — sin condicionales por SourceModule/FactType (ADR-026 §6.2), así que cualquier módulo
/// futuro con la misma necesidad (comisiones, nómina, etc.) puede reutilizarlo.
/// </summary>
/// <remarks>
/// Validación fail-fast en el constructor (mismo criterio que <c>JournalEntryLine.Create</c>/
/// <c>PostingRuleLine.Create</c>): una allocation sin cuenta o con monto no positivo nunca llega a
/// existir como objeto, así que <see cref="JournalFactory"/> jamás recibe una allocation inválida
/// que emitir. <see cref="SourceLineId"/> es trazabilidad opcional hacia la línea de origen del
/// módulo emisor (p. ej. <c>ExpenseLine.Id</c>) — hoy no se persiste en <see cref="Entities.JournalEntryLine"/>
/// (esa entidad no tiene ese campo; agregarlo requeriría migración EF, fuera de alcance de esta
/// fase); un traductor que lo necesite puede incorporarlo al texto de <see cref="Description"/>.
/// </remarks>
public sealed record PostingAllocation
{
    public Guid AccountingAccountId { get; }
    public decimal Amount { get; }
    public AccountNature Nature { get; }
    public string? Description { get; }
    public Guid? SourceLineId { get; }

    public PostingAllocation(
        Guid accountingAccountId,
        decimal amount,
        AccountNature nature,
        string? description = null,
        Guid? sourceLineId = null
    )
    {
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable de la allocation es obligatoria.",
                nameof(accountingAccountId)
            );
        if (amount <= 0m)
            throw new ArgumentException(
                "El monto de la allocation debe ser mayor a cero.",
                nameof(amount)
            );

        AccountingAccountId = accountingAccountId;
        Amount = amount;
        Nature = nature;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SourceLineId = sourceLineId;
    }
}
