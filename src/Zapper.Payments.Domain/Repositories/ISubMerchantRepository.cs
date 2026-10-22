using Zapper.Payments.Domain.Entities;

namespace Zapper.Payments.Domain.Repositories;

public interface ISubMerchantRepository
{
    Task<SubMerchant?> GetByIdAsync(string subMerchantId, CancellationToken ct = default);
    Task<IEnumerable<SubMerchant>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(SubMerchant subMerchant, CancellationToken ct = default);
}
