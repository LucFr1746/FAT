namespace Services.Abstractions;

/// <summary>
/// Service contract for sending notification and verification emails (e.g. Password Reset OTP).
/// Supports both Real SMTP sending and Dev Demo simulation mode.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a 6-digit Password Reset OTP code to the student's email.
    /// Returns true if sent via SMTP, or false if in Dev Demo mode.
    /// </summary>
    Task<bool> SendPasswordResetOtpAsync(string toEmail, string fullName, string otpCode, CancellationToken cancellationToken = default);
}
