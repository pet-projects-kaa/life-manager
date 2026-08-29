using System.Text.Json;
using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class TodayFactsService(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<IReadOnlyList<TodayFact>> GetAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var enabled = !bool.TryParse(configuration["TodayFacts:Enabled"], out var configured) || configured;
        if (!enabled) return [];
        try
        {
            var url = $"https://ru.wikipedia.org/api/rest_v1/feed/onthisday/events/{date:MM}/{date:dd}";
            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
                return [];

            var candidates = new List<TodayFact>();
            foreach (var item in events.EnumerateArray())
            {
                if (!item.TryGetProperty("text", out var textEl)) continue;
                var text = textEl.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length < 25) continue;
                var year = item.TryGetProperty("year", out var yearEl) && yearEl.ValueKind == JsonValueKind.Number ? yearEl.GetInt32() : (int?)null;
                string? sourceUrl = null;
                if (item.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array && pages.GetArrayLength() > 0)
                {
                    var page = pages[0];
                    if (page.TryGetProperty("content_urls", out var urls) && urls.TryGetProperty("desktop", out var desktop) && desktop.TryGetProperty("page", out var pageUrl))
                        sourceUrl = pageUrl.GetString();
                }
                candidates.Add(new TodayFact(year, text, sourceUrl ?? "https://ru.wikipedia.org/", "Википедия"));
            }

            if (candidates.Count <= 3) return candidates;
            // Берём факты из разных частей списка, чтобы не показывать три соседних события одного периода.
            var seed = date.DayNumber;
            var indexes = new HashSet<int>();
            for (var i = 0; indexes.Count < 3 && i < candidates.Count * 2; i++)
                indexes.Add((seed * (i + 3) + i * 17) % candidates.Count);
            return indexes.Select(i => candidates[i]).OrderBy(x => x.Year ?? int.MinValue).ToArray();
        }
        catch
        {
            return [];
        }
    }
}
