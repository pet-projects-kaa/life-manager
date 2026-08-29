using System.Globalization;
using System.Text.Json;
using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class WeatherService(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<WeatherSnapshot> GetAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        var enabled = !bool.TryParse(configuration["Weather:Enabled"], out var weatherEnabled) || weatherEnabled;
        if (!enabled) return Unavailable(profile.City);

        try
        {
            var lat = profile.Latitude.ToString(CultureInfo.InvariantCulture);
            var lon = profile.Longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&hourly=precipitation_probability&forecast_days=1&timezone=auto";
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
            if (root.TryGetProperty("hourly", out var hourly) && hourly.TryGetProperty("precipitation_probability", out var probs))
            {
                probability = probs.EnumerateArray().Take(12).Select(x => x.ValueKind == JsonValueKind.Number ? x.GetInt32() : 0).DefaultIfEmpty(0).Max();
            }
            if (precipitation > 0 && (probability ?? 0) < 50) probability = 70;
            var summary = Describe(code, temp, probability);
            var outfit = Outfit(temp, feels, probability ?? 0, wind);
            return new(true, profile.City, Math.Round(temp), Math.Round(feels), probability, Math.Round(wind), summary, outfit);
        }
        catch
        {
            return Unavailable(profile.City);
        }
    }

    private static WeatherSnapshot Unavailable(string city) => new(false, city, null, null, null, null, "Прогноз временно недоступен", "Проверь погоду перед выходом");

    private static string Describe(int code, double temp, int? rainProbability)
    {
        if (rainProbability >= 60 || code is >= 51 and <= 82) return $"{Math.Round(temp)}°, возможен дождь";
        if (code is 0 or 1) return $"{Math.Round(temp)}°, ясно";
        if (code is 2 or 3) return $"{Math.Round(temp)}°, облачно";
        if (code is >= 95) return $"{Math.Round(temp)}°, возможна гроза";
        return $"{Math.Round(temp)}°";
    }

    private static string Outfit(double temp, double feels, int rainProbability, double wind)
    {
        var parts = new List<string>();
        var effective = Math.Min(temp, feels);
        if (effective <= 0) parts.Add("тёплая куртка, шапка и перчатки");
        else if (effective <= 8) parts.Add("тёплая куртка или пальто");
        else if (effective <= 15) parts.Add("лёгкая куртка или тренч");
        else if (effective <= 22) parts.Add("лёгкий верхний слой на вечер");
        else parts.Add("лёгкая одежда");
        if (rainProbability >= 50) parts.Add("зонт");
        if (wind >= 30) parts.Add("что-то непродуваемое");
        return char.ToUpper(parts[0][0]) + parts[0][1..] + (parts.Count > 1 ? "; " + string.Join("; ", parts.Skip(1)) : "");
    }
}
