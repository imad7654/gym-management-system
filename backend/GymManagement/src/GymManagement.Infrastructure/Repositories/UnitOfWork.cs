using GymManagement.Domain.Entities;
using GymManagement.Domain.Interfaces;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace GymManagement.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;

    private IRepository<User>? _users;
    private IRepository<Role>? _roles;
    private IRepository<Client>? _clients;
    private IRepository<Package>? _packages;
    private IRepository<Payment>? _payments;
    private IRepository<PaymentHistory>? _paymentHistories;
    private IRepository<GymInfo>? _gymInfos;
    private IRepository<ExchangeRate>? _exchangeRates;
    private IRepository<AuditLog>? _auditLogs;
    private IRepository<RefreshToken>? _refreshTokens;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<User> Users => _users ??= new Repository<User>(_context);
    public IRepository<Role> Roles => _roles ??= new Repository<Role>(_context);
    public IRepository<Client> Clients => _clients ??= new Repository<Client>(_context);
    public IRepository<Package> Packages => _packages ??= new Repository<Package>(_context);
    public IRepository<Payment> Payments => _payments ??= new Repository<Payment>(_context);
    public IRepository<PaymentHistory> PaymentHistories => _paymentHistories ??= new Repository<PaymentHistory>(_context);
    public IRepository<GymInfo> GymInfos => _gymInfos ??= new Repository<GymInfo>(_context);
    public IRepository<ExchangeRate> ExchangeRates => _exchangeRates ??= new Repository<ExchangeRate>(_context);
    public IRepository<AuditLog> AuditLogs => _auditLogs ??= new Repository<AuditLog>(_context);
    public IRepository<RefreshToken> RefreshTokens => _refreshTokens ??= new Repository<RefreshToken>(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
