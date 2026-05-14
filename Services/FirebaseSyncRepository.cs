// RTDB: репозиторий (профиль, подписки, поиск, партии); DTO — в SigmaChess.Models.
using System.Diagnostics;
using System.Net.Http;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigmaChess.Engine;
using SigmaChess.Models;

namespace SigmaChess.Services;

/// <summary>Клиент синхронизации с Firebase RTDB.</summary>
public class FirebaseSyncRepository
{
    /// <summary>Совпадает с базой в (REST-запросы префикс-поиска).</summary>
    private const string RtdbRestRoot =
        "https://sigmachess-75f04-default-rtdb.europe-west1.firebasedatabase.app";

    private readonly AppService _app;

    public FirebaseSyncRepository(AppService app)
    {
        _app = app;
    }

    /// <summary>Строка EndReason для RTDB из <see cref="GameResult"/>.</summary>
    public static string ToEndReason(GameResult result) =>
        result switch
        {
            GameResult.Checkmate => "checkmate",
            GameResult.Stalemate => "stalemate",
            GameResult.DrawFiftyMoveRule => "fifty_move",
            GameResult.DrawRepetition => "repetition",
            GameResult.DrawInsufficientMaterial => "insufficient_material",
            _ => "unknown"
        };

    /// <summary>
    /// В терминальной позиции <paramref name="sideToMove"/> — сторона, обязанная ходить; при мате это проигравший.
    /// Победитель — противоположный цвет. Любая ничья даёт <c>Draw</c>.
    /// </summary>
    public static string ResolveWinnerColor(GameResult result, PieceColor sideToMove)
    {
        return result switch
        {
            GameResult.Checkmate => sideToMove == PieceColor.White ? "Black" : "White",
            GameResult.Stalemate => "Draw",
            GameResult.DrawFiftyMoveRule => "Draw",
            GameResult.DrawRepetition => "Draw",
            GameResult.DrawInsufficientMaterial => "Draw",
            _ => "Draw"
        };
    }

    /// <summary>
    /// Создаёт минимальный профиль, если узла нет или не заполнен UserName.
    /// <paramref name="preferredUserName"/> — при регистрации; для анонимного гостя без имени —
    /// <c>guest_</c> + короткий суффикс от <c>uid</c>; для email без имени — <c>Player</c>.
    /// Если узел уже есть с <c>UserChessGames</c>, для проставления имени используется Patch, не полный Put.
    /// </summary>
    public async Task EnsureUserProfileAsync(string? preferredUserName = null, CancellationToken cancellationToken = default)
    {
        var uid = _app.CurrentUserId;
        if (uid is null)
        {
            return;
        }

        var db = _app.RealtimeDatabase;
        var displayName = ResolveDisplayName(uid, preferredUserName, _app.IsAnonymousUser);
        var lower = displayName.Trim().ToLowerInvariant();

        var json = await db.Child("users").Child(uid).OnceAsJsonAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            var unix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var body = new NewProfile
            {
                UserName = displayName,
                UserNameLower = lower,
                RegisterDate = unix
            };
            await db.Child("users").Child(uid).PutAsync(JsonConvert.SerializeObject(body)).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var dto = JsonConvert.DeserializeObject<UserProfile>(json);
        if (!string.IsNullOrWhiteSpace(dto?.UserName))
        {
            var syncLower = dto.UserName.Trim().ToLowerInvariant();
            var patchMap = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(dto.UserNameLower)
                || !string.Equals(dto.UserNameLower, syncLower, StringComparison.Ordinal))
            {
                patchMap["UserNameLower"] = syncLower;
            }

            if (patchMap.Count > 0)
            {
                await db.Child("users").Child(uid).PatchAsync(JsonConvert.SerializeObject(patchMap))
                    .WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var patchNameMap = new Dictionary<string, object>
        {
            ["UserName"] = displayName,
            ["UserNameLower"] = lower
        };
        await db.Child("users").Child(uid).PatchAsync(JsonConvert.SerializeObject(patchNameMap)).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Читает узел <c>users/{uid}</c> как DTO или <c>null</c>.</summary>
    public async Task<UserProfile?> GetUserProfileByUidAsync(string uid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return null;
        }

        var json = await _app.RealtimeDatabase.Child("users").Child(uid).OnceAsJsonAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            return null;
        }

        return JsonConvert.DeserializeObject<UserProfile>(json);
    }

    /// <summary>Читает профиль текущего пользователя.</summary>
    public Task<UserProfile?> GetUserProfileAsync(CancellationToken cancellationToken = default)
    {
        var uid = _app.CurrentUserId;
        return uid is null
            ? Task.FromResult<UserProfile?>(null)
            : GetUserProfileByUidAsync(uid, cancellationToken);
    }

    /// <summary>Частичное обновление полей профиля в RTDB (например <c>AvatarUrl</c>).</summary>
    public async Task PatchUserProfileFieldsAsync(IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken = default)
    {
        var uid = _app.CurrentUserId;
        if (uid is null || fields.Count == 0)
        {
            return;
        }

        var json = JsonConvert.SerializeObject(fields);
        await _app.RealtimeDatabase.Child("users").Child(uid).PatchAsync(json).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveDisplayName(string uid, string? preferredUserName, bool isAnonymous)
    {
        if (!string.IsNullOrWhiteSpace(preferredUserName))
        {
            var s = preferredUserName.Trim();
            if (s.Length > 24)
            {
                s = s[..24];
            }

            return string.IsNullOrEmpty(s) ? FallbackDisplayName(uid, isAnonymous) : s;
        }

        return FallbackDisplayName(uid, isAnonymous);
    }

    private static string FallbackDisplayName(string uid, bool isAnonymous)
    {
        if (isAnonymous)
        {
            return GuestDisplayNameFromUid(uid);
        }

        return "Player";
    }

    /// <summary>Стабильное короткое имя гостя: <c>guest_</c> + последние до 6 символов <paramref name="uid"/> (нижний регистр).</summary>
    public static string GuestDisplayNameFromUid(string uid)
    {
        if (string.IsNullOrEmpty(uid))
        {
            return "guest_";
        }

        var take = Math.Min(6, uid.Length);
        var suffix = uid[^take..].ToLowerInvariant();
        return $"guest_{suffix}";
    }

    /// <summary>Ключи <c>users/{uid}/UserChessGames</c> (новее сверху — по строковому id).</summary>
    public async Task<IReadOnlyList<string>> GetUserChessGameIdsAsync(string uid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return [];
        }

        var json = await _app.RealtimeDatabase.Child("users").Child(uid).Child("UserChessGames").OnceAsJsonAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            return [];
        }

        var dict = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
        if (dict is null || dict.Count == 0)
        {
            return [];
        }

        var keys = new List<string>();
        foreach (var k in dict.Keys)
        {
            if (!string.IsNullOrWhiteSpace(k))
            {
                keys.Add(k);
            }
        }

        keys.Sort(StringComparer.Ordinal);
        keys.Reverse();
        return keys;
    }

    /// <summary>Читает <c>ChessGames/{gameId}</c>.</summary>
    public async Task<SavedGame?> GetChessGameByIdAsync(string gameId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var json = await _app.RealtimeDatabase.Child("ChessGames").Child(gameId).OnceAsJsonAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<SavedGame>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Завершённые партии пользователя для профиля и экрана истории.</summary>
    public async Task<IReadOnlyList<PastGame>> LoadPlayedGameSummariesForProfileAsync(string profileUid,
        int maxGames = 80,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileUid))
        {
            return [];
        }

        var ids = await GetUserChessGameIdsAsync(profileUid, cancellationToken).ConfigureAwait(false);
        if (ids.Count == 0)
        {
            return [];
        }

        var take = Math.Min(maxGames, ids.Count);
        var batch = new List<string>(take);
        for (var i = 0; i < take; i++)
        {
            batch.Add(ids[i]);
        }

        var pairs = new List<(string Id, SavedGame Rec)>();
        foreach (var gid in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rec = await GetChessGameByIdAsync(gid, cancellationToken).ConfigureAwait(false);
            if (rec is null || string.IsNullOrWhiteSpace(rec.WhiteUid))
            {
                continue;
            }

            pairs.Add((gid, rec));
        }

        var list = new List<PastGame>(pairs.Count);
        foreach (var (gid, rec) in pairs)
        {
            DateTimeOffset? ended = null;
            if (!string.IsNullOrWhiteSpace(rec.DateTime)
                && DateTimeOffset.TryParse(rec.DateTime, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                ended = parsed;
            }

            list.Add(new PastGame
            {
                GameId = gid,
                GameWinner = ChessOutcomePalette.NormalizeWinner(rec.Winner),
                EndReason = rec.EndReason ?? string.Empty,
                EndedAt = ended
            });
        }

        list.Sort((a, b) =>
        {
            var ta = a.EndedAt ?? DateTimeOffset.MinValue;
            var tb = b.EndedAt ?? DateTimeOffset.MinValue;
            return tb.CompareTo(ta);
        });

        return list;
    }

    /// <summary>
    /// Сохраняет партию и индексирует её у обоих участников (если белые и чёрные — один uid, индекс один раз).
    /// <paramref name="winner"/> — <c>White</c>, <c>Black</c> или <c>Draw</c>.
    /// </summary>
    public async Task<string?> SaveCompletedGameAsync(
        string whiteUid,
        string blackUid,
        string winner,
        string endReason,
        IReadOnlyList<SavedMove> moves,
        CancellationToken cancellationToken = default)
    {
        if (_app.CurrentUserId is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(whiteUid) || string.IsNullOrWhiteSpace(blackUid))
        {
            return null;
        }

        var db = _app.RealtimeDatabase;
        var gameId = Guid.NewGuid().ToString("N");
        var moveDict = new Dictionary<string, SavedMove>(StringComparer.Ordinal);
        for (var i = 0; i < moves.Count; i++)
        {
            moveDict[$"m{i:D4}"] = moves[i];
        }

        var record = new SavedGame
        {
            WhiteUid = whiteUid,
            BlackUid = blackUid,
            Winner = winner,
            EndReason = endReason,
            DateTime = DateTimeOffset.UtcNow.ToString("o"),
            Moves = moveDict
        };

        var gameJson = JsonConvert.SerializeObject(record);
        await db.Child("ChessGames").Child(gameId).PutAsync(gameJson).WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        await IndexGameForUserAsync(db, whiteUid, gameId, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(blackUid, whiteUid, StringComparison.Ordinal))
        {
            await IndexGameForUserAsync(db, blackUid, gameId, cancellationToken).ConfigureAwait(false);
        }

        return gameId;
    }

    private static async Task IndexGameForUserAsync(
        FirebaseClient db,
        string uid,
        string gameId,
        CancellationToken cancellationToken)
    {
        await db.Child("users").Child(uid).Child("UserChessGames").Child(gameId)
            .PutAsync(JsonConvert.SerializeObject(true)).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Список uid в <c>users/{me}/respects</c>.</summary>
    public async Task<IReadOnlyList<string>> GetRespectUidsAsync(CancellationToken cancellationToken = default)
    {
        var me = _app.CurrentUserId;
        if (me is null)
        {
            return [];
        }

        var db = _app.RealtimeDatabase.Child("users").Child(me);
        return await ReadUidMapKeysAsync(db.Child("respects"), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<string>> ReadUidMapKeysAsync(
        ChildQuery node,
        CancellationToken cancellationToken)
    {
        var json = await node.OnceAsJsonAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            return [];
        }

        var dict = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
        if (dict is null)
        {
            return [];
        }

        var keys = new List<string>();
        foreach (var k in dict.Keys)
        {
            if (!string.IsNullOrWhiteSpace(k))
            {
                keys.Add(k);
            }
        }

        return keys;
    }

    /// <summary>Загружает профили по respect list.</summary>
    public async Task<IReadOnlyList<RespectUser>> LoadRespectsAsync(CancellationToken cancellationToken = default)
    {
        var uids = await GetRespectUidsAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<RespectUser>(uids.Count);
        foreach (var uid in uids)
        {
            var dto = await GetUserProfileByUidAsync(uid, cancellationToken).ConfigureAwait(false);
            if (dto is null)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(dto.UserName) ? uid[..Math.Min(8, uid.Length)] : dto.UserName.Trim();
            list.Add(new RespectUser
            {
                Uid = uid,
                DisplayName = name,
                AvatarUrl = dto.AvatarUrl
            });
        }

        return list;
    }

    /// <summary>Добавляет uid в respect list и индекс <c>respectReceived</c>.</summary>
    public async Task AddRespectAsync(string targetUid, CancellationToken cancellationToken = default)
    {
        var me = _app.CurrentUserId;
        if (me is null || string.IsNullOrWhiteSpace(targetUid) || string.Equals(targetUid, me, StringComparison.Ordinal))
        {
            return;
        }

        var db = _app.RealtimeDatabase;
        var t = JsonConvert.SerializeObject(true);
        await db.Child("users").Child(me).Child("respects").Child(targetUid).PutAsync(t).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.Child("respectReceived").Child(targetUid).Child(me).PutAsync(t).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Удаляет из respect list и из <c>respectReceived</c>.</summary>
    public async Task RemoveRespectAsync(string targetUid, CancellationToken cancellationToken = default)
    {
        var me = _app.CurrentUserId;
        if (me is null || string.IsNullOrWhiteSpace(targetUid))
        {
            return;
        }

        var db = _app.RealtimeDatabase;
        await db.Child("users").Child(me).Child("respects").Child(targetUid).DeleteAsync().WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.Child("respectReceived").Child(targetUid).Child(me).DeleteAsync().WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Число пользователей, отметивших <paramref name="profileUid"/> в respect list.</summary>
    public async Task<int> GetRespectReceivedCountAsync(string profileUid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileUid))
        {
            return 0;
        }

        var json = await _app.RealtimeDatabase.Child("respectReceived").Child(profileUid).OnceAsJsonAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            return 0;
        }

        var dict = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
        return dict?.Count ?? 0;
    }

    /// <summary>
    /// Поиск игроков по префиксу <see cref="SigmaChess.Models.UserProfile.UserNameLower" />.
    /// Шаг 1: REST-запрос с orderBy/startAt/endAt.
    /// Шаг 2: если пусто или ошибка — скачать <c>users.json</c> и отфильтровать на клиенте.
    /// </summary>
    public async Task<IReadOnlyList<RespectUser>> SearchUsersByPrefixAsync(string prefix, int limit = 24,
        CancellationToken cancellationToken = default)
    {
        var me = _app.CurrentUserId;
        if (me is null)
        {
            return [];
        }

        var lower = prefix.Trim().ToLowerInvariant();
        if (lower.Length < 2)
        {
            return [];
        }

        var token = await _app.GetIdTokenAsync(false).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            return [];
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            var ordered = await TrySearchUsersOrderedAsync(http, lower, limit, token, me, cancellationToken)
                .ConfigureAwait(false);
            if (ordered is { Count: > 0 })
            {
                return ordered;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SearchUsers step1 (orderBy): {ex.Message}");
        }

        try
        {
            return await SearchUsersClientSideAsync(lower, limit, token, me, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SearchUsers step2 (client): {ex.Message}");
            return [];
        }
    }

    private static bool LooksLikeFirebaseErrorPayload(string body) =>
        body.Contains("\"error\"", StringComparison.Ordinal)
        || body.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<RespectUser>?> TrySearchUsersOrderedAsync(
        HttpClient http,
        string lower,
        int limit,
        string token,
        string me,
        CancellationToken cancellationToken)
    {
        var orderBy = Uri.EscapeDataString(JsonConvert.SerializeObject("UserNameLower"));
        var startAtEnc = Uri.EscapeDataString(JsonConvert.SerializeObject(lower));
        var endAtEnc = Uri.EscapeDataString(JsonConvert.SerializeObject(lower + "\uf8ff"));
        var authEnc = Uri.EscapeDataString(token);
        var url =
            $"{RtdbRestRoot}/users.json?orderBy={orderBy}&startAt={startAtEnc}&endAt={endAtEnc}&limitToFirst={limit + 8}&auth={authEnc}";

        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode
            || string.IsNullOrWhiteSpace(body)
            || body.Trim() == "null"
            || LooksLikeFirebaseErrorPayload(body))
        {
            return null;
        }

        return DeserializeAndFilterSearchResults(body, me, limit);
    }

    private async Task<IReadOnlyList<RespectUser>> SearchUsersClientSideAsync(
        string lower,
        int limit,
        string token,
        string me,
        CancellationToken cancellationToken)
    {
        var authEnc = Uri.EscapeDataString(token);
        var url = $"{RtdbRestRoot}/users.json?auth={authEnc}";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode
            || string.IsNullOrWhiteSpace(body)
            || body.Trim() == "null"
            || LooksLikeFirebaseErrorPayload(body))
        {
            return [];
        }

        var allProfiles = CollectUserProfilesSortedByUid(body);
        var results = new List<RespectUser>();

        foreach (var kv in allProfiles)
        {
            var row = MatchUserForClientSearch(kv, me, lower, prefixOnly: true);
            if (row is not null)
            {
                results.Add(row);
                if (results.Count >= limit)
                {
                    return results;
                }
            }
        }

        if (results.Count == 0)
        {
            foreach (var kv in allProfiles)
            {
                var row = MatchUserForClientSearch(kv, me, lower, prefixOnly: false);
                if (row is not null)
                {
                    results.Add(row);
                    if (results.Count >= limit)
                    {
                        break;
                    }
                }
            }
        }

        return results;
    }

    private static List<KeyValuePair<string, UserProfile>> CollectUserProfilesSortedByUid(string body)
    {
        var list = new List<KeyValuePair<string, UserProfile>>();
        foreach (var kv in EnumerateUserProfiles(body))
        {
            list.Add(kv);
        }

        list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return list;
    }

    private static RespectUser? MatchUserForClientSearch(
        KeyValuePair<string, UserProfile> kv,
        string me,
        string lower,
        bool prefixOnly)
    {
        if (string.Equals(kv.Key, me, StringComparison.Ordinal))
        {
            return null;
        }

        var dto = kv.Value;
        if (string.IsNullOrWhiteSpace(dto.UserName))
        {
            return null;
        }

        var nameLower = !string.IsNullOrWhiteSpace(dto.UserNameLower)
            ? dto.UserNameLower.Trim().ToLowerInvariant()
            : dto.UserName.Trim().ToLowerInvariant();

        var matches = prefixOnly
            ? nameLower.StartsWith(lower, StringComparison.Ordinal)
            : nameLower.Contains(lower, StringComparison.Ordinal);

        if (!matches)
        {
            return null;
        }

        return new RespectUser
        {
            Uid = kv.Key,
            DisplayName = dto.UserName.Trim(),
            AvatarUrl = dto.AvatarUrl
        };
    }

    private static IEnumerable<KeyValuePair<string, UserProfile>> EnumerateUserProfiles(string body)
    {
        JToken root;
        try
        {
            root = JToken.Parse(body);
        }
        catch (JsonException)
        {
            yield break;
        }

        if (root is not JObject jo)
        {
            yield break;
        }

        foreach (var prop in jo.Properties())
        {
            if (prop.Value is not JObject _)
            {
                continue;
            }

            UserProfile? dto;
            try
            {
                dto = prop.Value.ToObject<UserProfile>();
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto is not null)
            {
                yield return new KeyValuePair<string, UserProfile>(prop.Name, dto);
            }
        }
    }

    private static IReadOnlyList<RespectUser> DeserializeAndFilterSearchResults(string body, string me, int limit)
    {
        var list = new List<RespectUser>();
        foreach (var kv in EnumerateUserProfiles(body))
        {
            if (string.Equals(kv.Key, me, StringComparison.Ordinal))
            {
                continue;
            }

            var dto = kv.Value;
            if (string.IsNullOrWhiteSpace(dto.UserName))
            {
                continue;
            }

            list.Add(new RespectUser
            {
                Uid = kv.Key,
                DisplayName = dto.UserName.Trim(),
                AvatarUrl = dto.AvatarUrl
            });

            if (list.Count >= limit)
            {
                break;
            }
        }

        return list;
    }
}
