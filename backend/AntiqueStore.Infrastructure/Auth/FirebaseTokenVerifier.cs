using AntiqueStore.Application.Users;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntiqueStore.Infrastructure.Auth;

public sealed class FirebaseOptions
{
    public const string Section = "Firebase";
    public string? ProjectId { get; set; }
    /// <summary>Path to a service-account JSON file (keep outside the repo).</summary>
    public string? CredentialsPath { get; set; }
    /// <summary>Service-account JSON supplied via a secret/environment variable.</summary>
    public string? CredentialsJson { get; set; }
    /// <summary>e.g. "127.0.0.1:9099" to use the Firebase Auth Emulator in local development.</summary>
    public string? AuthEmulatorHost { get; set; }
}

public interface ITokenVerifier
{
    /// <summary>Verifies signature, issuer, audience and expiry. Returns null for any invalid token.</summary>
    Task<VerifiedIdentity?> VerifyAsync(string idToken, CancellationToken ct);
}

/// <summary>Verifies Firebase ID tokens with the Firebase Admin SDK (server-side only).</summary>
public sealed class FirebaseTokenVerifier : ITokenVerifier
{
    private readonly Lazy<FirebaseAuth> _auth;
    private readonly ILogger<FirebaseTokenVerifier> _logger;

    public FirebaseTokenVerifier(IOptions<FirebaseOptions> options, ILogger<FirebaseTokenVerifier> logger)
    {
        _logger = logger;
        var o = options.Value;
        _auth = new Lazy<FirebaseAuth>(() => FirebaseAuth.GetAuth(CreateApp(o)));
    }

    private static FirebaseApp CreateApp(FirebaseOptions o)
    {
        const string name = "antique-store";
        var existing = FirebaseApp.GetInstance(name);
        if (existing is not null) return existing;

        if (string.IsNullOrWhiteSpace(o.ProjectId))
            throw new InvalidOperationException("Firebase:ProjectId is not configured.");

        GoogleCredential credential;
        if (!string.IsNullOrWhiteSpace(o.AuthEmulatorHost))
        {
            // The Admin SDK reads this variable and accepts unsigned emulator tokens. Never set it in production.
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", o.AuthEmulatorHost);
            credential = GoogleCredential.FromAccessToken("emulator-placeholder");
        }
        else if (!string.IsNullOrWhiteSpace(o.CredentialsJson))
        {
            credential = CredentialFactory.FromJson<ServiceAccountCredential>(o.CredentialsJson).ToGoogleCredential();
        }
        else if (!string.IsNullOrWhiteSpace(o.CredentialsPath))
        {
            credential = CredentialFactory.FromFile<ServiceAccountCredential>(o.CredentialsPath).ToGoogleCredential();
        }
        else
        {
            // Token verification only needs Google's public keys + project id, but the SDK requires a credential.
            credential = GoogleCredential.GetApplicationDefault();
        }

        return FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = o.ProjectId }, name);
    }

    public async Task<VerifiedIdentity?> VerifyAsync(string idToken, CancellationToken ct)
    {
        try
        {
            var token = await _auth.Value.VerifyIdTokenAsync(idToken, ct);
            token.Claims.TryGetValue("email", out var email);
            token.Claims.TryGetValue("email_verified", out var verified);
            token.Claims.TryGetValue("name", out var name);
            token.Claims.TryGetValue("picture", out var picture);
            return new VerifiedIdentity(token.Uid, email as string, verified is true, name as string, picture as string);
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogInformation("Rejected Firebase token: {Reason}", ex.AuthErrorCode);
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
