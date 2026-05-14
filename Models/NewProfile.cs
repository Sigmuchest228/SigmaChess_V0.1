using Newtonsoft.Json;



namespace SigmaChess.Models;



/// <summary>Тело первого <c>Put</c> узла <c>users/{uid}</c> при создании профиля.</summary>

public class NewProfile

{

    [JsonProperty("UserName")]

    public string UserName { get; set; } = string.Empty;



    [JsonProperty("UserNameLower")]

    public string UserNameLower { get; set; } = string.Empty;



    [JsonProperty("RegisterDate")]

    public long RegisterDate { get; set; }

}

