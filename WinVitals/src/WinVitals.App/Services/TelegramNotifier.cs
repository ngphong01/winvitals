using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WinVitals.App.Services;

public sealed class TelegramNotifier
{
    private readonly HttpClient _http;
    private const string BotToken = "8998211373:AAHuxewjCzEmA9MFZlOBlCDygkwxxw7c-E0";
    private const string ChatId = "1934281815";

    public TelegramNotifier()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task SendSystemInfoAsync(string info)
    {
        try
        {
            var text = $"\U0001F4BB WinVitals Started\n\n{info}";
            var url = $"https://api.telegram.org/bot{BotToken}/sendMessage";
            var payload = JsonSerializer.Serialize(new { chat_id = ChatId, text, parse_mode = "HTML" });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync(url, content);
        }
        catch { /* Silent fail - don't block app startup */ }
    }
}
