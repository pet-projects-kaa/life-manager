using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public static class DemoData
{
    public static LifeData Create(string displayName)
    {
        var now = DateTimeOffset.Now;
        var today = now.Date;
        var water = new Habit { Title = "Вода", Icon = "💧", Target = 8, Unit = "стаканов" };
        var steps = new Habit { Title = "Шаги", Icon = "👟", Target = 6000, Unit = "шагов" };
        var vitamins = new Habit { Title = "Витамины", Icon = "💊", Target = 1, Unit = "раз" };
        var skincare = new Habit { Title = "Уход за кожей", Icon = "🧴", Target = 1, Unit = "раз" };
        var reading = new Habit { Title = "Чтение", Icon = "📖", Target = 20, Unit = "минут" };
        water.DailyValues[DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd")] = 5;
        steps.DailyValues[DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd")] = 4300;
        vitamins.DailyValues[DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd")] = 1;
        skincare.DailyValues[DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd")] = 1;
        reading.DailyValues[DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd")] = 10;

        for (var i = 1; i <= 29; i++)
        {
            var key = DateOnly.FromDateTime(now.AddDays(-i).DateTime).ToString("yyyy-MM-dd");
            water.DailyValues[key] = i % 4 == 0 ? 4 : 8;
            steps.DailyValues[key] = i % 3 == 0 ? 4200 : 6500;
            vitamins.DailyValues[key] = i % 7 == 0 ? 0 : 1;
        }

        return new LifeData
        {
            Profile = new Profile { DisplayName = displayName, City = "Москва", Latitude = 55.7558, Longitude = 37.6176, ZodiacSign = "Лев" },
            Tasks =
            [
                new() { Title = "Отправить документы", DueAt = now.Date.AddHours(10), Priority = "high" },
                new() { Title = "Купить продукты", Priority = "high" },
                new() { Title = "Полить цветы", DueAt = now.Date.AddHours(18) },
                new() { Title = "Разобрать полку", Priority = "normal" },
                new() { Title = "Сменить постельное бельё", DueAt = now.Date.AddDays(1), RepeatEveryDays = 7 },
                new() { Title = "Передать счётчики", DueAt = new DateTimeOffset(now.Year, now.Month, Math.Min(25, DateTime.DaysInMonth(now.Year, now.Month)), 12, 0, 0, now.Offset), RepeatEveryDays = 30 },
                new() { Title = "Поменять фильтр воды", DueAt = now.AddDays(2), RepeatEveryDays = 90 }
            ],
            Habits = [water, steps, vitamins, skincare, reading],
            ShoppingItems =
            [
                new() { Title = "Молоко", Category = "food", EstimatedPrice = 110 },
                new() { Title = "Яйца", Category = "food", EstimatedPrice = 160 },
                new() { Title = "Корм коту", Category = "pet", EstimatedPrice = 1700 },
                new() { Title = "Таблетки для посудомойки", Category = "home", EstimatedPrice = 850 },
                new() { Title = "Фильтры для воды", Category = "home", EstimatedPrice = 990 },
                new() { Title = "Насадки для щётки", Category = "other", EstimatedPrice = 750 }
            ],
            Purchases =
            [
                new() { Title = "Стоматология", Category = "medical", Amount = 47000, PurchasedAt = now.AddDays(-12) },
                new() { Title = "Робот-пылесос", Category = "appliance", Amount = 34990, PurchasedAt = now.AddMonths(-10), WarrantyUntil = now.AddDays(41) }
            ],
            HomeItems =
            [
                new() { Title = "Сменить полотенца", Category = "chore", Subtitle = "Ванная комната", RepeatEveryDays = 7, NextDueAt = now.Date },
                new() { Title = "Передать показания счётчиков", Category = "chore", Subtitle = "Вода и электричество", RepeatEveryDays = 30, NextDueAt = now.Date.AddDays(1) },
                new() { Title = "Очистить фильтр вытяжки", Category = "chore", Subtitle = "Кухня", RepeatEveryDays = 60, NextDueAt = now.Date },
                new() { Title = "Наполнитель для кота", Category = "consumable", DaysRemaining = 3 },
                new() { Title = "Таблетки для посудомойки", Category = "consumable", DaysRemaining = 9 },
                new() { Title = "Робот-пылесос", Category = "appliance", Subtitle = "Гарантия ещё 41 день", NextDueAt = now.AddDays(41) },
                new() { Title = "Поки", Category = "pet", Subtitle = "Таблетка от глистов", NextDueAt = now.AddDays(6), RepeatEveryDays = 90 }
            ],
            WatchItems =
            [
                new() { Title = "Подписка", Kind = "subscription", Amount = 899, DueAt = now.AddDays(3), Note = "Проверь, нужна ли она до продления" },
                new() { Title = "ОСАГО", Kind = "insurance", DueAt = now.AddDays(12), Note = "Пора проверить продление" },
                new() { Title = "Получить справку", Kind = "deadline", Amount = 8300, DueAt = now.AddDays(4), Note = "Потенциальная цена бездействия" }
            ]
        };
    }
}
