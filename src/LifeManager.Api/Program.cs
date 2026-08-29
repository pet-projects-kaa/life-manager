using System.Security.Claims;
using LifeManager.Api.Contracts;
using LifeManager.Api.Domain;
using LifeManager.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "life_manager_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddSingleton<JsonStore>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<BenefitService>();
builder.Services.AddSingleton<HoroscopeService>();
builder.Services.AddSingleton<AdviceService>();
builder.Services.AddSingleton<LegalAdvisorService>();
builder.Services.AddHttpClient<TodayFactsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LifeManager/1.0 (+https://annushkaaaaa.store/life-manager)");
});
builder.Services.AddHttpClient<WeatherService>((sp, client) =>
{
    var raw = sp.GetRequiredService<IConfiguration>()["Weather:TimeoutSeconds"];
    var seconds = int.TryParse(raw, out var parsed) ? parsed : 4;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 15));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LifeManager/1.0");
});

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.Use(async (context, next) =>
{
    // Keep the canonical app URL with a trailing slash without letting endpoint
    // routing treat /app and /app/ as the same route. /app/ must continue into
    // DefaultFilesMiddleware so wwwroot/app/index.html is served.
    if (string.Equals(context.Request.Path.Value, "/app", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("app/");
        return;
    }

    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "life-manager", utc = DateTimeOffset.UtcNow }));

var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", async (RegisterRequest request, JsonStore store, PasswordService passwords, HttpContext http) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var name = request.DisplayName.Trim();
    if (!email.Contains('@') || email.Length > 200) return Results.BadRequest(new { error = "Укажи корректный email" });
    if (request.Password.Length < 8) return Results.BadRequest(new { error = "Пароль должен быть не короче 8 символов" });
    if (string.IsNullOrWhiteSpace(name) || name.Length > 80) return Results.BadRequest(new { error = "Укажи имя" });
    if (await store.FindAccountAsync(email) is not null) return Results.Conflict(new { error = "Такой email уже зарегистрирован" });

    var (hash, salt) = passwords.Hash(request.Password);
    var account = new UserAccount { Email = email, DisplayName = name, PasswordHash = hash, PasswordSalt = salt };
    await store.AddAccountAsync(account);
    var data = await store.GetDataAsync(account.Id);
    data.Profile.DisplayName = name;
    await store.SaveDataAsync(account.Id, data);
    await SignInAsync(http, account);
    return Results.Ok(new { account.Id, account.Email, displayName = name });
});

auth.MapPost("/login", async (LoginRequest request, JsonStore store, PasswordService passwords, HttpContext http) =>
{
    var account = await store.FindAccountAsync(request.Email.Trim().ToLowerInvariant());
    if (account is null || !passwords.Verify(request.Password, account.PasswordHash, account.PasswordSalt))
        return Results.Unauthorized();
    await SignInAsync(http, account);
    return Results.Ok(new { account.Id, account.Email, account.DisplayName });
});

auth.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

auth.MapGet("/me", (ClaimsPrincipal user) =>
{
    if (!(user.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    return Results.Ok(new { id = user.FindFirstValue(ClaimTypes.NameIdentifier), email = user.FindFirstValue(ClaimTypes.Email), displayName = user.FindFirstValue(ClaimTypes.Name) });
});

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/dashboard", async (ClaimsPrincipal principal, JsonStore store, WeatherService weatherService, AdviceService adviceService, BenefitService benefits, HoroscopeService horoscopes, TodayFactsService factsService, CancellationToken ct) =>
{
    var id = UserId(principal);
    var data = await store.GetDataAsync(id);
    var now = DateTimeOffset.Now;
    var date = DateOnly.FromDateTime(now.DateTime);
    var weatherTask = weatherService.GetAsync(data.Profile, ct);
    var factsTask = factsService.GetAsync(date, ct);
    await Task.WhenAll(weatherTask, factsTask);
    var weather = await weatherTask;
    var facts = await factsTask;
    var advice = adviceService.Build(data, weather, now);
    var benefitCards = benefits.Build(data, now);
    var horoscope = horoscopes.Get(data.Profile.ZodiacSign, date);
    var key = date.ToString("yyyy-MM-dd");

    var habits = data.Habits.Where(x => !x.IsArchived).Select(x => new
    {
        x.Id, x.Title, x.Icon, x.Target, x.Unit,
        value = x.DailyValues.GetValueOrDefault(key),
        progress = x.Target <= 0 ? 0 : Math.Min(1, x.DailyValues.GetValueOrDefault(key) / (double)x.Target)
    });
    var tasks = data.Tasks.Where(x => !x.IsCompleted && (!x.DueAt.HasValue || x.DueAt.Value.Date <= now.Date)).OrderBy(x => x.DueAt).Take(8);
    return Results.Ok(new
    {
        profile = data.Profile,
        date = now,
        weather,
        horoscope,
        todayFacts = facts,
        tasks,
        habits,
        advice = advice.Take(4),
        notLose = benefitCards.Take(4),
        homeDue = data.HomeItems.Count(x => x.NextDueAt.HasValue && x.NextDueAt.Value <= now.AddDays(1))
    });
});

api.MapGet("/profile", async (ClaimsPrincipal principal, JsonStore store) => Results.Ok((await store.GetDataAsync(UserId(principal))).Profile));
api.MapPut("/profile", async (ProfileRequest request, ClaimsPrincipal principal, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.City)) return Results.BadRequest(new { error = "Укажи город" });
    var data = await store.GetDataAsync(UserId(principal));
    data.Profile.DisplayName = request.DisplayName.Trim();
    data.Profile.City = request.City.Trim();
    data.Profile.ZodiacSign = request.ZodiacSign.Trim();
    data.Profile.ClothingStyle = request.ClothingStyle.Trim();
    var userId = UserId(principal);
    await store.SaveDataAsync(userId, data);
    await store.UpdateAccountDisplayNameAsync(userId, data.Profile.DisplayName);
    return Results.Ok(data.Profile);
});

api.MapGet("/tasks", async (ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p));
    return Results.Ok(data.Tasks.OrderBy(x => x.IsCompleted).ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue));
});
api.MapPost("/tasks", async (TaskRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest(new { error = "Название обязательно" });
    var data = await store.GetDataAsync(UserId(p));
    var item = new LifeTask { Title = req.Title.Trim(), Notes = req.Notes?.Trim(), DueAt = req.DueAt, Priority = req.Priority, RepeatEveryDays = req.RepeatEveryDays };
    data.Tasks.Add(item); await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPut("/tasks/{id:guid}", async (Guid id, TaskRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); var item = data.Tasks.FirstOrDefault(x => x.Id == id); if (item is null) return Results.NotFound();
    item.Title = req.Title.Trim(); item.Notes = req.Notes?.Trim(); item.DueAt = req.DueAt; item.Priority = req.Priority; item.RepeatEveryDays = req.RepeatEveryDays;
    await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPost("/tasks/{id:guid}/toggle", async (Guid id, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); var item = data.Tasks.FirstOrDefault(x => x.Id == id); if (item is null) return Results.NotFound();
    if (item.RepeatEveryDays is > 0 && !item.IsCompleted)
    {
        item.DueAt = (item.DueAt ?? DateTimeOffset.Now).AddDays(item.RepeatEveryDays.Value); item.IsCompleted = false;
    }
    else item.IsCompleted = !item.IsCompleted;
    await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapDelete("/tasks/{id:guid}", async (Guid id, ClaimsPrincipal p, JsonStore store) => await DeleteAsync(p, store, d => d.Tasks.RemoveAll(x => x.Id == id)));

api.MapGet("/habits", async (ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p));
    var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
    return Results.Ok(data.Habits.Where(x => !x.IsArchived).Select(x => new { x.Id, x.Title, x.Icon, x.Target, x.Unit, value = x.DailyValues.GetValueOrDefault(today), stats = HabitStats(x) }));
});
api.MapPost("/habits", async (HabitRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Title) || req.Target <= 0) return Results.BadRequest(new { error = "Проверь название и цель" });
    var data = await store.GetDataAsync(UserId(p)); var item = new Habit { Title = req.Title.Trim(), Icon = string.IsNullOrWhiteSpace(req.Icon) ? "✓" : req.Icon, Target = req.Target, Unit = req.Unit.Trim() };
    data.Habits.Add(item); await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPost("/habits/{id:guid}/change", async (Guid id, int delta, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); var item = data.Habits.FirstOrDefault(x => x.Id == id); if (item is null) return Results.NotFound();
    var key = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"); item.DailyValues[key] = Math.Max(0, item.DailyValues.GetValueOrDefault(key) + delta);
    await store.SaveDataAsync(UserId(p), data); return Results.Ok(new { value = item.DailyValues[key] });
});
api.MapDelete("/habits/{id:guid}", async (Guid id, ClaimsPrincipal p, JsonStore store) => await DeleteAsync(p, store, d => d.Habits.RemoveAll(x => x.Id == id)));

api.MapGet("/shopping", async (ClaimsPrincipal p, JsonStore store) => Results.Ok((await store.GetDataAsync(UserId(p))).ShoppingItems.OrderBy(x => x.IsPurchased).ThenBy(x => x.CreatedAt)));
api.MapPost("/shopping", async (ShoppingRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest(new { error = "Название обязательно" });
    var data = await store.GetDataAsync(UserId(p)); var item = new ShoppingItem { Title = req.Title.Trim(), Category = req.Category, EstimatedPrice = req.EstimatedPrice };
    data.ShoppingItems.Add(item); await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPost("/shopping/{id:guid}/toggle", async (Guid id, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); var item = data.ShoppingItems.FirstOrDefault(x => x.Id == id); if (item is null) return Results.NotFound();
    item.IsPurchased = !item.IsPurchased;
    if (item.IsPurchased && item.EstimatedPrice is > 0 && !data.Purchases.Any(x => x.Title == item.Title && x.PurchasedAt > DateTimeOffset.Now.AddDays(-1)))
        data.Purchases.Add(new PurchaseRecord { Title = item.Title, Category = item.Category, Amount = item.EstimatedPrice.Value, PurchasedAt = DateTimeOffset.Now });
    await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapDelete("/shopping/{id:guid}", async (Guid id, ClaimsPrincipal p, JsonStore store) => await DeleteAsync(p, store, d => d.ShoppingItems.RemoveAll(x => x.Id == id)));

api.MapGet("/purchases", async (ClaimsPrincipal p, JsonStore store) => Results.Ok((await store.GetDataAsync(UserId(p))).Purchases.OrderByDescending(x => x.PurchasedAt)));
api.MapPost("/purchases", async (PurchaseRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Title) || req.Amount <= 0) return Results.BadRequest(new { error = "Проверь покупку и сумму" });
    var data = await store.GetDataAsync(UserId(p)); var item = new PurchaseRecord { Title = req.Title.Trim(), Category = req.Category, Amount = req.Amount, PurchasedAt = req.PurchasedAt ?? DateTimeOffset.Now, WarrantyUntil = req.WarrantyUntil };
    data.Purchases.Add(item); await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPost("/receipts", async (IFormFile file, ClaimsPrincipal p, JsonStore store) =>
{
    if (file.Length <= 0 || file.Length > 8 * 1024 * 1024) return Results.BadRequest(new { error = "Файл должен быть до 8 МБ" });
    var path = await store.SaveReceiptAsync(UserId(p), file); return Results.Ok(new { path });
}).DisableAntiforgery();

api.MapGet("/home", async (ClaimsPrincipal p, JsonStore store) => Results.Ok((await store.GetDataAsync(UserId(p))).HomeItems.OrderBy(x => x.NextDueAt ?? DateTimeOffset.MaxValue)));
api.MapPost("/home", async (HomeItemRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest(new { error = "Название обязательно" });
    var data = await store.GetDataAsync(UserId(p)); var item = new HomeItem { Title = req.Title.Trim(), Category = req.Category, Subtitle = req.Subtitle?.Trim(), RepeatEveryDays = req.RepeatEveryDays, NextDueAt = req.NextDueAt, DaysRemaining = req.DaysRemaining };
    data.HomeItems.Add(item); await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPost("/home/{id:guid}/complete", async (Guid id, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); var item = data.HomeItems.FirstOrDefault(x => x.Id == id); if (item is null) return Results.NotFound();
    item.LastDoneAt = DateTimeOffset.Now; if (item.RepeatEveryDays is > 0) item.NextDueAt = DateTimeOffset.Now.AddDays(item.RepeatEveryDays.Value); else item.NextDueAt = null;
    await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapDelete("/home/{id:guid}", async (Guid id, ClaimsPrincipal p, JsonStore store) => await DeleteAsync(p, store, d => d.HomeItems.RemoveAll(x => x.Id == id)));

api.MapGet("/benefits", async (ClaimsPrincipal p, JsonStore store, BenefitService service) => Results.Ok(service.Build(await store.GetDataAsync(UserId(p)), DateTimeOffset.Now)));
api.MapGet("/watch", async (ClaimsPrincipal p, JsonStore store) => Results.Ok((await store.GetDataAsync(UserId(p))).WatchItems.OrderBy(x => x.IsResolved).ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)));
api.MapPost("/watch", async (WatchItemRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest(new { error = "Название обязательно" });
    var data = await store.GetDataAsync(UserId(p)); var item = new WatchItem { Title = req.Title.Trim(), Kind = req.Kind, DueAt = req.DueAt, Amount = req.Amount, Note = req.Note?.Trim(), SourceUrl = req.SourceUrl };
    data.WatchItems.Add(item); await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapPost("/watch/{id:guid}/resolve", async (Guid id, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); var item = data.WatchItems.FirstOrDefault(x => x.Id == id); if (item is null) return Results.NotFound(); item.IsResolved = true;
    await store.SaveDataAsync(UserId(p), data); return Results.Ok(item);
});
api.MapDelete("/watch/{id:guid}", async (Guid id, ClaimsPrincipal p, JsonStore store) => await DeleteAsync(p, store, d => d.WatchItems.RemoveAll(x => x.Id == id)));

api.MapGet("/weather", async (ClaimsPrincipal p, JsonStore store, WeatherService weather, CancellationToken ct) => Results.Ok(await weather.GetAsync((await store.GetDataAsync(UserId(p))).Profile, ct)));
api.MapGet("/horoscope", async (ClaimsPrincipal p, JsonStore store, HoroscopeService horoscope) =>
{
    var profile = (await store.GetDataAsync(UserId(p))).Profile; return Results.Ok(horoscope.Get(profile.ZodiacSign, DateOnly.FromDateTime(DateTime.Now)));
});
api.MapGet("/today-facts", async (TodayFactsService facts, CancellationToken ct) => Results.Ok(await facts.GetAsync(DateOnly.FromDateTime(DateTime.Now), ct)));
api.MapGet("/advice", async (ClaimsPrincipal p, JsonStore store, WeatherService weather, AdviceService advice, CancellationToken ct) =>
{
    var data = await store.GetDataAsync(UserId(p)); var w = await weather.GetAsync(data.Profile, ct); return Results.Ok(advice.Build(data, w, DateTimeOffset.Now));
});
api.MapPost("/advice/feedback", async (AdviceFeedbackRequest req, ClaimsPrincipal p, JsonStore store) =>
{
    var data = await store.GetDataAsync(UserId(p)); data.AdviceFeedback.Add(new AdviceFeedback { AdviceKey = req.AdviceKey, Kind = req.Kind, Useful = req.Useful }); await store.SaveDataAsync(UserId(p), data); return Results.NoContent();
});

api.MapPost("/legal/advice", (LegalAdviceRequest request, LegalAdvisorService legal) => Results.Ok(legal.Get(request.Category, request.Text)));

app.MapFallbackToFile("app/{*path:nonfile}", "app/index.html");
app.Run();

static Guid UserId(ClaimsPrincipal principal)
    => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();

static async Task SignInAsync(HttpContext http, UserAccount account)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
        new Claim(ClaimTypes.Email, account.Email),
        new Claim(ClaimTypes.Name, account.DisplayName)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
}

static object HabitStats(Habit habit)
{
    var now = DateOnly.FromDateTime(DateTime.Now);
    var values = Enumerable.Range(0, 30).Select(i => habit.DailyValues.GetValueOrDefault(now.AddDays(-i).ToString("yyyy-MM-dd"))).ToArray();
    var completed = values.Count(v => v >= habit.Target);
    var weekdays = Enumerable.Range(0, 30).Where(i => now.AddDays(-i).DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).Select(i => values[i]).DefaultIfEmpty(0).Average();
    var weekends = Enumerable.Range(0, 30).Where(i => now.AddDays(-i).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Select(i => values[i]).DefaultIfEmpty(0).Average();
    return new { completedDays = completed, totalDays = 30, betterOnWeekdays = weekdays >= weekends };
}

static async Task<IResult> DeleteAsync(ClaimsPrincipal p, JsonStore store, Func<LifeData, int> delete)
{
    var id = UserId(p); var data = await store.GetDataAsync(id); var count = delete(data); if (count == 0) return Results.NotFound(); await store.SaveDataAsync(id, data); return Results.NoContent();
}
