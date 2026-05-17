namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Genera la Representación Impresa del Documento Electrónico (RIDE) en PDF.
/// Sin dependencias de HTTP ni de presentación — reutilizable desde API,
/// jobs Hangfire, envío por email, CLI u otros contextos.
/// </summary>
public interface IRideGeneratorService
{
    /// <summary>Genera el RIDE en PDF de una factura de venta autorizada.</summary>
    /// <param name="salesBillId">ID de la factura.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Bytes del PDF generado.</returns>
    Task<byte[]> GenerateFacturaPdfAsync(Guid salesBillId, CancellationToken ct = default);

    /// <summary>Genera el RIDE en PDF de una nota de crédito o débito autorizada.</summary>
    Task<byte[]> GenerateNotaPdfAsync(Guid salesNoteId, CancellationToken ct = default);
}
