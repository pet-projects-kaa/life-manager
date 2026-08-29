# Life Manager

Полноценный MVP веб-приложения «менеджер жизни»: дела, привычки, покупки, дом, погода, персональные советы, «Не потеряй» и бытовой юридический помощник.

## Что уже работает

- landing на `/`;
- приложение на `/app/`;
- desktop + mobile responsive UI;
- PWA manifest + service worker;
- регистрация, вход, cookie-auth;
- отдельные данные на каждого пользователя;
- JSON persistence в `App_Data` (удобно для MVP и маленького VPS);
- дела: CRUD/выполнение/повторение;
- привычки: цели, дневной прогресс, статистика за 30 дней;
- покупки: список, цена, отметка «куплено», автоматическое создание истории покупки;
- крупные покупки/гарантии;
- дом: регулярные дела, расходники, техника, питомцы;
- «Не потеряй»: сроки, подписки, страховки, гарантии, потенциальные налоговые вычеты;
- погода по названию города: геокодирование + прогноз через Open-Meteo без API-ключа, температура, осадки и рекомендация по одежде;
- ежедневный развлекательный гороскоп с темой дня;
- Advice Engine: советы из погоды, дел, привычек, дома и «Не потеряй», плюс отдельная практическая подсказка, сгенерированная из темы гороскопа;
- «Сегодня в истории»: живые факты дня из русской Википедии со ссылками на источник;
- 👍/👎 на советы влияет на их дальнейшее ранжирование;
- юридический помощник по свободному описанию проблемы: автоматически определяет область, собирает контекстные шаги и прикладывает КонсультантПлюс + профильные официальные источники;
- `/health`;
- Docker;
- GitHub Actions: build/publish на runner, доставка готового релиза на VPS, symlink `current`, health check, rollback.

## Быстрый локальный запуск

### На macOS в один клик

Запусти `run.command` (Docker Desktop должен быть установлен). Для остановки — `stop.command`.

### Через Docker

```bash
docker compose -f compose.local.yml up --build
```

Открыть:

- сайт: `http://localhost:5086/`
- приложение: `http://localhost:5086/app/`
- health: `http://localhost:5086/health`

Данные будут лежать в `./App_Data`.

### Через .NET 8

```bash
dotnet restore LifeManager.sln
dotnet run --project src/LifeManager.Api/LifeManager.Api.csproj
```

## Первый вход

1. Открыть `/app/`.
2. Создать профиль.
3. В профиле указать город — координаты искать и вводить не нужно.
4. Указать знак зодиака и стиль одежды. Погода, одежда, гороскоп, факты дня и персональные советы появятся на экране «Сегодня».

Тестового заполнения профиля в production UI больше нет: пользователь начинает с чистых данных.

## Production deploy

Workflow: `.github/workflows/deploy.yml`.

Он следует той же идее, что `template-filler`:

1. GitHub runner валидирует фронт.
2. Runner выполняет `dotnet restore/build/publish`.
3. Готовый publish архивируется.
4. Архив и `compose.production.yml` передаются на VPS по SSH.
5. На VPS создаётся release-каталог `~/apps/life-manager/releases/<sha>`.
6. `current` переключается symlink-ом.
7. Контейнер использует только `mcr.microsoft.com/dotnet/aspnet:8.0` и **ничего не компилирует на VPS**.
8. Проверяется `http://127.0.0.1:5086/health`.
9. При неуспехе возвращается предыдущий `current`.
10. Хранятся последние 3 релиза.

### GitHub Secrets

Обязательные:

- `VPS_HOST`
- `VPS_USER`
- `VPS_SSH_KEY`

Опционально:

- `LIFE_MANAGER_PUBLIC_URL` — полный публичный base URL приложения. Для текущего production: `https://annushkaaaaa.store/life-manager`. Workflow проверит `${URL}/health` после деплоя.

## Caddy и публичный URL

Production рассчитан на path-based routing:

- `https://annushkaaaaa.store/life-manager/` — landing;
- `https://annushkaaaaa.store/life-manager/app/` — приложение;
- `https://annushkaaaaa.store/life-manager/health` — health-check.

Caddy работает на VPS как systemd-service и проксирует на `127.0.0.1:5086`. Для `/life-manager/*` используется `handle_path`, поэтому префикс снимается перед передачей в ASP.NET Core. Фронтенд использует относительные URL, поэтому статика, API и PWA продолжают работать под префиксом.

Основной `/etc/caddy/Caddyfile` объявляет `annushkaaaaa.store` один раз и импортирует `/etc/caddy/apps/*.caddy`. Каждый новый проект добавляет только свой route-snippet. Готовый пример находится в `deploy/Caddyfile.example`.

Persistent data в production вынесены из immutable `/app`:

- `./current:/app:ro` — код релиза;
- `./data:/data` — writable данные;
- `App__DataPath=/data`.

## Ресурсы VPS

Production compose ограничивает контейнер примерно:

- RAM: 512 MB;
- CPU: 1 core.

Отдельной PostgreSQL/Redis/LLM в текущем MVP нет, поэтому нагрузка на 4 CPU / 4 GB VPS небольшая. Юридический помощник сейчас source-grounded и динамически собирает ответ по тексту без внешнего LLM, поэтому не требует AI API-ключа и не выдумывает ссылки.

## Хранилище

Сейчас используется JSON storage:

- `App_Data/accounts.json` — аккаунты (пароли только PBKDF2 hash + salt);
- `App_Data/users/<user-id>.json` — пользовательские данные;
- `App_Data/receipts/...` — загруженные чеки.

Это специально выбранный MVP-вариант. Интерфейс `JsonStore` можно позднее заменить на PostgreSQL/EF Core без переписывания фронта и API-контрактов.

## Важно про юридический модуль и «Не потеряй»

Модуль даёт справочные подсказки и ссылки на официальные источники. Он не обещает выплату и не заменяет юриста. Потенциальные вычеты показываются как повод **проверить условия**, потому что фактическое право зависит от обстоятельств пользователя.

## Следующие логичные этапы

- PostgreSQL + миграции;
- email/Telegram push-уведомления;
- OCR чеков;
- импорт банковских операций;
- календарь;
- ML-ranking советов вместо текущего rule-based score;
- optional локальная маленькая модель для формулировки советов;
- экспорт/backup пользовательских данных.


### Погода и приватность

Для получения прогноза сервер отправляет сохранённые в профиле координаты в Open-Meteo. Если это не нужно, установи `Weather__Enabled=false`; приложение продолжит работать с fallback-карточкой.

## V3 additions

- Voice shopping list: the browser Web Speech API records Russian speech, shows the transcript for editing, then adds up to 50 items in one API call with lightweight category detection.
- Daily mood check-in: five quick states are persisted per day and influence advice ranking/text.
- Interests/hobbies in Profile: comma-separated interests are used for hobby prompts and daily reading recommendations.
- Reading recommendations: server-side Wikipedia search selects up to three articles from the user's interests; if none are configured, the day's horoscope theme supplies a neutral topic.
- Legal helper v3: scenario-level classification instead of broad keyword buckets, weighted phrases + roots, ambiguity detection, classification confidence, follow-up questions and scenario-specific ConsultantPlus/official sources.
- Image of the day: client-side Puter.js integration. Generation starts only by explicit button click. No Life Manager API key is stored; Puter may show a one-time consent/auth popup and can create a temporary user session.

### Browser notes

Voice recognition uses `SpeechRecognition` / `webkitSpeechRecognition`, so availability depends on browser/platform and microphone permission. When unsupported, the same dialog works as a manual multi-item list input.

Image generation requires loading `https://js.puter.com/v2/`. If the external service is unavailable or the user declines its consent flow, the rest of Life Manager continues to work normally.
