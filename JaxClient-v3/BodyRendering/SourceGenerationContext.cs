using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MinecraftSkinRender.MojangApi;

namespace JaxClient_v3.BodyRendering;


[JsonSerializable(typeof(ProfileNameObj))]
[JsonSerializable(typeof(UserProfileObj))]
[JsonSerializable(typeof(TexturesObj))]
[JsonSerializable(typeof(MinecraftProfileObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}

internal static class JsonType
{
    public static JsonTypeInfo<ProfileNameObj> ProfileNameObj => SourceGenerationContext.Default.ProfileNameObj;
    public static JsonTypeInfo<UserProfileObj> UserProfileObj => SourceGenerationContext.Default.UserProfileObj;
    public static JsonTypeInfo<TexturesObj> TexturesObj => SourceGenerationContext.Default.TexturesObj;
    public static JsonTypeInfo<MinecraftProfileObj> MinecraftProfileObj => SourceGenerationContext.Default.MinecraftProfileObj;
}