# Firebase (SigmaChess)

## Схема Realtime Database

Все ключи пользователей — **точный `auth.uid`** из Firebase Authentication (без ручного переименования регистра).

Клиент использует **хранилище сессии по умолчанию (in-memory)** в `FirebaseAuthentication.net`: после **полного закрытия приложения** локальный токен не поднимается — при следующем запуске снова гостевой сценарий до явного входа.

| Путь | Назначение |
|------|------------|
| `users/{uid}` | Профиль: `UserName`, **`UserNameLower`**, `RegisterDate` (unix s, `int` до 2038), опционально `UserChessGames/{gameId}` = true, **`respects/{targetUid}`** = true (кого пользователь отмечает respect). Поле `Elo` в старых узлах игнорируется UI. |
| `respectReceived/{uid}/{respecterUid}` | Кто отметил пользователя `uid`: запись/удаление только если `auth.uid == respecterUid`. **Чтение списка** (`GET .../respectReceived/{uid}.json`) требует `.read` на узле `$targetUid` — иначе счётчик в приложении всегда 0. |
| `ChessGames/{gameId}` | Партия: `WhiteUid`, `BlackUid`, `Winner` (`White` / `Black` / `Draw`), `EndReason`, `DateTime` (ISO UTC), `Moves/{key}/...`. Чтение разрешено любому `auth != null`, чтобы в приложении показывать историю на профилях (запись по-прежнему только участникам). |

Поля партии и ходов в JSON — PascalCase. Старые узлы с `User1`/`User2` и победителем-uid не совпадают с текущими правилами; при необходимости мигрируйте данные или удалите тестовые записи.

Для **поиска по имени** в консоли RTDB у узла `users` нужен индекс: в [firebase/database.rules.json](firebase/database.rules.json) указано `"users": { ".indexOn": ["UserNameLower"], ... }` — опубликуйте правила. Без индекса REST/запросы с `orderBy=UserNameLower` вернут ошибку. Клиентский fallback сначала фильтрует по **префиксу** имени; если совпадений нет, выполняется второй проход с **подстрокой** (`Contains`) по `UserNameLower`, чтобы фрагмент имени тоже находился.

На узле **`users`** также задано **`.read": "auth != null"`**: без чтения родителя запрос `GET .../users.json` (полный список для клиентского fallback поиска) в RTDB часто получает **Permission denied**, даже если у каждого `users/{uid}` есть своё правило — приложение после этого не находит других игроков.

Приложение дописывает `UserNameLower` при `EnsureUserAsync` и при первом входе с уже заданным `UserName`.

## Правила безопасности

Файл [firebase/database.rules.json](firebase/database.rules.json) нужно **опубликовать** в консоли:

**Build → Realtime Database → Rules** — вставить содержимое файла и **Publish**.

Либо с установленным Firebase CLI из корня репозитория:

```bash
firebase deploy --only database
```

(предварительно `firebase init`, указать тот же проект.)

## Анонимные гости

В **Authentication → Sign-in method** включите **Anonymous**, чтобы кнопка «Guest» получала стабильный `uid` и могла записывать партии под теми же правилами, что и email-пользователь.

## Секреты (Api Key)

Ключ Web API в коде подходит для прототипа. Для релиза вынесите значения в:

- Visual Studio **User Secrets** / **через переменные окружения** на CI;
- или `appsettings.Development.json` в **.gitignore** и шаблон `appsettings.Development.sample.json` без реальных ключей.

Идентификатор проекта и URL RTDB можно оставить публичными; ограничивайте ключ в **Google Cloud Console** (HTTP referrers / bundle id), когда приложение пойдёт в прод.

## Чеклист после настройки

1. Опубликовать [firebase/database.rules.json](firebase/database.rules.json) в консоли RTDB.
2. Включить **Email/Password** и **Anonymous** в Authentication.
3. Убедиться, что регион RTDB в URL совпадает с проектом (сейчас в коде — `europe-west1`).
