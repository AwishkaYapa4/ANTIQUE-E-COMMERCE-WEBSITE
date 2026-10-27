using AntiqueStore.Infrastructure;
using AntiqueStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AntiqueStore.Migrations.SqlServer;

/// <summary>
/// Used only by `dotnet ef` to generate SQL Server migrations (run with --project and --startup-project set to this
/// project). Generating migrations does not open a connection. To apply them, use
/// `dotnet run --project AntiqueStore.Api -- db migrate` (reads real configuration).
/// </summary>
public sealed class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                         ?? "Server=localhost;Database=AntiqueStore;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(DatabaseOptions.SqlServerMigrationsAssembly))
            .Options;
        return new AppDbContext(options, TimeProvider.System);
    }
}
