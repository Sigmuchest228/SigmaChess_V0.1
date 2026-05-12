# Firebase (SigmaChess)

## Схема Realtime Database

Все ключи пользователей — **точный `auth.uid`** из Firebase Authentication (без ручного переименования регистра).

| Путь | Назначение |
|------|------------|
| `users/{uid}` | Профиль: `UserName`, **`UserNameLower`** (нижний регистр для поиска), **`PuzzlesSolved`** (решённые задачи; титул ранга выводится в приложении по порогам), `RegisterDate` (unix ms), опционально `UserChessGames/{gameId}` = true, **`follows/{targetUid}`** = true (подписка для быстрого открытия профиля). Поле `Elo` в старых узлах игнорируется UI. Устаревший **`friends/{uid}`** при первом входе может быть скопирован в `follows` приложением. |
| `ChessGames/{gameId}` | Партия: `WhiteUid`, `BlackUid`, `Winner` (`White` / `Black` / `Draw`), `EndReason`, `DateTime` (ISO UTC), `Moves/{key}/...`. Чтение разрешено любому `auth != null`, чтобы в приложении показывать историю на профилях (запись по-прежнему только участникам). |
| `ChessPuzzles/{puzzleId}` | Задача: `Fen`, `Solution` (`FromPos`, `ToPos`, опционально `Promotion` как `Queen`/`Rook`/…), опционально `Title`, `Difficulty`. Каталог только для чтения пользователями (`auth != null`); запись отключена в правилах — наполнение через консоль или Admin SDK. |
| `users/{uid}/puzzleProgress/{puzzleId}` | `true` или объект с меткой времени — задача решена; запись только владельцем узла (`users/{uid}` пишет сам пользователь). Используется вместе с `PuzzlesSolved` на родительском профиле: счётчик увеличивается только при первом решении. |

Пример для ручного импорта в консоли RTDB: [Resources/Raw/puzzles_seed.json](Resources/Raw/puzzles_seed.json) — вставьте дочерние узлы под **`ChessPuzzles`** (каждый ключ верхнего уровня JSON станет `puzzleId`).

Поля партии и ходов в JSON — PascalCase. Старые узлы с `User1`/`User2` и победителем-uid не совпадают с текущими правилами; при необходимости мигрируйте данные или удалите тестовые записи.

Для **поиска по имени** в консоли RTDB у узла `users` нужен индекс: в [firebase/database.rules.json](firebase/database.rules.json) указано `"users": { ".indexOn": ["UserNameLower"], ... }` — опубликуйте правила. Без индекса REST/запросы с `orderBy=UserNameLower` вернут ошибку.

На узле **`users`** также задано **`.read": "auth != null"`**: без чтения родителя запрос `GET .../users.json` (полный список для клиентского fallback поиска) в RTDB часто получает **Permission denied**, даже если у каждого `users/{uid}` есть своё правило — приложение после этого не находит других игроков.

Приложение дописывает `UserNameLower` и при необходимости **`PuzzlesSolved`** при `EnsureUserProfileAsync` и при первом входе с уже заданным `UserName`.

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
