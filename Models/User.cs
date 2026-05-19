using Newtonsoft.Json;

namespace SigmaChess.Models;

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
