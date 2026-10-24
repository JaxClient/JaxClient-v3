using System.Net.Http;
using System.Text;
using System.Text.Json;
using MinecraftSkinRender.MojangApi;

namespace JaxClient_v3.BodyRendering;


public static class MinecraftAPI
{
    public static readonly HttpClient Client = new();

    public const string ProfileName = "https://api.mojang.com/users/profiles/minecraft";
    public const string Profile = "https://api.minecraftservices.com/minecraft/profile";
    public const string UserProfile = "https://sessionserver.mojang.com/session/minecraft/profile";

    /// <summary>
    /// Gets a UUID from a Minecraft username.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static async Task<ProfileNameObj?> GetMinecraftProfileNameAsync(string name)
    {
        string url = $"{ProfileName}/{name}";
        var data = await Client.GetStringAsync(url);

        return JsonSerializer.Deserialize(data, JsonType.ProfileNameObj);
    }

    /// <summary>
    /// Gets account information.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <returns>Account information.</returns>
    public static async Task<MinecraftProfileObj?> GetMinecraftProfileAsync(string accessToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, Profile);
        message.Headers.Add("Authorization", $"Bearer {accessToken}");
        var data = await Client.SendAsync(message);
        var data1 = await data.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(data1, JsonType.MinecraftProfileObj);
    }

    /// <summary>
    /// Gets profile information, including skin data.
    /// </summary>
    /// <param name="uuid">The profile UUID.</param>
    /// <param name="url">The profile URL.</param>
    /// <returns>Profile information.</returns>
    public static async Task<UserProfileObj?> GetUserProfile(string? uuid)
    {
        var url = $"{UserProfile}/{uuid}";
        var data = await Client.GetStringAsync(url);

        return JsonSerializer.Deserialize(data, JsonType.UserProfileObj);
    }

    /// <summary>
    /// Converts a Base64 string to texture data.<br/>
    /// The Base64 string is obtained from <see cref="UserProfileObj.PropertiesObj.Value"/>.
    /// </summary>
    /// <param name="base64">The input Base64 string.</param>
    /// <returns></returns>
    public static TexturesObj? Base64ToTexture(string base64)
    {
        var data = Convert.FromBase64String(base64);
        var data1 = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize(data1, JsonType.TexturesObj);
    }
}
