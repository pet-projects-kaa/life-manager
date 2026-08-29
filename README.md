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
- погода через Open-Meteo без API-ключа + простая рекомендация по одежде;
- ежедневный развлекательный гороскоп (детерминированный, без внешнего API);
- Advice Engine: советы из погоды, дел, привычек, дома и «Не потеряй»;
- 👍/👎 на советы влияет на их дальнейшее ранжирование;
- юридический справочник: доставка, работа, банки, аренда + официальные источники;
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
3. В «Профиль» можно нажать **«Заполнить примером»**, чтобы получить данные как на макетах.
4. Указать свой город и координаты — прогноз начнёт подставляться в экран «Сегодня».

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

- `LIFE_MANAGER_PUBLIC_URL` — полный публичный base URL приложения, например `https://example.com/life`. Workflow проверит `${URL}/health` после деплоя.

## Caddy

Сервис входит в существующую external Docker network `shared-proxy` с alias `life-manager`.

Примеры находятся в `deploy/Caddyfile.example`.

Для пути:

```caddy
example.com {
    handle_path /life/* {
        reverse_proxy life-manager:8080
    }
}
```

Для поддомена:

```caddy
life.example.com {
    reverse_proxy life-manager:8080
}
```

Все ссылки и API-запросы в web UI относительные, поэтому приложение нормально работает и в корне домена, и под `/life/` при `handle_path`.

## Ресурсы VPS

Production compose ограничивает контейнер примерно:

- RAM: 512 MB;
- CPU: 1 core.

Отдельной PostgreSQL/Redis/LLM в текущем MVP нет, поэтому нагрузка на 4 CPU / 4 GB VPS небольшая.

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
