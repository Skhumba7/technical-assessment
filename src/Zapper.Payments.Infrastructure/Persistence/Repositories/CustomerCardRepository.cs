using Microsoft.EntityFrameworkCore;
using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.Repositories;

namespace Zapper.Payments.Infrastructure.Persistence.Repositories;

public class CustomerCardRepository : ICustomerCardRepository
{
    private readonly AppDbContext _context;

    public CustomerCardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerCard?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default)
    {
        return await _context.CustomerCards.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
    }

    public async Task<IEnumerable<CustomerCard>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.CustomerCards.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(CustomerCard customerCard, CancellationToken ct = default)
    {
        await _context.CustomerCards.AddAsync(customerCard, ct);
    }
}
