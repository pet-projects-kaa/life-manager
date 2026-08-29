using System.Text.Json.Serialization;

namespace LifeManager.Api.Domain;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LifeData
{
    public Profile Profile { get; set; } = new();
    public List<LifeTask> Tasks { get; set; } = [];
    public List<Habit> Habits { get; set; } = [];
    public List<ShoppingItem> ShoppingItems { get; set; } = [];
    public List<PurchaseRecord> Purchases { get; set; } = [];
    public List<HomeItem> HomeItems { get; set; } = [];
    public List<WatchItem> WatchItems { get; set; } = [];
    public List<AdviceFeedback> AdviceFeedback { get; set; } = [];
    public List<MoodEntry> MoodEntries { get; set; } = [];
}

public sealed class Profile
{
    public string DisplayName { get; set; } = "Друг";
    public string City { get; set; } = "Москва";
    // Kept for backward-compatible stored profiles. Weather now resolves by City first.
    public double Latitude { get; set; } = 55.7558;
    public double Longitude { get; set; } = 37.6176;
    public string ZodiacSign { get; set; } = "Лев";
    public string ClothingStyle { get; set; } = "casual";
    public string Interests { get; set; } = "";
}

public sealed class MoodEntry
{
    public string DateKey { get; set; } = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
    public string Mood { get; set; } = "neutral";
    public int Energy { get; set; } = 3;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LifeTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public bool IsCompleted { get; set; }
    public string Priority { get; set; } = "normal";
    public int? RepeatEveryDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Habit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "✓";
    public int Target { get; set; } = 1;
    public string Unit { get; set; } = "раз";
    public Dictionary<string, int> DailyValues { get; set; } = [];
    public bool IsArchived { get; set; }
}

public sealed class ShoppingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "other";
    public decimal? EstimatedPrice { get; set; }
    public bool IsPurchased { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PurchaseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "other";
    public decimal Amount { get; set; }
    public DateTimeOffset PurchasedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? WarrantyUntil { get; set; }
    public string? ReceiptPath { get; set; }
}

public sealed class HomeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "chore";
    public string? Subtitle { get; set; }
    public int? RepeatEveryDays { get; set; }
    public DateTimeOffset? LastDoneAt { get; set; }
    public DateTimeOffset? NextDueAt { get; set; }
    public int? DaysRemaining { get; set; }
}

public sealed class WatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "deadline";
    public DateTimeOffset? DueAt { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
    public string? SourceUrl { get; set; }
    public bool IsResolved { get; set; }
}

public sealed class AdviceFeedback
{
    public string AdviceKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Useful { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record AdviceCard(string Key, string Kind, string Title, string Text, string Label, int Priority, string? Action = null);
public sealed record BenefitCard(string Key, string Kind, string Title, string Text, decimal? Amount, DateTimeOffset? DueAt, string? SourceUrl, int Priority);
public sealed record WeatherSnapshot(bool Available, string City, double? Temperature, double? FeelsLike, int? PrecipitationProbability, double? WindSpeed, string Summary, string OutfitAdvice, double? MaxTemperature, double? MinTemperature, string? Sunrise, string? Sunset, string Source);
public sealed record HoroscopeCard(string Sign, string Text, string Disclaimer, string Theme, string ThemeTitle);
public sealed record LegalSource(string Title, string Url, string? Note = null);
public sealed record TodayFact(int? Year, string Text, string SourceUrl, string SourceTitle);
public sealed record ReadingSuggestion(string Title, string Snippet, string Url, string Topic, string Source);
public sealed record LegalAdvice(
    string Category,
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    IReadOnlyList<LegalSource> Sources,
    string Disclaimer,
    int Confidence = 0,
    IReadOnlyList<string>? MatchedSignals = null,
    IReadOnlyList<string>? FollowUpQuestions = null);
