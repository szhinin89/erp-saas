using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Bodega / almacén asociada a una sucursal del tenant.
/// Soft delete vía <see cref="MasterEntity"/>.
/// </summary>
public sealed class Bodega : MasterEntity, ITenantEntity
{
    public const int NombreMaxLen     = 100;
    public const int UbicacionMaxLen  = 300;
    public const int EncargadoMaxLen  = 100;

    public Guid   SucursalId  { get; private set; }
    public string Nombre      { get; private set; } = null!;
    public string? Ubicacion  { get; private set; }
    public string? Encargado  { get; private set; }

    // ── Campo SRI ────────────────────────────────────────────────
    /// <summary>
    /// Establecimiento SRI al que pertenece esta bodega.
    /// FK → establishment.id. Null si no está vinculada a un establecimiento electrónico.
    /// </summary>
    public Guid? EstablishmentId { get; private set; }

    private Bodega() { }

    public static Bodega Create(
        Guid   tenantId,
        Guid   sucursalId,
        string nombre,
        string? ubicacion,
        string? encargado,
        Guid   createdBy,
        Guid?  establishmentId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la bodega es obligatorio.", nameof(nombre));

        var b = new Bodega
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            SucursalId      = sucursalId,
            Nombre          = nombre.Trim(),
            Ubicacion       = Trim(ubicacion),
            Encargado       = Trim(encargado),
            EstablishmentId = establishmentId,
        };
        b.SetCreated(createdBy);
        return b;
    }

    public void Update(
        Guid   sucursalId,
        string nombre,
        string? ubicacion,
        string? encargado,
        Guid   updatedBy,
        Guid?  establishmentId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la bodega es obligatorio.", nameof(nombre));

        SucursalId      = sucursalId;
        Nombre          = nombre.Trim();
        Ubicacion       = Trim(ubicacion);
        Encargado       = Trim(encargado);
        EstablishmentId = establishmentId;
        SetUpdated(updatedBy);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
