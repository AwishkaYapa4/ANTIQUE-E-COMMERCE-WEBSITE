using AntiqueStore.Application;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Infrastructure;
using AntiqueStore.Infrastructure.Auditing;
using AntiqueStore.Infrastructure.Persistence;
using AntiqueStore.Infrastructure.Persistence.Seeding;
using AntiqueStore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AntiqueStore.Tests.Support;

public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

public sealed class TestCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public UserRole? Role { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
    public string? IpAddress { get; set; } = "127.0.0.1";
}

/// <summary>
/// Real application services over a file-backed SQLite database (or, via <see cref="CreateSqlServerAsync"/>, a
/// throwaway SQL Server database built by the real migrations). Each scope gets its own connection,
/// so tests that run work in parallel exercise genuine concurrent transactions.
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly string? _path;
    private readonly bool _sqlServer;
    public ServiceProvider Services { get; }
    public MutableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private TestHost(string? path, bool sqlServer, ServiceProvider services)
    {
        _path = path;
        _sqlServer = sqlServer;
        Services = services;
    }

    public static Task<TestHost> CreateAsync(bool seedEssentials = true)
    {
        var path = Path.Combine(Path.GetTempPath(), $"antique-test-{Guid.NewGuid():N}.db");
        return CreateCoreAsync(path, false, o => o.UseSqlite($"Data Source={path};Default Timeout=60;Pooling=False"), async db =>
        {
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }, seedEssentials);
    }

    /// <summary>Same services over a new SQL Server database created by the SQL Server migrations (dropped on dispose).</summary>
    public static Task<TestHost> CreateSqlServerAsync(bool seedEssentials = true)
    {
        var connectionString = SqlServerTestDatabase.NewConnectionString();
        return CreateCoreAsync(null, true,
            o => o.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(DatabaseOptions.SqlServerMigrationsAssembly)),
            db => db.Database.MigrateAsync(), seedEssentials);
    }

    private static async Task<TestHost> CreateCoreAsync(string? path, bool sqlServer, Action<DbContextOptionsBuilder> configureDb,
        Func<AppDbContext, Task> createSchema, bool seedEssentials)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        TestHost? host = null;
        services.AddSingleton<TimeProvider>(_ => host!.Clock);
        services.AddDbContext<AppDbContext>(configureDb);
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IHtmlSanitizer, HtmlSanitizerService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddSingleton<IPrivateFileStorage, InMemoryPrivateStorage>();
        services.AddScoped<DatabaseSeeder>();
        host = new TestHost(path, sqlServer, services.BuildServiceProvider());

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await createSchema(db);
        if (seedEssentials) await new DatabaseSeeder(db, NullLogger<DatabaseSeeder>.Instance).SeedEssentialsAsync(default);
        return host;
    }

    /// <summary>Runs work in a fresh scope (own DbContext + connection) acting as the given user.</summary>
    public async Task<T> RunAsync<T>(int? userId, Func<IServiceProvider, Task<T>> work, UserRole role = UserRole.Customer)
    {
        await using var scope = Services.CreateAsyncScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.UserId = userId;
        current.Role = userId is null ? null : role;
        current.Email = userId is null ? null : $"user{userId}@example.test";
        return await work(scope.ServiceProvider);
    }

    public Task RunAsync(int? userId, Func<IServiceProvider, Task> work, UserRole role = UserRole.Customer) =>
        RunAsync<bool>(userId, async sp => { await work(sp); return true; }, role);

    public Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work) =>
        RunAsync(null, sp => work(sp.GetRequiredService<AppDbContext>()));

    public async Task<int> CreateUserAsync(string name, UserRole role = UserRole.Customer) =>
        await DbAsync(async db =>
        {
            var user = new User { FirebaseUid = $"uid-{name}-{Guid.NewGuid():N}", Email = $"{name}@example.test", EmailVerified = true, DisplayName = name, Role = role };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });

    public async Task<int> CreateProductAsync(string name, decimal price, int quantity = 1, Action<Product>? configure = null) =>
        await DbAsync(async db =>
        {
            var product = new Product
            {
                Name = name, Slug = AntiqueStore.Application.Common.Text.Slugify(name) + "-" + Guid.NewGuid().ToString("N")[..6],
                Sku = "SKU-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), Price = price, QuantityAvailable = quantity,
                MaxPerOrder = Math.Max(1, quantity), Status = ProductStatus.Published, PublishedAt = DateTime.UtcNow,
                Images = [new ProductImage { Url = "/placeholders/product-box.svg", SortOrder = 0 }]
            };
            configure?.Invoke(product);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return product.Id;
        });

    public async Task AddDeliveryZoneAsync(string district = "Colombo", decimal fee = 1500m, bool cod = true) =>
        await DbAsync(async db =>
        {
            db.ShippingZones.Add(new ShippingZone
            {
                Name = district + " zone", CashOnDeliveryAvailable = cod,
                Districts = [new ShippingZoneDistrict { District = district }],
                Rates = [new ShippingRate { Name = "Standard", Fee = fee }]
            });
            return await db.SaveChangesAsync();
        });

    public Task<Product> GetProductAsync(int id) => DbAsync(db => db.Products.AsNoTracking().FirstAsync(p => p.Id == id));

    public async ValueTask DisposeAsync()
    {
        if (_sqlServer)
        {
            // Only ever the AntiqueStore_Test_* database this host created.
            await using (var scope = Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            await Services.DisposeAsync();
            return;
        }

        await Services.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (_path is null) return;
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { /* best effort temp cleanup */ }
        }
    }
}

public sealed class InMemoryPrivateStorage : IPrivateFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new();

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        lock (_files) _files[key] = ms.ToArray();
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        lock (_files) return Task.FromResult<Stream?>(_files.TryGetValue(storageKey, out var b) ? new MemoryStream(b) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        lock (_files) _files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
