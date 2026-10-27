using AntiqueStore.Application.Common;
using AntiqueStore.Infrastructure.Auditing;
using AntiqueStore.Infrastructure.Auth;
using AntiqueStore.Infrastructure.BackgroundJobs;
using AntiqueStore.Infrastructure.Media;
using AntiqueStore.Infrastructure.Persistence;
using AntiqueStore.Infrastructure.Persistence.Seeding;
using AntiqueStore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AntiqueStore.Infrastructure;

public sealed class DatabaseOptions
{
    public const string Section = "Database";
    /// <summary>
    /// "SqlServer" (default; migrations in AntiqueStore.Migrations.SqlServer), "MySql" (migrations in this assembly)
    /// or "Sqlite" (local demos/tests only; schema created directly).
    /// </summary>
    public string Provider { get; set; } = "SqlServer";
    public const string SqlServerMigrationsAssembly = "AntiqueStore.Migrations.SqlServer";
    public bool ApplyMigrationsOnStartup { get; set; }
    public bool SeedEssentials { get; set; } = true;
    /// <summary>Seeds EXAMPLE products, categories and delivery rates. Never enable in production.</summary>
    public bool SeedExampleData { get; set; }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<DatabaseOptions>(config.GetSection(DatabaseOptions.Section));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.Configure<FirebaseOptions>(config.GetSection(FirebaseOptions.Section));
        services.Configure<MaintenanceOptions>(config.GetSection(MaintenanceOptions.Section));

        var dbOptions = config.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = config.GetConnectionString("Default");
        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "ConnectionStrings:Default is not configured. Set it with user-secrets or the ConnectionStrings__Default environment variable.");
            if (dbOptions.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else if (dbOptions.Provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
                options.UseMySQL(connectionString, mysql => mysql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            else if (dbOptions.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(DatabaseOptions.SqlServerMigrationsAssembly));
            else
                throw new InvalidOperationException($"Unknown Database:Provider '{dbOptions.Provider}'. Use SqlServer, MySql or Sqlite.");
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<IHtmlSanitizer, HtmlSanitizerService>();
        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        var storageProvider = config.GetSection(StorageOptions.Section)["Provider"] ?? "Local";
        if (storageProvider.Equals("Cloudinary", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IImageStorage, CloudinaryImageStorage>();
        else
            services.AddSingleton<IImageStorage, LocalImageStorage>();
        services.AddSingleton<IPrivateFileStorage, LocalPrivateFileStorage>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.TryAddSingleton<ITokenVerifier, FirebaseTokenVerifier>();
        services.AddHostedService<MaintenanceWorker>();
        return services;
    }
}
