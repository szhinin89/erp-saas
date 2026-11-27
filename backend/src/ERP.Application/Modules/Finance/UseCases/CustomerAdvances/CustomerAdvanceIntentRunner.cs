using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 sobre anticipos (ADR-052 §5) — camino único de toda operación
/// con clave: replay sin efectos → una transacción → si el intento pierde por cualquier motivo y la
/// intención ya quedó registrada, se responde con ella. Misma huella → el anticipo; otra huella (u
/// otro tipo de operación) → 409; clave de otra empresa del tenant → 409 sin datos de ella.
/// </summary>
internal sealed class CustomerAdvanceIntentRunner(
    ICustomerAdvanceRepository advances,
    IBusinessPartnerRepository partners,
    IUnitOfWork uow,
    IDatabaseExceptionTranslator databaseErrors,
    ICurrentTenant tenant
)
{
    public const string ClientRequestConflict =
        "Ya existe una operación con este identificador pero con datos distintos.";
    private const string KeyIndexPrefix = "uq_customer_advance_movements_tenant_request";

    public async Task<Result<CustomerAdvanceDto>> ExecuteAsync(
        ClientRequestKey key,
        Func<CancellationToken, Task<Result<CustomerAdvanceDto>>> operation,
        CancellationToken ct
    )
    {
        // Camino rápido del replay: ningún efecto.
        if (await ReplayAsync(key, ct) is { } replay)
            return replay;

        await uow.BeginTransactionAsync(ct);
        Result<CustomerAdvanceDto> result;
        try
        {
            result = await operation(ct);
            if (result.IsSuccess)
                await uow.CommitAsync(ct);
            else
                await uow.RollbackAsync(ct);
        }
        catch (Exception ex)
        {
            // El intento se revierte completo y libera los locks; lo que quedó en memoria se
            // descarta para leer únicamente lo confirmado.
            await uow.RollbackAsync(ct);
            uow.ClearChangeTracker();
            if (await ReplayAsync(key, ct) is { } raced)
                return raced;
            if (IsKeyViolation(ex))
                return Result<CustomerAdvanceDto>.Conflict(ClientRequestConflict);
            throw;
        }
        if (result.IsSuccess)
            return result;

        return await ReplayAsync(key, ct) ?? result;
    }

    /// <summary>
    /// Replay sobre un anticipo ya cargado (p. ej. bajo su lock): null si nunca registró la clave.
    /// </summary>
    public async Task<Result<CustomerAdvanceDto>?> ReplayFromAsync(
        CustomerAdvance advance,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var original = advance
            .Movements.Where(m => m.ClientRequestId == key.Id)
            .OrderBy(m => m.LineNumber)
            .FirstOrDefault();
        if (original is null)
            return null;
        return key.Matches(original.RequestPayloadHash)
            ? await SuccessAsync(advance, ct)
            : Result<CustomerAdvanceDto>.Conflict(ClientRequestConflict);
    }

    public async Task<Result<CustomerAdvanceDto>> SuccessAsync(
        CustomerAdvance advance,
        CancellationToken ct
    )
    {
        var names = await partners.GetNamesByIdsAsync([advance.CustomerId], ct);
        return Result<CustomerAdvanceDto>.Success(
            CustomerAdvanceMap.ToDto(advance, names.GetValueOrDefault(advance.CustomerId))
        );
    }

    private async Task<Result<CustomerAdvanceDto>?> ReplayAsync(
        ClientRequestKey key,
        CancellationToken ct
    ) =>
        await advances.GetByClientRequestIdAsync(tenant.TenantId, key.Id, ct) is { } existing
            ? await ReplayFromAsync(existing, key, ct)
            : null;

    private bool IsKeyViolation(Exception ex) =>
        databaseErrors.TryGetUniqueViolation(ex, out var violation)
        && violation.ConstraintName?.StartsWith(KeyIndexPrefix, StringComparison.Ordinal) == true;
}
