using Microsoft.EntityFrameworkCore;
using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.Repositories;

namespace Zapper.Payments.Infrastructure.Persistence.Repositories;

public class SubMerchantRepository : ISubMerchantRepository
{
    private readonly AppDbContext _context;

    public SubMerchantRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SubMerchant?> GetByIdAsync(string subMerchantId, CancellationToken ct = default)
    {
        return await _context.SubMerchants.FirstOrDefaultAsync(s => s.SubMerchantId == subMerchantId, ct);
    }

    public async Task<IEnumerable<SubMerchant>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.SubMerchants.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(SubMerchant subMerchant, CancellationToken ct = default)
    {
        await _context.SubMerchants.AddAsync(subMerchant, ct);
    }
}
