using System.Security.Claims;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class AgentAuthenticationDefaults
{
    public const string Scheme = "SentinelAgent";
    public const string DeviceIdHeader = "X-SentinelLAN-Device-Id";
    public const string DeviceSecretHeader = "X-SentinelLAN-Device-Secret";
}

public sealed class AgentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SentinelDbContext db, SensitiveDataCipher cipher, AgentRequestAuthenticationSettings settings, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var deviceIdValue = Request.Headers[AgentAuthenticationDefaults.DeviceIdHeader].ToString();
        var secret = Request.Headers[AgentAuthenticationDefaults.DeviceSecretHeader].ToString();
        var signature = Request.Headers[AgentRequestSignature.SignatureHeader].ToString();
        if (string.IsNullOrWhiteSpace(deviceIdValue) && string.IsNullOrWhiteSpace(secret))
            return AuthenticateResult.NoResult();

        if (!Guid.TryParse(deviceIdValue, out var deviceId)) return AuthenticateResult.Fail("Agent identity is invalid.");
        if (!string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(signature)) return AuthenticateResult.Fail("Ambiguous Agent authentication.");

        var deviceIdentity = await (
            from credential in db.DeviceCredentials.AsNoTracking()
            join device in db.Devices.AsNoTracking() on credential.DeviceId equals device.Id
            where credential.DeviceId == deviceId &&
                  credential.RevokedAt == null &&
                  !device.IsRevoked &&
                  db.Organizations.Any(o => o.Id == device.OrganizationId && !o.IsSuspended) &&
                  credential.OrganizationId == device.OrganizationId
            select new { credential.SecretHash, credential.OrganizationId, credential.RequestSigningKeyEnvelope, credential.RequestSigningConfirmedAt })
            .SingleOrDefaultAsync(Context.RequestAborted);
        if (deviceIdentity is null)
            return AuthenticateResult.Fail("Invalid or revoked device credential.");

        if (string.IsNullOrEmpty(signature))
        {
            if (deviceIdentity.RequestSigningConfirmedAt is not null || !(settings.DevelopmentLegacyEnabled || settings.LegacyUntil > clock.GetUtcNow()) ||
                string.IsNullOrWhiteSpace(secret) || !SecretHash.Matches(secret, deviceIdentity.SecretHash))
                return AuthenticateResult.Fail("Signed Agent authentication is required.");
        }
        else
        {
            if (deviceIdentity.RequestSigningKeyEnvelope is null || !signature.StartsWith("S1.", StringComparison.Ordinal) || signature.Length != 47 ||
                !long.TryParse(Request.Headers[AgentRequestSignature.TimestampHeader], NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) ||
                timestamp < clock.GetUtcNow().ToUnixTimeSeconds() - 60 || timestamp > clock.GetUtcNow().ToUnixTimeSeconds() + 60 ||
                !Guid.TryParseExact(Request.Headers[AgentRequestSignature.NonceHeader], "N", out var nonce) || nonce == Guid.Empty ||
                Request.ContentLength > AgentRequestSignature.MaximumBodyBytes)
                return AuthenticateResult.Fail("Agent request signature metadata is invalid or expired.");
            byte[] key;
            byte[] actual;
            try
            {
                actual = Convert.FromBase64String(signature[3..]);
                key = AgentRequestKey.Read(cipher, deviceIdentity.OrganizationId, deviceId, deviceIdentity.RequestSigningKeyEnvelope);
            }
            catch (Exception error) when (error is CryptographicException or FormatException) { return AuthenticateResult.Fail("Agent signing credential is unavailable."); }
            try
            {
                if (key.Length != 32) return AuthenticateResult.Fail("Agent signing credential has an invalid size.");
                Request.EnableBuffering(32 * 1024, AgentRequestSignature.MaximumBodyBytes);
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[16 * 1024];
                var total = 0;
                int count;
                while ((count = await Request.Body.ReadAsync(buffer, Context.RequestAborted)) != 0)
                {
                    total += count;
                    if (total > AgentRequestSignature.MaximumBodyBytes) return AuthenticateResult.Fail("Agent request exceeds its size limit.");
                    digest.AppendData(buffer, 0, count);
                }
                var target = Context.Features.Get<IHttpRequestFeature>()?.RawTarget is { Length: > 0 } rawTarget ? rawTarget : $"{Request.PathBase}{Request.Path}{Request.QueryString}";
                var expected = AgentRequestSignature.Sign(key, deviceId, Request.Method, target, timestamp, nonce, digest.GetHashAndReset());
                if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return AuthenticateResult.Fail("Agent request signature is invalid.");
            }
            catch (IOException) { return AuthenticateResult.Fail("Agent request exceeds its buffering limit."); }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                if (Request.Body.CanSeek) Request.Body.Position = 0;
            }
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(Context.RequestAborted);
            if (await db.Set<AgentRequestNonce>().AnyAsync(value => value.DeviceId == deviceId && value.Nonce == nonce, Context.RequestAborted))
                return AuthenticateResult.Fail("Agent request nonce has already been used.");
            var reservation = new AgentRequestNonce
            {
                OrganizationId = deviceIdentity.OrganizationId,
                DeviceId = deviceId,
                Nonce = nonce,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).AddSeconds(61)
            };
            db.Add(reservation);
            if (deviceIdentity.RequestSigningConfirmedAt is null)
            {
                var credential = await db.DeviceCredentials.SingleOrDefaultAsync(value => value.DeviceId == deviceId && value.OrganizationId == deviceIdentity.OrganizationId && value.RevokedAt == null, Context.RequestAborted);
                if (credential is null) return AuthenticateResult.Fail("Device credential is no longer active.");
                credential.RequestSigningConfirmedAt ??= clock.GetUtcNow();
            }
            try { await db.SaveChangesAsync(Context.RequestAborted); }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                return AuthenticateResult.Fail("Device credential changed; retry with a new signed request.");
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.Entry(reservation).State = EntityState.Detached;
                return AuthenticateResult.Fail("Agent request nonce has already been used.");
            }
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, deviceId.ToString()),
            new Claim(SentinelAuthenticationDefaults.OrganizationClaim, deviceIdentity.OrganizationId.ToString()),
            new Claim(ClaimTypes.Role, Roles.Agent)
        };
        var identity = new ClaimsIdentity(claims, AgentAuthenticationDefaults.Scheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AgentAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}
