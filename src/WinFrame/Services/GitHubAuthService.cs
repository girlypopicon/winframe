using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinFrame.Models;

namespace WinFrame.Services;

public class DeviceFlowResult
{
    public string DeviceCode { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUri { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public int Interval { get; set; }
}

public class GitHubAuthService
{
    private const string ClientId = "Ov23liXXXXXXXXXXXXXX";
    private const string Scope = "read:user copilot";
    private readonly HttpClient _httpClient;

    public string? AccessToken { get; private set; }
    public GitHubUser? CurrentUser { get; private set; }
    public bool IsAuthenticated => AccessToken != null && CurrentUser != null;

    public event EventHandler? AuthenticationChanged;

    public GitHubAuthService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WinFrame/1.0");
    }

    public async Task<DeviceFlowResult> StartDeviceFlowAsync(CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = Scope
        });

        var response = await _httpClient.PostAsync(
            "https://github.com/login/device/code", content, ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new DeviceFlowResult
        {
            DeviceCode = root.GetProperty("device_code").GetString() ?? string.Empty,
            UserCode = root.GetProperty("user_code").GetString() ?? string.Empty,
            VerificationUri = root.TryGetProperty("verification_uri", out var uri) ? uri.GetString() ?? "https://github.com/login/device" : "https://github.com/login/device",
            ExpiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 900,
            Interval = root.TryGetProperty("interval", out var interval) ? interval.GetInt32() : 5
        };
    }

    public async Task<bool> PollForTokenAsync(string deviceCode, int intervalSeconds, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            try
            {
                var response = await _httpClient.PostAsync(
                    "https://github.com/login/oauth/access_token", content, ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("access_token", out var tokenProp))
                {
                    var token = tokenProp.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        AccessToken = token;
                        CurrentUser = await GetCurrentUserAsync(token, ct);
                        AuthenticationChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }

                if (root.TryGetProperty("error", out var errorProp))
                {
                    var error = errorProp.GetString();
                    if (error == "authorization_pending") continue;
                    if (error == "slow_down")
                    {
                        intervalSeconds += 5;
                        continue;
                    }
                    if (error is "expired_token" or "access_denied")
                        return false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // network error, continue polling
            }
        }
        return false;
    }

    public async Task<GitHubUser> GetCurrentUserAsync(string token, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var user = new GitHubUser
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            Login = root.TryGetProperty("login", out var login) ? login.GetString() ?? string.Empty : string.Empty,
            Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            AvatarUrl = root.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() ?? string.Empty : string.Empty,
            Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? string.Empty : string.Empty,
        };

        try
        {
            user.HasCopilotAccess = await CheckCopilotAccessAsync(token, ct);
        }
        catch { }

        return user;
    }

    private async Task<bool> CheckCopilotAccessAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/copilot_internal/v2/token");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetCopilotTokenAsync(CancellationToken ct = default)
    {
        if (AccessToken == null) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/copilot_internal/v2/token");
            request.Headers.Add("Authorization", $"Bearer {AccessToken}");
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                return tokenProp.GetString();
        }
        catch { }
        return null;
    }

    public void Logout()
    {
        AccessToken = null;
        CurrentUser = null;
        AuthenticationChanged?.Invoke(this, EventArgs.Empty);
    }
}
