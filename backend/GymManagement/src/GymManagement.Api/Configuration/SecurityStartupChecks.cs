using System.Text;

namespace GymManagement.Api.Configuration;

/// <summary>
/// Refuses to start the application when the secrets it depends on are missing, weak or
/// still set to a value that was once committed to the repository.
///
/// This runs at startup rather than at first use on purpose: a signing key problem that
/// surfaces the first time somebody logs in is a problem discovered in production by a
/// member, and a placeholder key that silently works is worse than one that fails loudly.
/// Anyone holding the signing key can mint a token for any account, the owner's included.
/// </summary>
public static class SecurityStartupChecks
{
    /// <summary>
    /// HS256 derives its strength from key length; below 32 bytes the key is weaker than
    /// the hash it feeds, and Microsoft's validator rejects it outright.
    /// </summary>
    private const int MinimumJwtKeyBytes = 32;

    /// <summary>
    /// Values that have appeared in this repository's git history, or are otherwise public.
    /// Editing a committed secret does not remove it - the old value stays in history forever -
    /// so these must never be accepted again even if someone pastes one back in.
    /// </summary>
    private static readonly string[] BurnedSecrets =
    {
        "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
        "root123",
        "gympass123",
    };

    /// <summary>
    /// Throws when a secret is unsafe enough to refuse startup. Returns anything that is
    /// worth complaining about but not worth blocking a developer over - the caller logs these.
    /// </summary>
    public static IReadOnlyList<string> Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var problems = new List<string>();
        var warnings = new List<string>();

        ValidateJwtKey(configuration, problems);
        ValidateConnectionString(configuration, environment, problems, warnings);
        ValidateSeedAdmin(configuration, environment, problems);
        ValidateEmail(configuration, environment, problems, warnings);

        if (problems.Count == 0) return warnings;

        var message = new StringBuilder()
            .AppendLine()
            .AppendLine("The application cannot start because required secrets are missing or unsafe:")
            .AppendLine();

        foreach (var problem in problems)
        {
            message.AppendLine("  - " + problem);
        }

        message
            .AppendLine()
            .AppendLine("Set them outside the repository. For local development:")
            .AppendLine()
            .AppendLine("  cd backend/GymManagement/src/GymManagement.Api")
            .AppendLine("  dotnet user-secrets init")
            .AppendLine("  dotnet user-secrets set \"Jwt:SecretKey\" \"<a long random string>\"")
            .AppendLine("  dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<your connection string>\"")
            .AppendLine()
            .AppendLine("In production use environment variables instead, with __ for the nesting:")
            .AppendLine()
            .AppendLine("  Jwt__SecretKey, ConnectionStrings__DefaultConnection")
            .AppendLine()
            .AppendLine("Generate a key with:  openssl rand -base64 48")
            .AppendLine();

        throw new InvalidOperationException(message.ToString());
    }

    private static void ValidateJwtKey(IConfiguration configuration, List<string> problems)
    {
        var key = configuration["Jwt:SecretKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            problems.Add("Jwt:SecretKey is not set. Without it no login token can be signed.");
            return;
        }

        if (IsBurned(key))
        {
            problems.Add(
                "Jwt:SecretKey is the placeholder that was committed to this repository. " +
                "It is public - anyone who can read the repo can forge a login token for any " +
                "account. Replace it with a new random value.");
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(key);
        if (byteCount < MinimumJwtKeyBytes)
        {
            problems.Add(
                $"Jwt:SecretKey is only {byteCount} bytes. It must be at least " +
                $"{MinimumJwtKeyBytes} bytes for HS256 signing.");
        }
    }

    private static void ValidateConnectionString(
        IConfiguration configuration,
        IHostEnvironment environment,
        List<string> problems,
        List<string> warnings)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            problems.Add("ConnectionStrings:DefaultConnection is not set.");
            return;
        }

        if (!BurnedSecrets.Any(burned => connectionString.Contains(burned, StringComparison.Ordinal)))
        {
            return;
        }

        const string explanation =
            "The database password in ConnectionStrings:DefaultConnection is one that was " +
            "committed to this repository, so it is public. Create a MySQL user that owns only " +
            "gymdb, give it a new password, and use that instead of root.";

        // A throwaway container on the developer's own machine is not worth blocking over;
        // a real server holding real members' phone numbers and addresses is.
        if (environment.IsDevelopment())
        {
            warnings.Add(explanation + " (Allowed here because the environment is Development.)");
        }
        else
        {
            problems.Add(explanation);
        }
    }

    /// <summary>
    /// Outside development the admin account must be configured deliberately. In development
    /// a random password is generated and printed once, which is handled by the seeder.
    /// </summary>
    private static void ValidateSeedAdmin(IConfiguration configuration, IHostEnvironment environment, List<string> problems)
    {
        if (environment.IsDevelopment()) return;

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            problems.Add(
                "Seed:AdminEmail and Seed:AdminPassword must both be set outside development, " +
                "so that no account is created with a default password.");
            return;
        }

        if (IsBurned(password) || password is "Admin@123")
        {
            problems.Add("Seed:AdminPassword is a known default. Choose a new one.");
        }
    }

    /// <summary>
    /// Outside development a mail server must be configured, because without one the
    /// "forgot password" flow silently does nothing.
    ///
    /// This is worth refusing to boot over rather than warning about. The reset page would
    /// still say "a link is on its way" - it says that whether or not the address exists,
    /// deliberately - so nobody would discover the email was never sent until an owner was
    /// locked out and the one recovery route turned out not to work.
    /// </summary>
    private static void ValidateEmail(
        IConfiguration configuration, IHostEnvironment environment,
        List<string> problems, List<string> warnings)
    {
        var configured =
            !string.IsNullOrWhiteSpace(configuration["Email:Host"])
            && !string.IsNullOrWhiteSpace(configuration["Email:Username"])
            && !string.IsNullOrWhiteSpace(configuration["Email:Password"]);

        if (configured) return;

        const string explanation =
            "No mail server is configured, so password reset emails cannot be sent. Set "
            + "Email:Host, Email:Username, Email:Password and Email:FromAddress. For Gmail "
            + "the password must be a 16-character App Password, not the account's own "
            + "password, and it belongs in user-secrets rather than a tracked file.";

        if (environment.IsDevelopment())
        {
            warnings.Add(explanation
                + " (Allowed here because the environment is Development - reset links are "
                + "written to this console instead.)");
        }
        else
        {
            problems.Add(explanation);
        }
    }

    private static bool IsBurned(string value) =>
        BurnedSecrets.Any(burned => string.Equals(value, burned, StringComparison.Ordinal));

}
