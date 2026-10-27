using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AntiqueStore.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` to generate MySQL migrations. Generating migrations does not open a
/// connection, so the placeholder connection string here is never used to connect anywhere.
/// To apply migrations, use `dotnet run --project AntiqueStore.Api -- db migrate` (reads real configuration).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                         ?? "server=localhost;port=3306;database=antique_store;user=design_time;password=not-used";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(connection, mysql => mysql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options, TimeProvider.System);
    }
}
