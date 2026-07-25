using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Contrato público del Posting Engine (ADR-026 §8): Domain Event → PostingFact → PostingRule →
/// JournalEntry. El pipeline interno (idempotencia, resolución de regla/período, construcción
/// y validación del asiento) es un detalle de implementación — no se expone aquí.
/// </summary>
public interface IPostingEngine
{
    /// <summary>
    /// Ejecuta validación y preparación del asiento contable — deja las entidades en staging
    /// (agregadas al <c>ChangeTracker</c> del <c>ErpDbContext</c> ambiente) pero <b>no garantiza
    /// persistencia física inmediata</b>. La persistencia real depende de que el ciclo externo
    /// que originó la llamada ejecute su propio <c>SaveChangesAsync</c>. En producción:
    /// <c>ErpDbContext.SaveChangesAsync()</c> → Domain Event Publish → <c>PostAsync</c> →
    /// flush final del mismo <c>SaveChangesAsync</c> (ADR-026 §8, Fase 3.3.1). Un consumidor
    /// que invoque <c>PostAsync</c> fuera de ese ciclo es responsable de comitear él mismo.
    /// </summary>
    Task<Result<PostingOutcomeDto>> PostAsync(PostingFact fact, CancellationToken cancellationToken = default);
}
