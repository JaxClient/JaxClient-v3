using System.Text.Json.Serialization;

namespace JaxClient_v3.BodyRendering;


public record ProfileNameObj
{
    [JsonPropertyName("id")]
    public string UUID { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
}
