using System.Security.Cryptography;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GymManagement.Infrastructure.Data.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Demo packages and marketing copy are useful while developing and are the last thing
        // a real gym wants sitting in its database on opening day, so they are opt-in.
        var seedDemoData = configuration.GetValue("Seed:DemoData", false);

        try
        {
            await context.Database.MigrateAsync();

            await SeedRolesAsync(context, logger);
            await context.SaveChangesAsync(); // Save roles first

            await SeedAdminUserAsync(context, passwordHasher, configuration, environment, logger);
            await SeedGymInfoAsync(context, seedDemoData, logger);

            if (seedDemoData)
            {
                await SeedDemoPackagesAsync(context, logger);

                // Saved before the members are seeded: they buy these packages, and the
                // payment flow needs them to have ids already.
                await context.SaveChangesAsync();

                var clock = scope.ServiceProvider.GetRequiredService<IMembershipClock>();
                await DemoGymSeeder.SeedAsync(context, clock, logger);
            }

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

    /// <summary>
    /// Creates the first administrator from configuration. There is deliberately no default
    /// password: a known default on the one account that can read every member phone number
    /// and address is a door left open, and defaults are exactly what nobody remembers to
    /// change. When none is configured in development a random one is generated and printed
    /// once.
    /// </summary>
    private static async Task SeedAdminUserAsync(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email))
        {
            if (!environment.IsDevelopment())
            {
                // Startup validation already refuses this case; guard anyway so the seeder
                // can never invent an account on its own.
                logger.LogError("Seed:AdminEmail is not configured. No administrator was created.");
                return;
            }

            email = "admin@gym.local";
        }

        if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email)) return;

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.Admin);
        if (adminRole == null)
        {
            logger.LogWarning("Admin role not found. Cannot seed admin user.");
            return;
        }

        var generated = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
            {
                logger.LogError("Seed:AdminPassword is not configured. No administrator was created.");
                return;
            }

            password = GenerateStrongPassword();
            generated = true;
        }

        var adminUser = new User
        {
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true
        };

        await context.Users.AddAsync(adminUser);
        await context.SaveChangesAsync();

        await context.UserRoles.AddAsync(new UserRole
        {
            UserId = adminUser.Id,
            RoleId = adminRole.Id,
            AssignedAt = DateTime.UtcNow
        });

        if (generated)
        {
            // Printed once, never stored anywhere readable. If it is missed, delete the user
            // row and restart to get a new one.
            logger.LogWarning(
                "\n" +
                "=====================================================================\n" +
                " ADMIN ACCOUNT CREATED - this password is shown once and not saved\n" +
                "   email:    {Email}\n" +
                "   password: {Password}\n" +
                " Copy it now, then change it after signing in.\n" +
                "=====================================================================",
                email, password);
        }
        else
        {
            logger.LogInformation("Seeded admin user: {Email}", email);
        }
    }

    private static async Task SeedDemoPackagesAsync(ApplicationDbContext context, ILogger logger)
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
        logger.LogInformation("Seeded {Count} demo packages", packages.Count);
    }

    /// <summary>
    /// The homepage and the login screen both read this row, so one always has to exist.
    /// Outside demo mode it is created empty for the owner to fill in rather than pre-filled
    /// with another gym name and address.
    /// </summary>
    private static async Task SeedGymInfoAsync(ApplicationDbContext context, bool seedDemoData, ILogger logger)
    {
        if (await context.GymInfos.AnyAsync()) return;

        if (!seedDemoData)
        {
            await context.GymInfos.AddAsync(new GymInfo { GymName = "My Gym" });
            logger.LogInformation("Created empty gym information row");
            return;
        }

        // This is what the public homepage shows until the owner edits it under
        // Settings, so it is The Fit Bear Gym's own copy rather than generic filler.
        // OperatingHours is deliberately plain text: nothing parses it, and the Settings
        // screen edits it as free text.
        var gymInfo = new GymInfo
        {
            GymName = "🐻 The Fit Bear Gym",
            Description = "Where strength meets nature. Serious equipment, real coaching, and a room full of people who show up.",
            Address = "Add your street address under Settings",
            PhoneNumber = "+961 00 000 000",
            Email = "hello@thefitbeargym.com",
            InstagramUrl = "https://instagram.com/thefitbeargym",
            HeroTitle = "Where Strength Meets Nature",
            HeroSubtitle = "Train like a bear, dominate like a champion. Join our pack and unleash your primal strength!",
            AboutTitle = "📍 Find Us & Join The Pack",
            AboutContent = "The Fit Bear Gym - where bears train champions. Come in for a look around, meet the coaches, and we will find the membership that fits how you actually train.",
            OperatingHours = "Mon-Fri: 6:00 AM - 10:00 PM\nSaturday: 8:00 AM - 8:00 PM\nSunday: 9:00 AM - 6:00 PM",
            MetaTitle = "The Fit Bear Gym - Where Strength Meets Nature",
            MetaDescription = "Serious equipment, real coaching, and a community that shows up. Join The Fit Bear Gym and start training today."
        };

        await context.GymInfos.AddAsync(gymInfo);
        logger.LogInformation("Seeded demo gym information");
    }

    private static string GenerateStrongPassword()
    {
        // Excludes characters that are easy to misread when copied out of a console log.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789-_@#";
        var chars = new char[24];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(chars);
    }
}
