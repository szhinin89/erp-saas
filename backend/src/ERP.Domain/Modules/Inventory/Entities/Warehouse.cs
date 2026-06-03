using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class Warehouse : MasterEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int NameMaxLen            = 100;
    public const int AddressMaxLen         = 300;
    public const int ManagerMaxLen         = 100;
    public const int CodeMaxLen            = 20;
    public const int StorageTypeMaxLen     = 50;
    public const int PhoneMaxLen           = 50;
    public const int EmailMaxLen           = 150;
    public const int LatLonMaxLen          = 25;

    public Guid    BranchId         { get; private set; }
    public string  Name             { get; private set; } = null!;
    public string? Code             { get; private set; }
    public string? StorageType      { get; private set; }
    public string? Address          { get; private set; }
    public string? Phone            { get; private set; }
    public string? Email            { get; private set; }
    public string? Manager          { get; private set; }
    public string? Latitude         { get; private set; }
    public string? Longitude        { get; private set; }
    public decimal? Capacity        { get; private set; }
    public decimal? DailyDispatchGoal { get; private set; }
    public Guid?   EstablishmentId  { get; private set; }

    private Warehouse() { }

    public static Warehouse Create(
        Guid     subscriberId,
        Guid     branchId,
        string   name,
        string   code,
        string?  storageType,
        string?  address,
        string?  phone,
        string?  email,
        string?  manager,
        string?  latitude,
        string?  longitude,
        decimal? capacity,
        decimal? dailyDispatchGoal,
        Guid     createdBy,
        Guid     companyId,
        Guid?    establishmentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Warehouse name is required.", nameof(name));

        var w = new Warehouse
        {
            Id               = Guid.NewGuid(),
            SubscriberId         = subscriberId,
            BranchId         = branchId,
            Name             = name.Trim(),
            Code             = code,
            StorageType      = Trim(storageType),
            Address          = Trim(address),
            Phone            = Trim(phone),
            Email            = Trim(email),
            Manager          = Trim(manager),
            Latitude         = Trim(latitude),
            Longitude        = Trim(longitude),
            Capacity         = capacity,
            DailyDispatchGoal = dailyDispatchGoal,
            CompanyId = companyId,
            EstablishmentId  = establishmentId,
        };
        w.SetCreated(createdBy);
        return w;
    }

    public void Update(
        Guid     branchId,
        string   name,
        string?  storageType,
        string?  address,
        string?  phone,
        string?  email,
        string?  manager,
        string?  latitude,
        string?  longitude,
        decimal? capacity,
        decimal? dailyDispatchGoal,
        Guid     updatedBy,
        Guid?    establishmentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Warehouse name is required.", nameof(name));

        BranchId          = branchId;
        Name              = name.Trim();
        StorageType       = Trim(storageType);
        Address           = Trim(address);
        Phone             = Trim(phone);
        Email             = Trim(email);
        Manager           = Trim(manager);
        Latitude          = Trim(latitude);
        Longitude         = Trim(longitude);
        Capacity          = capacity;
        DailyDispatchGoal = dailyDispatchGoal;
        EstablishmentId   = establishmentId;
        SetUpdated(updatedBy);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
