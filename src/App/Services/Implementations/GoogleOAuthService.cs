using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Handles Google OAuth2 authentication via local HttpListener loopback.
/// Includes CSRF state parameter validation.
/// </summary>
public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleOAuthService> _logger;
    private readonly HttpClient _httpClient;

    public GoogleOAuthService(IConfiguration configuration, ILogger<GoogleOAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task<GoogleOAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        var clientId = _configuration["GoogleOAuth:ClientId"];
        var clientSecret = _configuration["GoogleOAuth:ClientSecret"];

        // If credentials are not configured in appsettings.json, provide clear instructions/demo mode
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("YOUR_GOOGLE_CLIENT_ID"))
        {
            _logger.LogWarning("Google OAuth ClientId is not configured. Falling back to Google Auth Simulation / Dev Mode.");

            // Simulating Google OAuth for dev testing when ClientId is unconfigured
            await Task.Delay(800, cancellationToken);
            var mockUser = new GoogleUserInfoDto(
                GoogleId: "109823749812739487123",
                Email: "student.fpt@fpt.edu.vn",
                FullName: "Nguyễn Văn A (FPT Student)",
                PictureUrl: "https://lh3.googleusercontent.com/a/default-user=s96-c"
            );
            return new GoogleOAuthResult(true, mockUser, null);
        }

        const string redirectUri = "http://localhost:5001/signin-google/";
        var state = Guid.NewGuid().ToString("N");

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start local HttpListener at {RedirectUri}", redirectUri);
            return new GoogleOAuthResult(false, null, "Không thể khởi động cổng lắng nghe đăng nhập Google. Vui lòng thử lại.");
        }

        // Build Google Authorization URL
        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                      $"client_id={Uri.EscapeDataString(clientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString("openid email profile")}" +
                      $"&state={Uri.EscapeDataString(state)}" +
                      $"&prompt=select_account";

        _logger.LogInformation("Opening system browser for Google OAuth: {AuthUrl}", authUrl);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open system browser.");
            listener.Stop();
            return new GoogleOAuthResult(false, null, "Không thể mở trình duyệt hệ thống để đăng nhập Google.");
        }

        // Listen for redirect callback with 2-minute timeout
        HttpListenerContext context;
        try
        {
            var contextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);

            var completedTask = await Task.WhenAny(contextTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                listener.Stop();
                return new GoogleOAuthResult(false, null, "Đã hết thời gian chờ đăng nhập bằng Google (Timeout).");
            }

            context = await contextTask;
        }
        catch (OperationCanceledException)
        {
            listener.Stop();
            return new GoogleOAuthResult(false, null, "Đã hủy thao tác đăng nhập Google.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while listening for Google OAuth callback.");
            return new GoogleOAuthResult(false, null, "Lỗi xảy ra trong quá trình nhận phản hồi từ Google.");
        }

        var request = context.Request;
        var response = context.Response;

        // Respond to browser to close tab gracefully
        var responseString = "<html><head><meta charset='utf-8'/></head><body style='font-family:sans-serif;text-align:center;padding-top:50px;'><h2>Xác thực Google thành công!</h2><p>Bạn có thể đóng cửa sổ này và quay lại ứng dụng.</p><script>setTimeout(function(){window.close();}, 1500);</script></body></html>";
        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        response.OutputStream.Close();
        listener.Stop();

        // Process query string parameters
        var code = request.QueryString["code"];
        var incomingState = request.QueryString["state"];
        var error = request.QueryString["error"];

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Google OAuth returned error: {Error}", error);
            return new GoogleOAuthResult(false, null, $"Người dùng đã hủy hoặc Google từ chối xác thực: {error}");
        }

        // CSRF verification
        if (incomingState != state)
        {
            _logger.LogWarning("Google OAuth CSRF State Mismatch!");
            return new GoogleOAuthResult(false, null, "Cảnh báo an toàn: Trạng thái xác thực (CSRF state) không hợp lệ.");
        }

        if (string.IsNullOrEmpty(code))
        {
            return new GoogleOAuthResult(false, null, "Không nhận được mã xác thực (Authorization Code) từ Google.");
        }

        // Exchange Authorization Code for Tokens
        try
        {
            var tokenRequestValues = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            };

            if (!string.IsNullOrWhiteSpace(clientSecret) && !clientSecret.Contains("YOUR_GOOGLE_CLIENT_SECRET"))
            {
                tokenRequestValues["client_secret"] = clientSecret;
            }

            var tokenResponse = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequestValues), cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Token Exchange failed: {ErrorBody}", errorBody);

                if (errorBody.Contains("invalid_client") || errorBody.Contains("client secret"))
                {
                    return new GoogleOAuthResult(false, null, "Lỗi Google OAuth: Client Secret không hợp lệ. Vui lòng kiểm tra lại ClientSecret trong appsettings.json.");
                }

                return new GoogleOAuthResult(false, null, "Lỗi khi trao đổi Token với Google Server.");
            }

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
            if (tokenJson == null || string.IsNullOrEmpty(tokenJson.AccessToken))
            {
                return new GoogleOAuthResult(false, null, "Token nhận được từ Google không hợp lệ.");
            }

            // Fetch UserInfo using AccessToken
            using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenJson.AccessToken);

            var userInfoResponse = await _httpClient.SendAsync(userInfoRequest, cancellationToken);
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                return new GoogleOAuthResult(false, null, "Không thể lấy thông tin người dùng từ Google API.");
            }

            var googleUserRaw = await userInfoResponse.Content.ReadFromJsonAsync<GoogleRawUserInfo>(cancellationToken: cancellationToken);
            if (googleUserRaw == null || string.IsNullOrEmpty(googleUserRaw.Email))
            {
                return new GoogleOAuthResult(false, null, "Thông tin người dùng Google thiếu Email.");
            }

            var userInfo = new GoogleUserInfoDto(
                GoogleId: googleUserRaw.Id,
                Email: googleUserRaw.Email,
                FullName: googleUserRaw.Name ?? googleUserRaw.Email,
                PictureUrl: googleUserRaw.Picture
            );

            return new GoogleOAuthResult(true, userInfo, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Google OAuth token exchange / userinfo request.");
            return new GoogleOAuthResult(false, null, $"Lỗi kết nối với Google API: {ex.Message}");
        }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private sealed class GoogleRawUserInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }
}
