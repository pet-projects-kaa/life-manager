using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class BenefitService
{
    public IReadOnlyList<BenefitCard> Build(LifeData data, DateTimeOffset now)
    {
        var result = new List<BenefitCard>();

        foreach (var purchase in data.Purchases)
        {
            if (purchase.Category is "medical" or "education" or "fitness")
            {
                result.Add(new(
                    $"deduction:{purchase.Id}",
                    "benefit",
                    "Проверь налоговый вычет",
                    $"Расход «{purchase.Title}» на {purchase.Amount:N0} ₽ может относиться к социальным налоговым вычетам при выполнении условий.",
                    null,
                    null,
                    "https://www.nalog.gov.ru/rn77/taxation/taxes/ndfl/nalog_vichet/soc_nv/",
                    90));
            }

            if (purchase.WarrantyUntil is { } warranty && warranty > now)
            {
                var days = Math.Max(0, (int)Math.Ceiling((warranty - now).TotalDays));
                result.Add(new(
                    $"warranty:{purchase.Id}",
                    "warranty",
                    purchase.Title,
                    $"Гарантия действует ещё примерно {days} дн.",
                    null,
                    warranty,
                    null,
                    days <= 45 ? 85 : 45));
            }
            else if (purchase.Category == "appliance" && purchase.Amount >= 5_000 && purchase.PurchasedAt > now.AddMonths(-3))
            {
                result.Add(new(
                    $"warranty-missing:{purchase.Id}",
                    "warranty",
                    $"Добавь гарантию: {purchase.Title}",
                    "Крупная покупка уже сохранена, но срок гарантии не указан. Если внесёшь его, приложение предупредит заранее.",
                    null,
                    null,
                    null,
                    48));
            }
        }

        foreach (var item in data.WatchItems.Where(x => !x.IsResolved))
        {
            var days = item.DueAt is { } due ? (int)Math.Ceiling((due - now).TotalDays) : 999;
            var priority = days <= 3 ? 100 : days <= 14 ? 80 : 50;
            var text = item.Kind switch
            {
                "subscription" => item.Amount is > 0
                    ? $"Списание {item.Amount:N0} ₽ через {Math.Max(0, days)} дн. Проверь, нужна ли подписка."
                    : $"Продление через {Math.Max(0, days)} дн. Проверь, нужна ли подписка.",
                "insurance" => $"Срок подходит через {Math.Max(0, days)} дн. Проверь продление заранее.",
                "deadline" => item.Amount is > 0
                    ? $"До срока примерно {Math.Max(0, days)} дн. Цена бездействия по твоей отметке — до {item.Amount:N0} ₽."
                    : $"До срока примерно {Math.Max(0, days)} дн.",
                "warranty" => $"Срок гарантии подходит через {Math.Max(0, days)} дн.",
                _ => item.Note ?? "Проверь этот срок"
            };
            result.Add(new($"watch:{item.Id}", item.Kind, item.Title, text, item.Amount, item.DueAt, item.SourceUrl, priority));
        }

        return result.OrderByDescending(x => x.Priority).ToArray();
    }
}
