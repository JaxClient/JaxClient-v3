using System.Text.Json.Serialization;

namespace MinecraftSkinRender.MojangApi;

/// <summary>
/// Minecraft account information.
/// </summary>
/// <value></value>
public record MinecraftProfileObj
{
    public record MinecraftSkinObj : CapesObj
    {
        [JsonPropertyName("variant")]
        public string Variant { get; set; }
    }

    public record CapesObj
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("alias")]
        public string Alias { get; set; }
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("skins")]
    public List<MinecraftSkinObj> Skins { get; set; }
    [JsonPropertyName("capes")]
    public List<CapesObj> Capes { get; set; }
}
