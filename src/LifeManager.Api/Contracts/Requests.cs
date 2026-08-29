namespace LifeManager.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record ProfileRequest(string DisplayName, string City, double Latitude, double Longitude, string ZodiacSign, string ClothingStyle);
public sealed record TaskRequest(string Title, string? Notes, DateTimeOffset? DueAt, string Priority, int? RepeatEveryDays);
public sealed record HabitRequest(string Title, string Icon, int Target, string Unit);
public sealed record ShoppingRequest(string Title, string Category, decimal? EstimatedPrice);
public sealed record PurchaseRequest(string Title, string Category, decimal Amount, DateTimeOffset? PurchasedAt, DateTimeOffset? WarrantyUntil);
public sealed record HomeItemRequest(string Title, string Category, string? Subtitle, int? RepeatEveryDays, DateTimeOffset? NextDueAt, int? DaysRemaining);
public sealed record WatchItemRequest(string Title, string Kind, DateTimeOffset? DueAt, decimal? Amount, string? Note, string? SourceUrl);
public sealed record LegalAdviceRequest(string Category, string Text);
public sealed record AdviceFeedbackRequest(string AdviceKey, string Kind, bool Useful);
