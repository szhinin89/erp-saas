using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class Warehouse : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int NameMaxLen = 100;
    public const int AddressMaxLen = 300;
    public const int ManagerMaxLen = 100;
    public const int CodeMaxLen = 20;
    public const int StorageTypeMaxLen = 50;
    public const int PhoneMaxLen = 50;
    public const int EmailMaxLen = 150;
    public const int LatLonMaxLen = 25;

    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public string? StorageType { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Manager { get; private set; }
    public string? Latitude { get; private set; }
    public string? Longitude { get; private set; }
    public decimal? Capacity { get; private set; }
    public decimal? DailyDispatchGoal { get; private set; }
    public bool IsMain { get; private set; }
    public Guid? EstablishmentId { get; private set; }

    private Warehouse() { }

    public static Warehouse Create(
        Guid tenantId,
        Guid branchId,
        string name,
        string code,
        string? storageType,
        string? address,
        string? phone,
        string? email,
        string? manager,
        string? latitude,
        string? longitude,
        decimal? capacity,
        decimal? dailyDispatchGoal,
        Guid createdBy,
        Guid companyId,
        bool isMain = false,
        Guid? establishmentId = null
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Warehouse name is required.", nameof(name));

        var w = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            Name = name.Trim(),
            Code = code,
            StorageType = Trim(storageType),
            Address = Trim(address),
            Phone = Trim(phone),
            Email = Trim(email),
            Manager = Trim(manager),
            Latitude = Trim(latitude),
            Longitude = Trim(longitude),
            Capacity = capacity,
            DailyDispatchGoal = dailyDispatchGoal,
            CompanyId = companyId,
            IsMain = isMain,
            EstablishmentId = establishmentId,
        };
        w.SetCreated(createdBy);
        return w;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Bodega Principal), ver política de Bootstrap en
    /// <c>CLAUDE.md</c>. <see cref="Update"/> permanece abierto.
    /// </summary>
    public static Warehouse CreateSystemSeeded(
        Guid tenantId,
        Guid branchId,
        string name,
        string code,
        string? storageType,
        string? address,
        string? phone,
        string? email,
        string? manager,
        string? latitude,
        string? longitude,
        decimal? capacity,
        decimal? dailyDispatchGoal,
        Guid createdBy,
        Guid companyId,
        bool isMain = false,
        Guid? establishmentId = null
    )
    {
        var w = Create(
            tenantId,
            branchId,
            name,
            code,
            storageType,
            address,
            phone,
            email,
            manager,
            latitude,
            longitude,
            capacity,
            dailyDispatchGoal,
            createdBy,
            companyId,
            isMain,
            establishmentId
        );
        w.MarkAsSystemSeeded();
        return w;
    }

    public void Update(
        Guid branchId,
        string name,
        string? storageType,
        string? address,
        string? phone,
        string? email,
        string? manager,
        string? latitude,
        string? longitude,
        decimal? capacity,
        decimal? dailyDispatchGoal,
        Guid updatedBy,
        Guid? establishmentId = null
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Warehouse name is required.", nameof(name));

        BranchId = branchId;
        Name = name.Trim();
        StorageType = Trim(storageType);
        Address = Trim(address);
        Phone = Trim(phone);
        Email = Trim(email);
        Manager = Trim(manager);
        Latitude = Trim(latitude);
        Longitude = Trim(longitude);
        Capacity = capacity;
        DailyDispatchGoal = dailyDispatchGoal;
        EstablishmentId = establishmentId;
        SetUpdated(updatedBy);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La bodega", "deshabilitarse");

        base.Disable(updatedBy);
    }
}
