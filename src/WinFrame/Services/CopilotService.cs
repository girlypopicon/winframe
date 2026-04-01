using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinFrame.Models;

namespace WinFrame.Services;

public class CopilotService
{
    private readonly HttpClient _httpClient;
    private readonly GitHubAuthService _authService;

    public CopilotService(GitHubAuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WinFrame/1.0");
        _httpClient.DefaultRequestHeaders.Add("Editor-Version", "WinFrame/1.0");
        _httpClient.DefaultRequestHeaders.Add("Editor-Plugin-Version", "WinFrame/1.0");
        _httpClient.DefaultRequestHeaders.Add("Copilot-Integration-Id", "vscode-chat");
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        IReadOnlyList<Message> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_authService.IsAuthenticated)
        {
            foreach (var token in GetMockResponse())
                yield return token;
            yield break;
        }

        var copilotToken = await _authService.GetCopilotTokenAsync(ct);
        if (copilotToken == null)
        {
            foreach (var token in GetMockResponse("I don't have Copilot access for this account. Please ensure your GitHub account has an active Copilot subscription."))
                yield return token;
            yield break;
        }

        var requestMessages = new List<object>();
        requestMessages.Add(new { role = "system", content = "You are WinFrame AI, a helpful coding assistant powered by GitHub Copilot. Help the user with coding tasks, answer questions, and assist with project organization." });

        foreach (var msg in messages)
        {
            requestMessages.Add(new
            {
                role = msg.Role == MessageRole.User ? "user" : "assistant",
                content = msg.Content
            });
        }

        var requestBody = JsonSerializer.Serialize(new
        {
            model = "gpt-4o",
            messages = requestMessages,
            stream = true,
            temperature = 0.7,
            max_tokens = 4096
        });

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.githubcopilot.com/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {copilotToken}");
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Network error — fall back to mock response
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            foreach (var token in GetMockResponse())
                yield return token;
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }

            using (doc)
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("choices", out var choices)) continue;
                if (choices.GetArrayLength() == 0) continue;
                var delta = choices[0];
                if (!delta.TryGetProperty("delta", out var deltaEl)) continue;
                if (!deltaEl.TryGetProperty("content", out var contentEl)) continue;
                var content = contentEl.GetString();
                if (content != null)
                    yield return content;
            }
        }
    }

    private static IEnumerable<string> GetMockResponse(string? customMessage = null)
    {
        var response = customMessage ?? "Hello! I'm WinFrame AI, your coding assistant. I'm ready to help you with coding tasks, answer questions, and assist with your projects. Sign in with GitHub and ensure you have a Copilot subscription to get full AI assistance.\n\nWhat can I help you with today?";
        foreach (var word in response.Split(' '))
            yield return word + " ";
    }
}
