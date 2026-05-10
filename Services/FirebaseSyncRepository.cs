using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using SigmaChess.Engine;

namespace SigmaChess.Services;

/// <summary>
/// Запись в RTDB: профиль и завершённые партии. Пути и JSON — только здесь.
/// </summary>
public sealed class FirebaseSyncRepository
{
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

        var json = await db.Child("users").Child(uid).OnceAsJsonAsync()
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
        {
            var unix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var body = new { UserName = displayName, Elo = 1200, RegisterDate = unix };
            await db.Child("users").Child(uid).PutAsync(JsonConvert.SerializeObject(body)).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var dto = JsonConvert.DeserializeObject<UserProfileRtdbDto>(json);
        if (!string.IsNullOrWhiteSpace(dto?.UserName))
        {
            return;
        }

        var patch = new { UserName = displayName };
        await db.Child("users").Child(uid).PatchAsync(JsonConvert.SerializeObject(patch)).WaitAsync(cancellationToken)
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
