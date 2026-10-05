using ERP.Domain.Modules.Communications.Entities;

namespace ERP.Domain.Modules.Communications.Interfaces;

public interface ICommunicationOutboxRepository
{
    /// <summary>
    /// ZH-COMMUNICATIONS-CONTRACT-01 — encolado idempotente con autoridad en PostgreSQL
    /// (<c>INSERT … ON CONFLICT DO NOTHING</c> sobre el índice único de idempotencia). Escribe de
    /// inmediato, dentro de la transacción ambiente si existe (atómico con el hecho de negocio).
    /// Una identidad ya encolada devuelve la existente; nunca lanza por la colisión esperable ni
    /// aborta la transacción del negocio.
    /// </summary>
    Task<CommunicationEnqueueResult> EnqueueAsync(
        CommunicationOutbox communication,
        CancellationToken ct = default
    );
}

public sealed record CommunicationEnqueueResult(Guid Id, bool Created);
