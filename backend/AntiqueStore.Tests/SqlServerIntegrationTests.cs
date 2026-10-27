using AntiqueStore.Api.Cli;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Application.Reports;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Infrastructure;
using AntiqueStore.Infrastructure.Persistence;
using AntiqueStore.Infrastructure.Persistence.Seeding;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AntiqueStore.Tests;

/// <summary>
/// The SQL Server migrations and the provider-sensitive paths (concurrent stock updates, unique-index races,
/// bulk updates/deletes, reporting queries) against a real SQL Server. Skipped when no server is reachable.
/// </summary>
public sealed class SqlServerIntegrationTests
{
    [SqlServerFact]
    public async Task Migrations_build_the_full_schema_and_match_the_model()
    {
        await using var host = await TestHost.CreateSqlServerAsync();

        await host.DbAsync(async db =>
        {
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
            // The migration snapshot must describe the current model, or a migration is missing.
            Assert.False(db.Database.HasPendingModelChanges());
            return true;
        });
    }

    [SqlServerFact]
    public async Task Seeding_essentials_twice_does_not_duplicate_rows()
    {
        await using var host = await TestHost.CreateSqlServerAsync();
        var before = await host.DbAsync(async db => (await db.StoreSettings.CountAsync(), await db.HomepageSections.CountAsync(), await db.ContentPages.CountAsync()));

        await host.DbAsync(async db =>
        {
            await new DatabaseSeeder(db, NullLogger<DatabaseSeeder>.Instance).SeedEssentialsAsync(default);
            return true;
        });

        var after = await host.DbAsync(async db => (await db.StoreSettings.CountAsync(), await db.HomepageSections.CountAsync(), await db.ContentPages.CountAsync()));
        Assert.Equal(before, after);
        Assert.True(after.Item1 > 0);
        Assert.Equal(0, await host.DbAsync(db => db.Products.CountAsync()));
    }

    [SqlServerFact]
    public async Task Parallel_stock_holds_on_a_unique_item_succeed_exactly_once()
    {
        await using var host = await TestHost.CreateSqlServerAsync();
        var productId = await host.CreateProductAsync("Teak Chair", 50000m, quantity: 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            host.RunAsync(null, sp => sp.GetRequiredService<InventoryService>().TryHoldStockAsync(productId, 1, default))));

        Assert.Equal(1, results.Count(r => r));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(1, product.QuantityReserved);
    }

    [SqlServerFact]
    public async Task Two_customers_ordering_the_same_item_at_once_create_exactly_one_order()
    {
        await using var host = await TestHost.CreateSqlServerAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Brass Lantern", 32500m, quantity: 1);
        var alice = await host.CreateUserAsync("alice");
        var bob = await host.CreateUserAsync("bob");
        await host.AddToCartAsync(alice, productId);
        await host.AddToCartAsync(bob, productId);

        var outcomes = await Task.WhenAll(new[] { alice, bob }.Select(async u =>
        {
            try { await host.PlaceOrderAsync(u, Builders.Order()); return "ordered"; }
            catch (BusinessRuleException e) { return e.Code; }
        }));

        Assert.Single(outcomes, o => o == "ordered");
        Assert.Equal(1, await host.DbAsync(db => db.Orders.CountAsync()));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(1, product.QuantitySold);
        Assert.Equal(InventoryState.Sold, product.InventoryState);
    }

    [SqlServerFact]
    public async Task Concurrent_duplicate_submissions_produce_a_single_order_and_clear_the_cart()
    {
        await using var host = await TestHost.CreateSqlServerAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Painting", 95000m);
        var user = await host.CreateUserAsync("doubleclick");
        await host.AddToCartAsync(user, productId);
        var request = Builders.Order(key: "double-click-key-0001");

        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            try { await host.PlaceOrderAsync(user, request); }
            catch (BusinessRuleException) { /* a racer may find the cart already cleared */ }
        }));

        Assert.Equal(1, await host.DbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(0, await host.DbAsync(db => db.CartItems.CountAsync()));
        Assert.Equal(1, (await host.GetProductAsync(productId)).QuantitySold);
    }

    [SqlServerFact]
    public async Task Order_money_values_and_reports_round_trip_through_SQL_Server()
    {
        await using var host = await TestHost.CreateSqlServerAsync();
        await host.AddDeliveryZoneAsync("Colombo", 1500.50m);
        var productId = await host.CreateProductAsync("Planter's Chair", 185000.25m);
        var user = await host.CreateUserAsync("buyer");
        await host.AddToCartAsync(user, productId);

        var order = (await host.PlaceOrderAsync(user, Builders.Order())).Order;
        Assert.Equal(186500.75m, order.Total);

        var dashboard = await host.RunAsync(null, sp => sp.GetRequiredService<ReportingService>().GetDashboardAsync(default));
        Assert.NotNull(dashboard);
        var day = DateOnly.FromDateTime(host.Clock.GetUtcNow().UtcDateTime);
        var sales = await host.RunAsync(null, sp => sp.GetRequiredService<ReportingService>().GetSalesReportAsync(day.AddDays(-1), day.AddDays(1), default));
        Assert.NotNull(sales);
    }

    [SqlServerFact]
    public async Task Db_migrate_refuses_a_database_whose_tables_were_created_without_migrations()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(SqlServerTestDatabase.NewConnectionString(), sql => sql.MigrationsAssembly(DatabaseOptions.SqlServerMigrationsAssembly))
            .Options;
        await using var db = new AppDbContext(options, TimeProvider.System);
        await db.Database.EnsureCreatedAsync();
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DatabaseInitializer.MigrateAsync(db, new DatabaseOptions { Provider = "SqlServer" }, default));
            Assert.Contains("no EF Core migration history", error.Message);
            Assert.True(await db.Products.CountAsync() >= 0); // existing tables were left untouched
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
