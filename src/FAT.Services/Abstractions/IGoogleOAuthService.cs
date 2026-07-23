using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// Service contract for handling Google OAuth2 authentication flow.
/// </summary>
public interface IGoogleOAuthService
{
    /// <summary>
    /// Kicks off the local loopback OAuth2 handshake, opens the system browser,
    /// and retrieves user profile information upon user authorization.
    /// </summary>
    Task<GoogleOAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default);
}
