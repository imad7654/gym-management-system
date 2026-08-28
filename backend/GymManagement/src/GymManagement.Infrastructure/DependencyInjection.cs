using GymManagement.Application.Interfaces;
using GymManagement.Domain.Interfaces;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using GymManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // A declared version rather than ServerVersion.AutoDetect. AutoDetect opens a
        // connection while the service container is still being built, which means the app
        // cannot start at all if the database happens to be down at that moment, and
        // `dotnet ef migrations add` cannot run without a live server. Neither is a good
        // trade for detecting something we already know.
        var serverVersion = new MySqlServerVersion(
            Version.Parse(configuration["Database:ServerVersion"] ?? "8.0.0"));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connectionString, serverVersion,
                mySqlOptions =>
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }));

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IMembershipClock, MembershipClock>();

        return services;
    }
}
