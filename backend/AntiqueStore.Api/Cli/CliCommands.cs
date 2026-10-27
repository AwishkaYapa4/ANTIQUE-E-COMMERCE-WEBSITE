using AntiqueStore.Domain.Enums;
using AntiqueStore.Infrastructure;
using AntiqueStore.Infrastructure.Persistence;
using AntiqueStore.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace AntiqueStore.Api.Cli;

/// <summary>
/// Trusted operator commands, run on the server/host (never exposed over HTTP):
///   dotnet run -- db migrate            Apply EF Core migrations
///   dotnet run -- db seed [--examples]  Seed essential settings (+ example catalog)
///   dotnet run -- admin grant email     Promote an existing user (must have signed in once) to Admin
///   dotnet run -- admin revoke email    Demote an Admin to Customer
///   dotnet run -- admin list            List admins
/// </summary>
public static class CliCommands
{
    public static bool IsCliInvocation(string[] args) => args.Length >= 2 && args[0] is "db" or "admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = CancellationToken.None;

        switch (args[0], args[1])
        {
            case ("db", "migrate"):
                await DatabaseInitializer.MigrateAsync(db, scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value, ct);
                Console.WriteLine("Database is up to date.");
                return 0;

            case ("db", "seed"):
                var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
                await seeder.SeedEssentialsAsync(ct);
                if (args.Contains("--examples")) await seeder.SeedExampleCatalogAsync(ct);
                Console.WriteLine("Seeding complete.");
                return 0;

            case ("admin", "grant" or "revoke") when args.Length >= 3:
            {
                var email = args[2].Trim();
                var users = await db.Users.Where(u => u.Email == email).ToListAsync(ct);
                if (users.Count == 0)
                {
                    Console.Error.WriteLine($"No user with email {email}. They must sign in to the website once before being promoted.");
                    return 1;
                }
                if (users.Count > 1)
                {
                    Console.Error.WriteLine($"Multiple accounts use {email}; resolve duplicates first.");
                    return 1;
                }
                var user = users[0];
                if (args[1] == "grant" && !user.EmailVerified)
                {
                    Console.Error.WriteLine($"{email} has not verified their email address. Verify it before granting admin access.");
                    return 1;
                }
                user.Role = args[1] == "grant" ? UserRole.Admin : UserRole.Customer;
                db.AdminAuditLogs.Add(new Domain.Entities.AdminAuditLog
                {
                    Action = $"admin.{args[1]}", EntityType = "User", EntityId = user.Id.ToString(),
                    Summary = $"{email} role set to {user.Role} via CLI on {Environment.MachineName}", CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
                Console.WriteLine($"{email} is now {user.Role}.");
                return 0;
            }

            case ("admin", "list"):
                foreach (var a in await db.Users.Where(u => u.Role == UserRole.Admin).OrderBy(u => u.Email).ToListAsync(ct))
                    Console.WriteLine($"{a.Id}\t{a.Email}\t{a.DisplayName}");
                return 0;

            default:
                Console.Error.WriteLine("Unknown command. Use: db migrate | db seed [--examples] | admin grant <email> | admin revoke <email> | admin list");
                return 2;
        }
    }
}

public static class DatabaseInitializer
{
    public static async Task MigrateAsync(AppDbContext db, DatabaseOptions options, CancellationToken ct)
    {
        if (options.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.EnsureCreatedAsync(ct); // demo/test provider: no migrations
            return;
        }

        // A database whose tables were created without migrations (e.g. EnsureCreated) has no migration history, and
        // migrating it would fail half-way on "object already exists". Stop before touching it instead.
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync(ct) && await creator.HasTablesAsync(ct) &&
            !(await db.Database.GetAppliedMigrationsAsync(ct)).Any())
        {
            throw new InvalidOperationException(
                "The database already contains tables but has no EF Core migration history, so migrations were not applied. " +
                "Back up and move or remove those tables (or point ConnectionStrings:Default at a new database), then run 'db migrate' again.");
        }
        await db.Database.MigrateAsync(ct);
    }

    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (options.ApplyMigrationsOnStartup)
        {
            logger.LogInformation("Applying database migrations on startup");
            await MigrateAsync(db, options, ct);
        }
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        if (options.SeedEssentials) await seeder.SeedEssentialsAsync(ct);
        if (options.SeedExampleData) await seeder.SeedExampleCatalogAsync(ct);
    }
}
