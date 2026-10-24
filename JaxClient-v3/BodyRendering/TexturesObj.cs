using System.Text.Json.Serialization;

namespace MinecraftSkinRender.MojangApi;

/// <summary>
/// Minecraft texture data.
/// </summary>
public record TexturesObj
{
    public record TexturesObj1
    {
        public record SkinObj
        {
            public record MetadataObj
            {
                [JsonPropertyName("model")]
                public string Model { get; set; }
            }
            [JsonPropertyName("url")]
            public string Url { get; set; }
            [JsonPropertyName("metadata")]
            public MetadataObj Metadata { get; set; }
        }
        public record CapeObj
        {
            [JsonPropertyName("url")]
            public string Url { get; set; }
        }
        [JsonPropertyName("SKIN")]
        public SkinObj Skin { get; set; }
        [JsonPropertyName("CAPE")]
        public CapeObj Cape { get; set; }
    }
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; }
    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; }
    [JsonPropertyName("signatureRequired")]
    public bool SignatureRequired { get; set; }
    [JsonPropertyName("textures")]
    public TexturesObj1 Textures { get; set; }
}
