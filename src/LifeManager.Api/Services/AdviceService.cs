using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class AdviceService(BenefitService benefitService, HoroscopeService horoscopeService)
{
    public IReadOnlyList<AdviceCard> Build(LifeData data, WeatherSnapshot weather, DateTimeOffset now)
    {
        var result = new List<AdviceCard>();
        if (weather.Available && weather.PrecipitationProbability >= 50)
            result.Add(new("weather-rain", "weather", "Погода вмешивается в планы", $"Сегодня вероятность осадков до {weather.PrecipitationProbability}%. {weather.OutfitAdvice}.", "Полезно сейчас", 95, "Погода"));

        var todayTasks = data.Tasks.Where(x => !x.IsCompleted && (!x.DueAt.HasValue || x.DueAt.Value.Date <= now.Date)).ToList();
        if (todayTasks.Count > 6)
            result.Add(new("tasks-overload", "plan", "План выглядит перегруженным", $"На сегодня у тебя {todayTasks.Count} незакрытых дел. Выбери 3–5 обязательных, остальные лучше перенести осознанно.", "На основе твоих данных", 88, "Дела"));
        else if (todayTasks.Count is > 0 and <= 4)
            result.Add(new("tasks-realistic", "plan", "Сегодня можно закрыть один хвост", $"На сегодня {todayTasks.Count} активных дел. Если останется ресурс, выбери одно небольшое отложенное дело, а не добавляй новый большой блок.", "На основе твоих данных", 55, "Дела"));

        foreach (var habit in data.Habits.Where(x => !x.IsArchived))
        {
            var key = DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd");
            var value = habit.DailyValues.GetValueOrDefault(key);
            if (habit.Target > 1 && value < habit.Target / 2 && now.Hour >= 16)
                result.Add(new($"habit:{habit.Id}", "habit", $"«{habit.Title}» пока ниже цели", $"Сейчас {value} из {habit.Target} {habit.Unit}. Если цель сегодня уже нереалистична, лучше снизить её осознанно, чем догонять поздно вечером.", "Полезно сейчас", 52, "Привычки"));
        }

        foreach (var home in data.HomeItems.Where(x => x.NextDueAt.HasValue && x.NextDueAt.Value <= now.AddDays(1)).Take(2))
            result.Add(new($"home:{home.Id}", "home", "Домовое дело уже пора сделать", $"{home.Title}{(string.IsNullOrWhiteSpace(home.Subtitle) ? "" : $" — {home.Subtitle}")}.", "Дом", 66, "Дом"));

        foreach (var consumable in data.HomeItems.Where(x => x.Category == "consumable" && x.DaysRemaining is <= 3).Take(2))
            result.Add(new($"consumable:{consumable.Id}", "home", $"Скоро закончится: {consumable.Title}", $"По твоей отметке осталось примерно на {consumable.DaysRemaining} дн. Можно добавить в покупки заранее.", "Дом", 70, "Покупки"));

        foreach (var benefit in benefitService.Build(data, now).Take(3))
            result.Add(new($"benefit:{benefit.Key}", "benefit", benefit.Title, benefit.Text, "Не потеряй", benefit.Priority, "Выгода"));

        // Развлекательная подсказка строится именно из темы гороскопа, а не существует отдельным случайным текстом.
        var horoscope = horoscopeService.Get(data.Profile.ZodiacSign, DateOnly.FromDateTime(now.DateTime));
        result.Add(BuildHoroscopeAdvice(horoscope, data, todayTasks, now));

        var keyFeedback = data.AdviceFeedback
            .GroupBy(x => x.AdviceKey)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Useful ? 1 : -1));
        var kindFeedback = data.AdviceFeedback
            .Where(x => !string.IsNullOrWhiteSpace(x.Kind))
            .GroupBy(x => x.Kind)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Useful ? 1 : -1));

        return result
            .Select(x =>
            {
                var keyScore = keyFeedback.GetValueOrDefault(x.Key);
                var kindScore = kindFeedback.GetValueOrDefault(x.Kind);
                return x with { Priority = x.Priority + keyScore * 5 + kindScore * 3 };
            })
            .OrderByDescending(x => x.Priority)
            .Take(8)
            .ToArray();
    }

    private static AdviceCard BuildHoroscopeAdvice(HoroscopeCard horoscope, LifeData data, IReadOnlyList<LifeTask> todayTasks, DateTimeOffset now)
    {
        var (title, text) = horoscope.Theme switch
        {
            "money" => ("Гороскоп предлагает финансовую ревизию", data.WatchItems.Any(x => !x.IsResolved)
                ? "Выбери один активный срок, подписку или платёж в «Не потеряй» и проверь его сегодня. Это развлекательная подсказка, но действие вполне практичное."
                : "Проверь одну автоматическую подписку или необязательную покупку перед оплатой. Это развлекательная подсказка, а не финансовый прогноз."),
            "home" => ("По гороскопу — немного порядка", data.HomeItems.Any()
                ? $"Выбери одно небольшое домашнее дело{(data.HomeItems.FirstOrDefault(x => x.NextDueAt.HasValue && x.NextDueAt.Value <= now.AddDays(2)) is { } h ? $" — например, «{h.Title}»" : "")} и на этом остановись."
                : "Выбери одну маленькую зону дома на 10–15 минут вместо большой уборки."),
            "communication" => ("Сегодня ставка на ясность", "Если есть сообщение или договорённость, которую откладываешь, сформулируй её коротко и без намёков. Это совет, вдохновлённый сегодняшней темой гороскопа."),
            "energy" => ("Не забивай день до отказа", todayTasks.Count > 0
                ? $"У тебя уже {todayTasks.Count} дел на сегодня. Оставь хотя бы один свободный промежуток и не добавляй ещё одно обязательство автоматически."
                : "Не заполняй свободное время новыми задачами только потому, что оно появилось."),
            "boundaries" => ("Проверь чужие ожидания", "Перед новым «да» проверь, это действительно твоя задача и есть ли на неё ресурс. Гороскоп здесь используется только как развлекательный повод для рефлексии."),
            _ => ("Гороскоп предлагает сфокусироваться", todayTasks.Count > 0
                ? $"Выбери одно из сегодняшних дел — например, «{todayTasks[0].Title}» — и сначала доведи его до конца."
                : "Выбери одну важную задачу дня и не дроби внимание на мелочи.")
        };
        return new($"horoscope:{horoscope.Theme}", "horoscope", title, text, $"Гороскоп · {horoscope.ThemeTitle}", 80, "Гороскоп");
    }
}
