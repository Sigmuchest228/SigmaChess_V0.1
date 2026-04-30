namespace SigmaChess.Engine;

/// <summary>
/// Один шахматный ход: From → To. Promotion заполнен только для превращения пешки
/// (Queen/Rook/Bishop/Knight) — генератор выдаёт по одному Move на каждый вариант,
/// а UI потом подменяет нужный через выражение <c>move with { Promotion = ... }</c>.
/// Sealed record => иммутабельный, free Equals/GetHashCode по полям.
/// </summary>
public sealed record Move(Position From, Position To, PieceType? Promotion = null);
