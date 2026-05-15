using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class Warehouse : MasterEntity, ITenantEntity
{
    public const int NameMaxLen    = 100;
    public const int AddressMaxLen = 300;
    public const int ManagerMaxLen = 100;

    public Guid    BranchId        { get; private set; }
    public string  Name            { get; private set; } = null!;
    public string? Address         { get; private set; }
    public string? Manager         { get; private set; }
    public Guid?   EstablishmentId { get; private set; }

    private Warehouse() { }

    public static Warehouse Create(
        Guid    tenantId,
        Guid    branchId,
        string  name,
        string? address,
        string? manager,
        Guid    createdBy,
        Guid?   establishmentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Warehouse name is required.", nameof(name));

        var w = new Warehouse
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            BranchId        = branchId,
            Name            = name.Trim(),
            Address         = Trim(address),
            Manager         = Trim(manager),
            EstablishmentId = establishmentId,
        };
        w.SetCreated(createdBy);
        return w;
    }

    public void Update(
        Guid    branchId,
        string  name,
        string? address,
        string? manager,
        Guid    updatedBy,
        Guid?   establishmentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Warehouse name is required.", nameof(name));

        BranchId        = branchId;
        Name            = name.Trim();
        Address         = Trim(address);
        Manager         = Trim(manager);
        EstablishmentId = establishmentId;
        SetUpdated(updatedBy);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
