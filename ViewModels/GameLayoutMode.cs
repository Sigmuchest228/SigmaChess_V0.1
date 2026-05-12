namespace SigmaChess.ViewModels;

/// <summary>Раскладка экрана партии: обычная (экран) или «за доской» (обе стороны).</summary>
public enum GameLayoutMode
{
    /// <summary>Таймеры сверху/снизу доски, одна колонка записи справа.</summary>
    Casual,

    /// <summary>Таймеры сбоку, две колонки записей, чёрные фигуры повёрнуты на 180°.</summary>
    FaceToFace
}
