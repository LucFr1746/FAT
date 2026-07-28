using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Abstractions;

namespace Services.Implementations;

/// <summary>
/// Email delivery service implementation using System.Net.Mail SmtpClient.
/// Operates in Dev Simulation mode when SMTP settings are unconfigured.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetOtpAsync(string toEmail, string fullName, string otpCode, CancellationToken cancellationToken = default)
    {
        var smtpHost = _configuration["Smtp:Host"];
        var smtpPortStr = _configuration["Smtp:Port"];
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"] ?? smtpUser ?? "noreply@fat.fpt.edu.vn";
        var fromName = _configuration["Smtp:FromName"] ?? "FAT Academic Tracker";

        // Check if SMTP is configured
        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || smtpUser.Contains("YOUR_"))
        {
            _logger.LogWarning("SMTP is not configured. EmailService running in Dev Simulation Mode for OTP: {OtpCode} to {Email}", otpCode, toEmail);
            return false; // Dev simulation mode
        }

        int port = 587;
        if (!string.IsNullOrWhiteSpace(smtpPortStr) && int.TryParse(smtpPortStr, out int p))
        {
            port = p;
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, fromName);
            message.To.Add(new MailAddress(toEmail, fullName));
            message.Subject = $"[FAT System] Mã xác nhận đặt lại mật khẩu: {otpCode}";
            message.IsBodyHtml = true;
            message.Body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #FAF6EE;'>
                    <div style='max-width: 500px; margin: 0 auto; background-color: #FFFFFF; padding: 30px; border-radius: 16px; border: 1.5px solid #E5DDCB;'>
                        <h2 style='color: #473C33; text-align: center;'>🎓 FAT Academic Tracker</h2>
                        <h3 style='color: #ABC270; text-align: center;'>Mã Xác Thực Quên Mật Khẩu</h3>
                        <p style='color: #473C33;'>Xin chào <b>{WebUtility.HtmlEncode(fullName)}</b>,</p>
                        <p style='color: #7C6E65;'>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản FAT System. Mã OTP xác thực của bạn là:</p>
                        <div style='text-align: center; margin: 24px 0;'>
                            <span style='font-size: 32px; font-weight: bold; color: #473C33; letter-spacing: 6px; background-color: #FEC868; padding: 12px 24px; border-radius: 12px; display: inline-block;'>{otpCode}</span>
                        </div>
                        <p style='color: #7C6E65; font-size: 13px;'>Mã xác nhận có hiệu lực trong vòng <b>5 phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
                        <hr style='border: none; border-top: 1px solid #E5DDCB; margin: 20px 0;' />
                        <p style='color: #A39688; font-size: 12px; text-align: center;'>FPT Academic & Conduct Tracker • System Security</p>
                    </div>
                </div>";

            using var smtpClient = new SmtpClient(smtpHost, port)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            await smtpClient.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Successfully sent OTP reset email via SMTP to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email via SMTP to {Email}. Falling back to Dev Simulation mode.", toEmail);
            return false;
        }
    }
}
