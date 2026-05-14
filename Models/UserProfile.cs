using Newtonsoft.Json;

namespace SigmaChess.Models;

/// <summary>Данные профиля пользователя в RTDB (узел users/{uid}).</summary>
public class UserProfile
{
    [JsonProperty("UserName")]
    public string? UserName { get; set; }

    /// <summary>Имя в нижнем регистре для префикс-поиска в RTDB (<c>OrderByChild</c>).</summary>
    [JsonProperty("UserNameLower")]
    public string? UserNameLower { get; set; }

    [JsonProperty("RegisterDate")]
    public long? RegisterDate { get; set; }

    /// <summary>Download URL после загрузки в Firebase Storage, аватар.</summary>
    [JsonProperty("AvatarUrl")]
    public string? AvatarUrl { get; set; }
}
