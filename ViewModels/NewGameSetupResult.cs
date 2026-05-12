namespace SigmaChess.ViewModels;

/// <summary>Выбор времени и режима раскладки при старте партии.</summary>
public sealed record NewGameSetupResult(
    bool Unlimited,
    bool SameTimeForBoth,
    int WhiteMinutes,
    int BlackMinutes,
    GameLayoutMode LayoutMode);
