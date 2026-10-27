using Microsoft.Data.SqlClient;

namespace AntiqueStore.Tests.Support;

/// <summary>
/// Throwaway SQL Server databases for integration tests. Each test gets its own uniquely named
/// AntiqueStore_Test_* database, created by migrations and dropped afterwards; other databases are never touched.
/// Server: ANTIQUESTORE_TEST_SQLSERVER (a connection string without a database), else localhost with Windows Authentication.
/// </summary>
public static class SqlServerTestDatabase
{
    public const string NamePrefix = "AntiqueStore_Test_";

    private static readonly string Server = Environment.GetEnvironmentVariable("ANTIQUESTORE_TEST_SQLSERVER")
                                            ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly Lazy<bool> Available = new(() =>
    {
        try
        {
            using var connection = new SqlConnection(new SqlConnectionStringBuilder(Server) { InitialCatalog = "master", ConnectTimeout = 3 }.ConnectionString);
            connection.Open();
            return true;
        }
        catch (SqlException) { return false; }
        catch (InvalidOperationException) { return false; }
    });

    public static bool IsAvailable => Available.Value;

    public static string NewConnectionString() =>
        new SqlConnectionStringBuilder(Server) { InitialCatalog = $"{NamePrefix}{Guid.NewGuid():N}" }.ConnectionString;
}

/// <summary>A fact that runs only when a SQL Server instance is reachable; otherwise it is reported as skipped.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerTestDatabase.IsAvailable)
            Skip = "SQL Server is not reachable. Set ANTIQUESTORE_TEST_SQLSERVER to run SQL Server integration tests.";
    }
}
