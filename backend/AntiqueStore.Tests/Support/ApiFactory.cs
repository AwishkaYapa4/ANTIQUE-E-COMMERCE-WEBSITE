using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AntiqueStore.Application.Users;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Infrastructure.Auth;
using AntiqueStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AntiqueStore.Tests.Support;

/// <summary>
/// Stand-in for Firebase: accepts tokens shaped "test:{uid}:{email}:{verified}". Everything after token
/// verification (profile sync, roles from MySQL, policies) is the real production code path.
/// </summary>
public sealed class FakeTokenVerifier : ITokenVerifier
{
    public Task<VerifiedIdentity?> VerifyAsync(string idToken, CancellationToken ct)
    {
        var parts = idToken.Split(':');
        if (parts.Length != 4 || parts[0] != "test") return Task.FromResult<VerifiedIdentity?>(null);
        return Task.FromResult<VerifiedIdentity?>(new VerifiedIdentity(parts[1], parts[2], parts[3] == "true", parts[1], null));
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"antique-api-{Guid.NewGuid():N}.db");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Same strict DI validation as Development, so unresolvable registrations fail the test run.
        builder.UseDefaultServiceProvider(o => { o.ValidateOnBuild = true; o.ValidateScopes = true; });
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Default Timeout=60;Pooling=False");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedEssentials", "true");
        builder.UseSetting("Database:SeedExampleData", "false");
        builder.UseSetting("Maintenance:Enabled", "false");
        builder.UseSetting("Storage:LocalRoot", Path.Combine(Path.GetTempPath(), "antique-api-uploads"));
        builder.UseSetting("Storage:PrivateRoot", Path.Combine(Path.GetTempPath(), "antique-api-private"));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITokenVerifier>();
            services.AddSingleton<ITokenVerifier, FakeTokenVerifier>();
        });
    }

    public HttpClient ClientFor(string uid, bool verified = true)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"test:{uid}:{uid}@example.test:{(verified ? "true" : "false")}");
        return client;
    }

    /// <summary>Creates (via a first authenticated request) and promotes an admin – mirrors the CLI "admin grant" step.</summary>
    public async Task<HttpClient> AdminClientAsync(string uid = "admin")
    {
        var client = ClientFor(uid);
        (await client.GetAsync("/api/me")).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.FirebaseUid == uid);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        });
        return client;
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        await using var scope = Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { /* temp cleanup is best effort */ }
    }
}

public static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new Xunit.Sdk.XunitException($"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, ApiFactory.Json)!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json);
}
