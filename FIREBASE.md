# Firebase (SigmaChess)

## Схема Realtime Database

Все ключи пользователей — **точный `auth.uid`** из Firebase Authentication (без ручного переименования регистра).

| Путь | Назначение |
|------|------------|
| `users/{uid}` | Профиль: `UserName`, `Elo`, `RegisterDate` (unix ms), опционально `UserChessGames/{gameId}` = true |
| `ChessGames/{gameId}` | Партия: `WhiteUid`, `BlackUid`, `Winner` (`White` / `Black` / `Draw`), `EndReason`, `DateTime` (ISO UTC), `Moves/{key}/...` |

Поля партии и ходов в JSON — PascalCase. Старые узлы с `User1`/`User2` и победителем-uid не совпадают с текущими правилами; при необходимости мигрируйте данные или удалите тестовые записи.

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
