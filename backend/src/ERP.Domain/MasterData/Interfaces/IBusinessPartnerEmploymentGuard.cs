namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Puerto propiedad de MasterData (EMP-T-03, E-D3, EMP-BR-17, INV-10): un BusinessPartner con una
/// ficha laboral activa en cualquier empresa del tenant no se desactiva ni pierde el rol Employee.
/// MasterData no conoce las entidades de Employees; la implementación vive en Infrastructure.
///
/// PROTOCOLO (obligatorio, dentro de una transacción activa): primero
/// <see cref="AcquireBpEmploymentLockAsync"/>, después <see cref="HasActiveEmploymentAsync"/>, y
/// recién entonces la escritura. Los casos de uso de Employees que crean o reincorporan fichas
/// toman el mismo lock antes de verificar el BP, lo que serializa ambas carreras.
/// </summary>
public interface IBusinessPartnerEmploymentGuard
{
    /// <summary>
    /// Lock transaccional exclusivo <c>("BpEmployment", tenantId, businessPartnerId)</c>: se libera
    /// al confirmar o revertir la transacción ambiente. Exige una transacción activa.
    /// </summary>
    Task AcquireBpEmploymentLockAsync(
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// ¿Existe una ficha laboral activa del BP en alguna empresa del tenant? Devuelve únicamente un
    /// booleano: no revela empresa, ficha ni cantidad. <paramref name="tenantId"/> debe provenir del
    /// contexto autenticado, nunca de datos del cliente.
    /// </summary>
    Task<bool> HasActiveEmploymentAsync(
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    );
}
