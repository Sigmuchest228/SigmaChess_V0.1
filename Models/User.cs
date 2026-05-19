using Newtonsoft.Json;

namespace SigmaChess.Models;

/// <summary>Данные профиля пользователя в RTDB (узел users/{uid}).</summary>
public class User
{
    [JsonProperty("UserName")]
    public string? UserName { get; set; }

    /// <summary>Имя в нижнем регистре для префикс-поиска в RTDB (<c>OrderByChild</c>).</summary>
    [JsonProperty("UserNameLower")]
    public string? UserNameLower { get; set; }

    /// <summary>Unix time в секундах (UTC); <c>int</c> достаточен до 2038.</summary>
    [JsonProperty("RegisterDate")]
    public int? RegisterDate { get; set; }

    /// <summary>Download URL после загрузки в Firebase Storage, аватар.</summary>
    [JsonProperty("AvatarUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? AvatarUrl { get; set; }
}
