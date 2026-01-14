using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AlienCyborgModernTempleOS;

public sealed class LmStudioChatClient
{
    private readonly HttpClient _http;

    public LmStudioChatClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("http://localhost:1234/v1/");
        _http.Timeout = TimeSpan.FromMinutes(20);
    }

    public async Task<string> ChatAsync(
        string model,
        (string role, string content)[] messages,
        CancellationToken ct,
        double temperature = 0.7)
    {
        // Keep payload minimal for LM Studio compatibility.
        var payload = new
        {
            model = model,
            messages = messages.Select(m => new { role = m.role, content = m.content }).ToArray(),
            temperature = temperature
        };

        using var resp = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken: ct);

        if (!resp.IsSuccessStatusCode)
        {
            // LM Studio returns a useful JSON error body on 400. Show it.
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"LM Studio chat/completions failed {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");
        }

        var json = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct);
        return json?.choices?.FirstOrDefault()?.message?.content ?? "";
    }

    public sealed class ChatCompletionResponse
    {
        public Choice[]? choices { get; set; }
        public sealed class Choice { public Msg? message { get; set; } }
        public sealed class Msg { public string? content { get; set; } }
    }
}
