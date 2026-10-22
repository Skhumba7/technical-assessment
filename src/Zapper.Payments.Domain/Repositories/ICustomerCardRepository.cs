using Zapper.Payments.Domain.Entities;

namespace Zapper.Payments.Domain.Repositories;

public interface ICustomerCardRepository
{
    Task<CustomerCard?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default);
    Task<IEnumerable<CustomerCard>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(CustomerCard customerCard, CancellationToken ct = default);
}
