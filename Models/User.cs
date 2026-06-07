using Newtonsoft.Json;

namespace SigmaChess.Models;

// Модель пользователя так, как он хранится в Firebase. Атрибуты [JsonProperty] задают
// имена полей в базе. Хранит имя (UserName), его версию в нижнем регистре для поиска
// (UserNameLower), дату регистрации (RegisterDate, Unix-время в секундах) и при наличии
// ссылку на аватар (AvatarUrl). Используется FirebaseSyncRepository при чтении/записи
// профиля и в UserProfileViewModel для показа данных.
public class User
{
    [JsonProperty("UserName")]
    public string? UserName { get; set; }

    [JsonProperty("UserNameLower")]
    public string? UserNameLower { get; set; }

    [JsonProperty("RegisterDate")]
    public int? RegisterDate { get; set; }

    [JsonProperty("AvatarUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string? AvatarUrl { get; set; }
}
