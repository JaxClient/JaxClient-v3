using System.Text.Json.Serialization;

namespace MinecraftSkinRender.MojangApi;

/// <summary>
/// Minecraft user profile information.
/// </summary>
/// <value></value>
public record UserProfileObj
{
    public record PropertiesObj
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("value")]
        public string Value { get; set; }
        [JsonPropertyName("signature")]
        public string Signature { get; set; }
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("properties")]
    public List<PropertiesObj> Properties { get; set; }
}
