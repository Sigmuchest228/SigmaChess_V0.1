using System.Diagnostics;
using System.Net.Http;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigmaChess.Engine;

namespace SigmaChess.Services;

/// <summary>
/// Запись в RTDB: профиль и завершённые партии. Пути и JSON — только здесь.
/// </summary>
public sealed class FirebaseSyncRepository
{
    /// <summary>Совпадает с базой в <see cref="AppService"/> (REST-запросы префикс-поиска).</summary>
    private const string RtdbRestRoot =
        "https://sigmachess-75f04-default-rtdb.europe-west1.firebasedatabase.app";

    private readonly AppService _app;

    public FirebaseSyncRepository(AppService app)
    {
        _app = app;
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
            var body = new { UserName = displayName, UserNameLower = lower, PuzzlesSolved = 0, RegisterDate = unix };
            await db.Child("users").Child(uid).PutAsync(JsonConvert.SerializeObject(body)).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var dto = JsonConvert.DeserializeObject<UserProfileRtdbDto>(json);
        if (!string.IsNullOrWhiteSpace(dto?.UserName))
        {
            var syncLower = dto.UserName.Trim().ToLowerInvariant();
            var patchMap = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(dto.UserNameLower)
                || !string.Equals(dto.UserNameLower, syncLower, StringComparison.Ordinal))
            {
                patchMap["UserNameLower"] = syncLower;
            }

            if (!dto.PuzzlesSolved.HasValue)
            {
                patchMap["PuzzlesSolved"] = 0;
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
        if (dto is null || !dto.PuzzlesSolved.HasValue)
        {
            patchNameMap["PuzzlesSolved"] = 0;
        }

        await db.Child("users").Child(uid).PatchAsync(JsonConvert.SerializeObject(patchNameMap)).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Читает узел <c>users/{uid}</c> как DTO или <c>null</c>.</summary>
    public async Task<UserProfileRtdbDto?> GetUserProfileByUidAsync(string uid,
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

        return JsonConvert.DeserializeObject<UserProfileRtdbDto>(json);
    }

    /// <summary>Читает профиль текущего пользователя.</summary>
    public Task<UserProfileRtdbDto?> GetUserProfileAsync(CancellationToken cancellationToken = default)
    {
        var uid = _app.CurrentUserId;
        return uid is null
            ? Task.FromResult<UserProfileRtdbDto?>(null)
            : GetUserProfileByUidAsync(uid, cancellationToken);
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
        return dict is null || dict.Count == 0
            ? []
            : dict.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).OrderDescending(StringComparer.Ordinal).ToList();
    }

    /// <summary>Читает <c>ChessGames/{gameId}</c>.</summary>
    public async Task<FirebaseGameRecord?> GetChessGameByIdAsync(string gameId,
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
            return JsonConvert.DeserializeObject<FirebaseGameRecord>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Завершённые партии пользователя для профиля и экрана истории.</summary>
    public async Task<IReadOnlyList<PlayedGameSummary>> LoadPlayedGameSummariesForProfileAsync(string profileUid,
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
        var batch = ids.Take(take).ToList();

        var pairs = new List<(string Id, FirebaseGameRecord Rec)>(batch.Count);
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

        var list = new List<PlayedGameSummary>(pairs.Count);
        foreach (var (gid, rec) in pairs)
        {
            DateTimeOffset? ended = null;
            if (!string.IsNullOrWhiteSpace(rec.DateTime)
                && DateTimeOffset.TryParse(rec.DateTime, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                ended = parsed;
            }

            list.Add(new PlayedGameSummary
            {
                GameId = gid,
                GameWinner = ChessOutcomePalette.NormalizeWinner(rec.Winner),
                EndReason = rec.EndReason ?? string.Empty,
                EndedAt = ended
            });
        }

        return list.OrderByDescending(x => x.EndedAt ?? DateTimeOffset.MinValue).ToList();
    }

    /// <summary>Список uid в <c>users/{me}/follows</c>; при пустом <c>follows</c> одноразово копирует из устаревшего <c>friends</c>.</summary>
    public async Task<IReadOnlyList<string>> GetFollowUidsAsync(CancellationToken cancellationToken = default)
    {
        var me = _app.CurrentUserId;
        if (me is null)
        {
            return [];
        }

        var db = _app.RealtimeDatabase.Child("users").Child(me);
        var followKeys = await ReadUidMapKeysAsync(db.Child("follows"), cancellationToken).ConfigureAwait(false);
        if (followKeys.Count > 0)
        {
            return followKeys;
        }

        var legacyFriendKeys = await ReadUidMapKeysAsync(db.Child("friends"), cancellationToken).ConfigureAwait(false);
        if (legacyFriendKeys.Count == 0)
        {
            return [];
        }

        foreach (var uid in legacyFriendKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await db.Child("follows").Child(uid).PutAsync(JsonConvert.SerializeObject(true)).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return legacyFriendKeys;
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
        return dict is null
            ? []
            : dict.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
    }

    /// <summary>Загружает профили по списку подписок.</summary>
    public async Task<IReadOnlyList<FollowProfileSummary>> LoadFollowsAsync(CancellationToken cancellationToken = default)
    {
        var uids = await GetFollowUidsAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<FollowProfileSummary>(uids.Count);
        foreach (var uid in uids)
        {
            var dto = await GetUserProfileByUidAsync(uid, cancellationToken).ConfigureAwait(false);
            if (dto is null)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(dto.UserName) ? uid[..Math.Min(8, uid.Length)] : dto.UserName.Trim();
            list.Add(new FollowProfileSummary
            {
                Uid = uid,
                DisplayName = name,
                PuzzlesSolved = UserSigmaRank.NormalizePuzzlesSolved(dto.PuzzlesSolved),
                AvatarUrl = dto.AvatarUrl
            });
        }

        return list;
    }

    /// <summary>Добавляет uid в <c>users/{me}/follows</c>.</summary>
    public async Task AddFollowAsync(string targetUid, CancellationToken cancellationToken = default)
    {
        var me = _app.CurrentUserId;
        if (me is null || string.IsNullOrWhiteSpace(targetUid) || string.Equals(targetUid, me, StringComparison.Ordinal))
        {
            return;
        }

        await _app.RealtimeDatabase.Child("users").Child(me).Child("follows").Child(targetUid)
            .PutAsync(JsonConvert.SerializeObject(true)).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Увеличивает <c>PuzzlesSolved</c> текущего пользователя на 1 (для экрана задач).</summary>
    public async Task IncrementPuzzlesSolvedAsync(CancellationToken cancellationToken = default)
    {
        var uid = _app.CurrentUserId;
        if (uid is null)
        {
            return;
        }

        try
        {
            await EnsureUserProfileAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var profile = await GetUserProfileByUidAsync(uid, cancellationToken).ConfigureAwait(false);
            var next = UserSigmaRank.NormalizePuzzlesSolved(profile?.PuzzlesSolved) + 1;
            await _app.RealtimeDatabase.Child("users").Child(uid).PatchAsync(JsonConvert.SerializeObject(new { PuzzlesSolved = next }))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IncrementPuzzlesSolvedAsync: {ex.Message}");
        }
    }

    /// <summary>Все задачи из <c>ChessPuzzles</c>.</summary>
    public async Task<IReadOnlyList<(string Id, FirebasePuzzleDto Dto)>> LoadChessPuzzlesOrderedAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _app.RealtimeDatabase.Child("ChessPuzzles").OnceAsJsonAsync()
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
            {
                return [];
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, FirebasePuzzleDto>>(json);
            if (dict is null || dict.Count == 0)
            {
                return [];
            }

            return dict
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadChessPuzzlesOrderedAsync: {ex.Message}");
            return [];
        }
    }

    /// <summary>Одна задача по id.</summary>
    public async Task<FirebasePuzzleDto?> GetPuzzleByIdAsync(string puzzleId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(puzzleId))
        {
            return null;
        }

        try
        {
            var json = await _app.RealtimeDatabase.Child("ChessPuzzles").Child(puzzleId).OnceAsJsonAsync()
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
            {
                return null;
            }

            return JsonConvert.DeserializeObject<FirebasePuzzleDto>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetPuzzleByIdAsync: {ex.Message}");
            return null;
        }
    }

    public async Task<HashSet<string>> GetSolvedPuzzleIdsAsync(string uid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            var json = await _app.RealtimeDatabase.Child("users").Child(uid).Child("puzzleProgress").OnceAsJsonAsync()
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (dict is null || dict.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            return dict.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetSolvedPuzzleIdsAsync: {ex.Message}");
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Первое решение: пишет <c>puzzleProgress</c> и увеличивает <c>PuzzlesSolved</c>.</summary>
    public async Task<bool> TryMarkPuzzleSolvedFirstTimeAsync(string puzzleId,
        CancellationToken cancellationToken = default)
    {
        var uid = _app.CurrentUserId;
        if (uid is null || string.IsNullOrWhiteSpace(puzzleId))
        {
            return false;
        }

        try
        {
            await EnsureUserProfileAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var progressRef = _app.RealtimeDatabase.Child("users").Child(uid).Child("puzzleProgress").Child(puzzleId);
            var existing = await progressRef.OnceAsJsonAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing) && existing.Trim() != "null")
            {
                return false;
            }

            await progressRef.PutAsync(JsonConvert.SerializeObject(true)).WaitAsync(cancellationToken).ConfigureAwait(false);
            var profile = await GetUserProfileByUidAsync(uid, cancellationToken).ConfigureAwait(false);
            var next = UserSigmaRank.NormalizePuzzlesSolved(profile?.PuzzlesSolved) + 1;
            await _app.RealtimeDatabase.Child("users").Child(uid).PatchAsync(JsonConvert.SerializeObject(new { PuzzlesSolved = next }))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryMarkPuzzleSolvedFirstTimeAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>Поиск по префиксу <see cref="UserProfileRtdbDto.UserNameLower"/> (индекс в RTDB). При ошибке запроса — полная выборка <c>users</c> и фильтр на клиенте (для небольших баз).</summary>
    public async Task<IReadOnlyList<FollowProfileSummary>> SearchUsersByPrefixAsync(string prefix, int limit = 24,
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
            // При успешном HTTP Firebase может вернуть 200 и "{}". Раньше мы считали это финальным ответом
            // и НИКОГДА не вызывали fallback — из‑за этого поиск всегда был пустым при сломанном orderBy/индексе.
            if (ordered is { Count: > 0 })
            {
                return ordered;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SearchUsers ordered query: {ex.Message}");
        }

        try
        {
            return await SearchUsersClientSideAsync(lower, limit, token, me, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SearchUsers client fallback: {ex.Message}");
            return [];
        }
    }

    private static bool LooksLikeFirebaseErrorPayload(string body) =>
        body.Contains("\"error\"", StringComparison.Ordinal)
        || body.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<FollowProfileSummary>?> TrySearchUsersOrderedAsync(
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

    private async Task<IReadOnlyList<FollowProfileSummary>> SearchUsersClientSideAsync(
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

        var list = new List<FollowProfileSummary>();
        foreach (var kv in EnumerateUserProfiles(body).OrderBy(x => x.Key, StringComparer.Ordinal))
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

            var nameLower = !string.IsNullOrWhiteSpace(dto.UserNameLower)
                ? dto.UserNameLower.Trim().ToLowerInvariant()
                : dto.UserName.Trim().ToLowerInvariant();

            if (!nameLower.StartsWith(lower, StringComparison.Ordinal))
            {
                continue;
            }

            list.Add(new FollowProfileSummary
            {
                Uid = kv.Key,
                DisplayName = dto.UserName.Trim(),
                PuzzlesSolved = UserSigmaRank.NormalizePuzzlesSolved(dto.PuzzlesSolved),
                AvatarUrl = dto.AvatarUrl
            });

            if (list.Count >= limit)
            {
                break;
            }
        }

        return list;
    }

    /// <summary>
    /// Обход <c>users.json</c>: значение каждого uid — объект профиля (лишние поля вроде <c>follows</c>/<c>friends</c> игнорируются).
    /// По узлу на ошибку десериализации — не ломаем весь ответ.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, UserProfileRtdbDto>> EnumerateUserProfiles(string body)
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

            UserProfileRtdbDto? dto;
            try
            {
                dto = prop.Value.ToObject<UserProfileRtdbDto>();
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto is not null)
            {
                yield return new KeyValuePair<string, UserProfileRtdbDto>(prop.Name, dto);
            }
        }
    }

    private static IReadOnlyList<FollowProfileSummary> DeserializeAndFilterSearchResults(string body, string me, int limit)
    {
        var list = new List<FollowProfileSummary>();
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

            list.Add(new FollowProfileSummary
            {
                Uid = kv.Key,
                DisplayName = dto.UserName.Trim(),
                PuzzlesSolved = UserSigmaRank.NormalizePuzzlesSolved(dto.PuzzlesSolved),
                AvatarUrl = dto.AvatarUrl
            });

            if (list.Count >= limit)
            {
                break;
            }
        }

        return list;
    }

    /// <summary>Частичное обновление полей оформления (AvatarUrl, WallpaperPreset, WallpaperCustomUrl).</summary>
    public async Task PatchUserAppearanceAsync(IReadOnlyDictionary<string, object?> fields,
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

    /// <summary>
    /// Сохраняет партию и индексирует её у обоих участников (если белые и чёрные — один uid, индекс один раз).
    /// <paramref name="winner"/> — <c>White</c>, <c>Black</c> или <c>Draw</c>.
    /// </summary>
    public async Task<string?> SaveCompletedGameAsync(
        string whiteUid,
        string blackUid,
        string winner,
        string endReason,
        IReadOnlyList<FirebaseMoveRecord> moves,
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
        var moveDict = new Dictionary<string, FirebaseMoveRecord>(StringComparer.Ordinal);
        for (var i = 0; i < moves.Count; i++)
        {
            moveDict[$"m{i:D4}"] = moves[i];
        }

        var record = new FirebaseGameRecord
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
        // JSON literal true
        await db.Child("users").Child(uid).Child("UserChessGames").Child(gameId)
            .PutAsync(JsonConvert.SerializeObject(true)).WaitAsync(cancellationToken).ConfigureAwait(false);
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
}
