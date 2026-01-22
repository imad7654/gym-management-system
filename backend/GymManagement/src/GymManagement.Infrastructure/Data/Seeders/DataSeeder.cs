using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymManagement.Infrastructure.Data.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            await context.Database.MigrateAsync();

            await SeedRolesAsync(context, logger);
            await context.SaveChangesAsync(); // Save roles first

            await SeedAdminUserAsync(context, passwordHasher, logger);
            await SeedPackagesAsync(context, logger);
            await SeedGymInfoAsync(context, logger);

            await context.SaveChangesAsync();
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Roles.AnyAsync()) return;

        var roles = new List<Role>
        {
            new() { Name = Roles.Admin, Description = "Full system access" },
            new() { Name = Roles.Client, Description = "Member access" },
            new() { Name = Roles.Trainer, Description = "Trainer access - future" },
            new() { Name = Roles.Staff, Description = "Staff access - future" }
        };

        await context.Roles.AddRangeAsync(roles);
        logger.LogInformation("Seeded {Count} roles", roles.Count);
    }

    private static async Task SeedAdminUserAsync(ApplicationDbContext context, IPasswordHasher passwordHasher, ILogger logger)
    {
        if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == "admin@gym.com")) return;

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.Admin);
        if (adminRole == null)
        {
            logger.LogWarning("Admin role not found. Cannot seed admin user.");
            return;
        }

        var adminUser = new User
        {
            Email = "admin@gym.com",
            PasswordHash = passwordHasher.HashPassword("Admin@123"),
            FirstName = "System",
            LastName = "Administrator",
            PhoneNumber = "+1234567890",
            IsActive = true
        };

        await context.Users.AddAsync(adminUser);
        await context.SaveChangesAsync();

        var userRole = new UserRole
        {
            UserId = adminUser.Id,
            RoleId = adminRole.Id,
            AssignedAt = DateTime.UtcNow
        };

        await context.UserRoles.AddAsync(userRole);
        logger.LogInformation("Seeded admin user: admin@gym.com");
    }

    private static async Task SeedPackagesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Packages.IgnoreQueryFilters().AnyAsync()) return;

        var packages = new List<Package>
        {
            new()
            {
                Name = "Monthly Basic",
                Description = "Access to gym facilities during regular hours. Includes locker usage.",
                DurationDays = 30,
                Price = 49.99m,
                IsActive = true,
                DisplayOrder = 1
            },
            new()
            {
                Name = "Monthly Premium",
                Description = "Full access to gym facilities 24/7. Includes locker, towel service, and 2 personal training sessions.",
                DurationDays = 30,
                Price = 79.99m,
                IsActive = true,
                DisplayOrder = 2
            },
            new()
            {
                Name = "Quarterly Basic",
                Description = "3 months access to gym facilities during regular hours. Save 10% compared to monthly!",
                DurationDays = 90,
                Price = 134.99m,
                IsActive = true,
                DisplayOrder = 3
            },
            new()
            {
                Name = "Annual Premium",
                Description = "Full year of unlimited 24/7 access. Includes all premium benefits plus 12 personal training sessions.",
                DurationDays = 365,
                Price = 699.99m,
                IsActive = true,
                DisplayOrder = 4
            }
        };

        await context.Packages.AddRangeAsync(packages);
        logger.LogInformation("Seeded {Count} packages", packages.Count);
    }

    private static async Task SeedGymInfoAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.GymInfos.AnyAsync()) return;

        var gymInfo = new GymInfo
        {
            GymName = "PowerFit Gym",
            Description = "Your ultimate fitness destination. State-of-the-art equipment, expert trainers, and a motivating atmosphere to help you achieve your fitness goals.",
            Address = "123 Fitness Street, Health City, HC 12345",
            PhoneNumber = "+1 (555) 123-4567",
            Email = "info@powerfitgym.com",
            FacebookUrl = "https://facebook.com/powerfitgym",
            InstagramUrl = "https://instagram.com/powerfitgym",
            HeroTitle = "Transform Your Body, Transform Your Life",
            HeroSubtitle = "Join our community of fitness enthusiasts and start your journey to a healthier, stronger you today!",
            AboutTitle = "About PowerFit Gym",
            AboutContent = "Founded in 2020, PowerFit Gym has been dedicated to helping our community achieve their fitness goals. Our facility features over 10,000 sq ft of workout space, including cardio zones, free weights, strength training equipment, and dedicated areas for group classes. Our certified trainers are committed to providing personalized guidance and support to members of all fitness levels.",
            OperatingHours = @"{
                ""monday"": ""5:00 AM - 11:00 PM"",
                ""tuesday"": ""5:00 AM - 11:00 PM"",
                ""wednesday"": ""5:00 AM - 11:00 PM"",
                ""thursday"": ""5:00 AM - 11:00 PM"",
                ""friday"": ""5:00 AM - 10:00 PM"",
                ""saturday"": ""7:00 AM - 8:00 PM"",
                ""sunday"": ""8:00 AM - 6:00 PM""
            }",
            MetaTitle = "PowerFit Gym - Your Ultimate Fitness Destination",
            MetaDescription = "Join PowerFit Gym for state-of-the-art equipment, expert trainers, and a motivating atmosphere. Start your fitness journey today!"
        };

        await context.GymInfos.AddAsync(gymInfo);
        logger.LogInformation("Seeded gym information");
    }
}
