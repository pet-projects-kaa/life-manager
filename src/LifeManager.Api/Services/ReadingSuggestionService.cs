using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class ReadingSuggestionService(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly IReadOnlyDictionary<string, string[]> ThemeTopics = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["focus"] = ["внимание психология", "прокрастинация", "состояние потока"],
        ["money"] = ["поведенческая экономика", "финансовая грамотность", "эффект владения"],
        ["home"] = ["история интерьера", "архитектура жилых домов", "эргономика"],
        ["communication"] = ["теория коммуникации", "активное слушание", "язык тела"],
        ["energy"] = ["циркадный ритм", "отдых психология", "сон"],
        ["boundaries"] = ["личные границы психология", "ассертивность", "социальная психология"]
    };

    public async Task<IReadOnlyList<ReadingSuggestion>> GetAsync(Profile profile, HoroscopeCard horoscope, DateOnly date, CancellationToken cancellationToken = default)
    {
        var enabled = !bool.TryParse(configuration["Reading:Enabled"], out var configured) || configured;
        if (!enabled) return [];

        var topics = ParseInterests(profile.Interests).ToList();
        if (topics.Count == 0 && ThemeTopics.TryGetValue(horoscope.Theme, out var defaults))
            topics.AddRange(defaults);
        if (topics.Count == 0)
            topics.AddRange(["история науки", "психология", "культура"]);

        var topic = PickForDay(topics, date, profile.ZodiacSign);
        try
        {
            var query = Uri.EscapeDataString(topic);
            var url = $"https://ru.wikipedia.org/w/api.php?action=query&list=search&srsearch={query}&format=json&utf8=1&srlimit=5";
            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("query", out var queryEl) ||
                !queryEl.TryGetProperty("search", out var searchEl) || searchEl.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<ReadingSuggestion>();
            foreach (var item in searchEl.EnumerateArray())
            {
                if (!item.TryGetProperty("title", out var titleEl)) continue;
                var title = titleEl.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;
                var snippet = item.TryGetProperty("snippet", out var snippetEl) ? CleanSnippet(snippetEl.GetString()) : "";
                var articleUrl = $"https://ru.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";
                result.Add(new ReadingSuggestion(title, snippet, articleUrl, topic, "Википедия"));
                if (result.Count == 3) break;
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> ParseInterests(string? raw)
        => (raw ?? string.Empty)
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Regex.Replace(x, @"\s+", " ").Trim())
            .Where(x => x.Length is >= 2 and <= 80)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12);

    private static string PickForDay(IReadOnlyList<string> topics, DateOnly date, string sign)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{date:yyyy-MM-dd}|{sign}|{string.Join('|', topics)}"));
        return topics[bytes[0] % topics.Count];
    }

    private static string CleanSnippet(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "Открыть статью и посмотреть, зацепит ли тема.";
        var noTags = Regex.Replace(html, "<.*?>", " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        var compact = Regex.Replace(decoded, @"\s+", " ").Trim();
        return compact.Length > 220 ? compact[..217] + "…" : compact;
    }
}
