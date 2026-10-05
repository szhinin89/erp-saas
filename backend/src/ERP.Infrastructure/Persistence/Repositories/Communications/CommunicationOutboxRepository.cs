using System.Data.Common;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace ERP.Infrastructure.Persistence.Repositories.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — encolado idempotente con autoridad en PostgreSQL.
/// <para>
/// <c>INSERT … ON CONFLICT DO NOTHING</c> contra <c>ux_communication_outbox_idempotency</c>: dos
/// encolados concurrentes de la misma identidad producen UNA fila; el perdedor espera al ganador
/// (si éste revierte, inserta él) y luego lee la existente. La colisión esperable nunca lanza ni
/// aborta la transacción ambiente — a diferencia de <c>Add</c> + <c>SaveChanges</c>, cuya violación
/// de índice único revertiría el hecho de negocio (p. ej. la autorización SRI).
/// </para>
/// <para>
/// Escribe de inmediato: dentro de la transacción ambiente si existe (atómico con el hecho), o en
/// una transacción propia corta (comunicación + adjuntos). Las columnas y conversiones salen del
/// modelo EF, así que el SQL no puede divergir del mapeo.
/// </para>
/// </summary>
public sealed class CommunicationOutboxRepository : ICommunicationOutboxRepository
{
    private readonly ErpDbContext _db;

    public CommunicationOutboxRepository(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<CommunicationEnqueueResult> EnqueueAsync(
        CommunicationOutbox communication,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(communication);
        if (string.IsNullOrWhiteSpace(communication.IdempotencyKey))
            throw new InvalidOperationException(
                "Una comunicación encolada requiere su identidad (IdempotencyKey)."
            );

        if (_db.Database.CurrentTransaction is not null)
            return await EnqueueCoreAsync(communication, ct);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            var result = await EnqueueCoreAsync(communication, ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private async Task<CommunicationEnqueueResult> EnqueueCoreAsync(
        CommunicationOutbox communication,
        CancellationToken ct
    )
    {
        if (await InsertAsync(communication, onConflictDoNothing: true, ct) == 0)
        {
            // Identidad ya encolada: la clave incluye el alcance, se busca por (tenant, empresa, clave)
            // por la vía explícita de plataforma (una fila System no es visible desde ningún contexto).
            var existingId = await _db
                .CommunicationOutbox.AsPlatformQuery()
                .Where(x =>
                    x.TenantId == communication.TenantId
                    && x.CompanyId == communication.CompanyId
                    && x.IdempotencyKey == communication.IdempotencyKey
                )
                .Select(x => x.Id)
                .SingleAsync(ct);
            return new CommunicationEnqueueResult(existingId, Created: false);
        }

        foreach (var attachment in communication.Attachments)
            await InsertAsync(attachment, onConflictDoNothing: false, ct);

        return new CommunicationEnqueueResult(communication.Id, Created: true);
    }

    private Task<int> InsertAsync(object entity, bool onConflictDoNothing, CancellationToken ct)
    {
        var entry = _db.Entry(entity);
        var entityType = entry.Metadata;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

        var columns = new List<string>();
        var parameters = new List<DbParameter>();
        foreach (var property in entityType.GetProperties())
        {
            var column = property.GetColumnName(table);
            if (column is null)
                continue;

            var value = entry.Property(property.Name).CurrentValue;
            var converter = property.GetTypeMapping().Converter;
            var providerValue = value is null ? null : converter?.ConvertToProvider(value) ?? value;

            columns.Add($"\"{column}\"");
            parameters.Add(
                new NpgsqlParameter($"p{parameters.Count}", providerValue ?? DBNull.Value)
            );
        }

        var qualifiedTable = table.Schema is null
            ? $"\"{table.Name}\""
            : $"\"{table.Schema}\".\"{table.Name}\"";
        var sql =
            $"INSERT INTO {qualifiedTable} ({string.Join(", ", columns)}) "
            + $"VALUES ({string.Join(", ", parameters.Select(p => "@" + p.ParameterName))})"
            + (onConflictDoNothing ? " ON CONFLICT DO NOTHING" : string.Empty);

        return _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }
}
