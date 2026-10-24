using SkiaSharp;

namespace JaxClient_v3.Rendering;

public static class FullBodyRenderer
{
    public static SKImage Render(SKBitmap skin)
    {
        if (skin == null)
            throw new Exception("Invalid skin");

        return Skin3DHeadTypeA.MakeHeadImage(skin);
    }
}