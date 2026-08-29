using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class WeatherService(HttpClient httpClient, IConfiguration configuration)
{
    private sealed record Location(double Latitude, double Longitude, string Name, string? Admin1, string? Country, string? Timezone);
    private static readonly ConcurrentDictionary<string, (Location Location, DateTimeOffset Expires)> GeoCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<WeatherSnapshot> GetAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        var enabled = !bool.TryParse(configuration["Weather:Enabled"], out var weatherEnabled) || weatherEnabled;
        if (!enabled) return Unavailable(profile.City);

        try
        {
            var location = await ResolveLocationAsync(profile, cancellationToken);
            if (location is null) return Unavailable(profile.City, "Город не найден");

            var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
            var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                      "&current=temperature_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m" +
                      "&hourly=precipitation_probability" +
                      "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset" +
                      "&forecast_days=1&timezone=auto";

            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var current = root.GetProperty("current");
            var temp = current.GetProperty("temperature_2m").GetDouble();
            var feels = current.GetProperty("apparent_temperature").GetDouble();
            var precipitation = current.GetProperty("precipitation").GetDouble();
            var wind = current.GetProperty("wind_speed_10m").GetDouble();
            var code = current.GetProperty("weather_code").GetInt32();

            int? probability = null;
            if (root.TryGetProperty("daily", out var daily) && daily.TryGetProperty("precipitation_probability_max", out var dailyProb))
                probability = dailyProb.EnumerateArray().FirstOrDefault().ValueKind == JsonValueKind.Number ? dailyProb[0].GetInt32() : null;
            if (probability is null && root.TryGetProperty("hourly", out var hourly) && hourly.TryGetProperty("precipitation_probability", out var probs))
                probability = probs.EnumerateArray().Take(24).Select(x => x.ValueKind == JsonValueKind.Number ? x.GetInt32() : 0).DefaultIfEmpty(0).Max();
            if (precipitation > 0 && (probability ?? 0) < 50) probability = 70;

            double? max = null, min = null;
            string? sunrise = null, sunset = null;
            if (root.TryGetProperty("daily", out daily))
            {
                if (daily.TryGetProperty("temperature_2m_max", out var maxEl) && maxEl.GetArrayLength() > 0) max = maxEl[0].GetDouble();
                if (daily.TryGetProperty("temperature_2m_min", out var minEl) && minEl.GetArrayLength() > 0) min = minEl[0].GetDouble();
                if (daily.TryGetProperty("sunrise", out var riseEl) && riseEl.GetArrayLength() > 0) sunrise = riseEl[0].GetString();
                if (daily.TryGetProperty("sunset", out var setEl) && setEl.GetArrayLength() > 0) sunset = setEl[0].GetString();
            }

            var displayCity = BuildLocationName(location);
            var summary = Describe(code, temp, probability);
            var outfit = Outfit(temp, feels, probability ?? 0, wind, profile.ClothingStyle);
            return new(true, displayCity, Math.Round(temp), Math.Round(feels), probability, Math.Round(wind), summary, outfit,
                max.HasValue ? Math.Round(max.Value) : null, min.HasValue ? Math.Round(min.Value) : null, sunrise, sunset,
                "Open-Meteo");
        }
        catch
        {
            return Unavailable(profile.City);
        }
    }

    private async Task<Location?> ResolveLocationAsync(Profile profile, CancellationToken ct)
    {
        var city = profile.City.Trim();
        if (!string.IsNullOrWhiteSpace(city))
        {
            if (GeoCache.TryGetValue(city, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
                return cached.Location;

            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=ru&format=json";
            using var geoResponse = await httpClient.GetAsync(geoUrl, ct);
            if (geoResponse.IsSuccessStatusCode)
            {
                await using var geoStream = await geoResponse.Content.ReadAsStreamAsync(ct);
                using var geoDoc = await JsonDocument.ParseAsync(geoStream, cancellationToken: ct);
                if (geoDoc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var item = results[0];
                    var location = new Location(
                        item.GetProperty("latitude").GetDouble(),
                        item.GetProperty("longitude").GetDouble(),
                        item.GetProperty("name").GetString() ?? city,
                        item.TryGetProperty("admin1", out var admin) ? admin.GetString() : null,
                        item.TryGetProperty("country", out var country) ? country.GetString() : null,
                        item.TryGetProperty("timezone", out var tz) ? tz.GetString() : null);
                    GeoCache[city] = (location, DateTimeOffset.UtcNow.AddHours(12));
                    return location;
                }
            }
        }

        if (profile.Latitude is >= -90 and <= 90 && profile.Longitude is >= -180 and <= 180)
            return new Location(profile.Latitude, profile.Longitude, string.IsNullOrWhiteSpace(city) ? "Текущий город" : city, null, null, null);
        return null;
    }

    private static string BuildLocationName(Location location)
    {
        if (!string.IsNullOrWhiteSpace(location.Admin1) && !string.Equals(location.Admin1, location.Name, StringComparison.OrdinalIgnoreCase))
            return $"{location.Name}, {location.Admin1}";
        return location.Name;
    }

    private static WeatherSnapshot Unavailable(string city, string? reason = null)
        => new(false, city, null, null, null, null, reason ?? "Прогноз временно недоступен", "Проверь погоду перед выходом", null, null, null, null, "Open-Meteo");

    private static string Describe(int code, double temp, int? rainProbability)
    {
        if (code is >= 95) return $"{Math.Round(temp)}°, возможна гроза";
        if (code is >= 71 and <= 77) return $"{Math.Round(temp)}°, снег";
        if (rainProbability >= 60 || code is >= 51 and <= 82) return $"{Math.Round(temp)}°, возможен дождь";
        if (code is 0 or 1) return $"{Math.Round(temp)}°, ясно";
        if (code is 2 or 3) return $"{Math.Round(temp)}°, облачно";
        if (code is 45 or 48) return $"{Math.Round(temp)}°, туман";
        return $"{Math.Round(temp)}°";
    }

    private static string Outfit(double temp, double feels, int rainProbability, double wind, string style)
    {
        var parts = new List<string>();
        var effective = Math.Min(temp, feels);
        var sporty = string.Equals(style, "sport", StringComparison.OrdinalIgnoreCase);
        var classic = string.Equals(style, "classic", StringComparison.OrdinalIgnoreCase);

        if (effective <= -10) parts.Add("зимняя куртка, тёплая обувь, шапка и перчатки");
        else if (effective <= 0) parts.Add(classic ? "тёплое пальто, шарф и перчатки" : "тёплая куртка, шапка и перчатки");
        else if (effective <= 8) parts.Add(classic ? "пальто или утеплённый тренч" : sporty ? "ветровка с тёплым слоем" : "тёплая куртка или пальто");
        else if (effective <= 15) parts.Add(classic ? "тренч или жакет с верхним слоем" : sporty ? "лёгкая ветровка" : "лёгкая куртка или тренч");
        else if (effective <= 22) parts.Add("лёгкий верхний слой на вечер");
        else parts.Add("лёгкая одежда");
        if (rainProbability >= 50) parts.Add("возьми зонт");
        if (wind >= 30) parts.Add("лучше непродуваемый верх");
        return char.ToUpper(parts[0][0]) + parts[0][1..] + (parts.Count > 1 ? "; " + string.Join("; ", parts.Skip(1)) : "");
    }
}
