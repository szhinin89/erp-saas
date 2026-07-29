namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Configuración específica del rol Carrier. Solo existe cuando RoleType = Carrier.
///
/// Datos requeridos para emitir guías de remisión electrónicas SRI.
/// Simplificación consciente: asume un perfil de transporte por carrier.
/// Ver ADR-BP-15 para limitaciones y ruta de evolución a gestión de flota.
/// Almacenado en tabla master_bp_carrier_configs (1:1 con master_bp_roles).
/// </summary>
public sealed record CarrierRoleConfig
{
    public const int AuthNumberMaxLen = 50;

    public string? TransportAuthorizationNumber { get; }
    public decimal? VehicleCapacityTons { get; }

    private CarrierRoleConfig(string? transportAuthorizationNumber, decimal? vehicleCapacityTons)
    {
        TransportAuthorizationNumber = transportAuthorizationNumber;
        VehicleCapacityTons = vehicleCapacityTons;
    }

    public static CarrierRoleConfig Create(
        string? transportAuthorizationNumber = null,
        decimal? vehicleCapacityTons = null
    )
    {
        var auth = transportAuthorizationNumber?.Trim();
        if (auth is { Length: 0 })
            auth = null;
        if (auth?.Length > AuthNumberMaxLen)
            throw new ArgumentException(
                $"El número de autorización no puede superar {AuthNumberMaxLen} caracteres.",
                nameof(transportAuthorizationNumber)
            );

        if (vehicleCapacityTons is <= 0)
            throw new ArgumentException(
                "La capacidad del vehículo debe ser mayor a cero.",
                nameof(vehicleCapacityTons)
            );

        return new CarrierRoleConfig(auth, vehicleCapacityTons);
    }
}
