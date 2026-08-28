using GymManagement.Domain.Entities;

namespace GymManagement.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Role> Roles { get; }
    IRepository<Client> Clients { get; }
    IRepository<Package> Packages { get; }
    IRepository<Payment> Payments { get; }
    IRepository<PaymentHistory> PaymentHistories { get; }
    IRepository<GymInfo> GymInfos { get; }
    IRepository<ExchangeRate> ExchangeRates { get; }
    IRepository<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
