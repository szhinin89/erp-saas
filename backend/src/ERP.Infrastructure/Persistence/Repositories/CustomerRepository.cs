using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public CustomerRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<Customer> Scoped(Guid subscriberId) =>
        _context.Customers.ForOperationalScope(subscriberId, _company);

    public Task AddAsync(Customer customer, CancellationToken ct = default)
        => _context.Customers.AddAsync(customer, ct).AsTask();

    public Task<Customer?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => Scoped(subscriberId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsIdentificationAsync(
        Guid subscriberId,
        string identificationType,
        string identificationNumber,
        Guid? excludeCustomerId,
        CancellationToken ct = default)
    {
        var type = Customer.NormalizeIdentificationType(identificationType);
        var number = Customer.NormalizeIdentificationNumber(identificationNumber);
        var q = Scoped(subscriberId).Where(x =>
            x.IdentificationType == type &&
            x.IdentificationNumber == number);
        if (excludeCustomerId is not null)
            q = q.Where(x => x.Id != excludeCustomerId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Customer>> GetAsync(
        Guid subscriberId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default)
    {
        var q = Scoped(subscriberId);
        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.LegalName.ToLower().Contains(s) ||
                (x.TradeName != null && x.TradeName.ToLower().Contains(s)) ||
                x.IdentificationNumber.ToLower().Contains(s) ||
                (x.Email != null && x.Email.ToLower().Contains(s)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(s)));
        }

        return await q.OrderBy(x => x.LegalName).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
