using System.Security.Claims;
using System.Text.Encodings.Web;
using AntiqueStore.Application.Users;
using AntiqueStore.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AntiqueStore.Api.Auth;

public static class AuthConstants
{
    public const string Scheme = "Firebase";
    public const string AdminPolicy = "Admin";
    public const string VerifiedEmailPolicy = "VerifiedEmail";
    public const string UserIdClaim = "app_user_id";
    public const string FirebaseUidClaim = "firebase_uid";
    public const string EmailVerifiedClaim = "email_verified";
}

/// <summary>
/// Validates "Authorization: Bearer &lt;Firebase ID token&gt;" with the Firebase Admin SDK, then loads (or creates)
/// the MySQL profile. The role claim comes exclusively from MySQL — never from the token or the client.
/// </summary>
public sealed class FirebaseAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    ITokenVerifier verifier, ProfileService profiles)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length is 0 or > 4096) return AuthenticateResult.Fail("Malformed token");

        var identity = await verifier.VerifyAsync(token, Context.RequestAborted);
        if (identity is null) return AuthenticateResult.Fail("Invalid or expired token");
        if (string.IsNullOrWhiteSpace(identity.Email)) return AuthenticateResult.Fail("An email address is required");

        var user = await profiles.SyncAsync(identity, Context.RequestAborted);
        if (user.IsDisabled) return AuthenticateResult.Fail("Account disabled");

        var claims = new List<Claim>
        {
            new(AuthConstants.UserIdClaim, user.Id.ToString()),
            new(AuthConstants.FirebaseUidClaim, user.FirebaseUid),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(AuthConstants.EmailVerifiedClaim, identity.EmailVerified ? "true" : "false")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthConstants.Scheme));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthConstants.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.io/401", title = "Please sign in to continue.", status = 401, code = "unauthenticated"
        });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.io/403", title = "You do not have permission to perform this action.", status = 403, code = "forbidden"
        });
    }
}
