using System.IO;
using System.Windows;
using JaxClient_v3;

namespace JaxClient_v3.BodyRendering;

using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using System.Runtime.InteropServices;




internal class SkinPreviewWindow
{
    

    public static async Task<string> ShowPreview(
        string skinFile,
        string skinName)
{
    
    bool havecape = true;
    bool screenshotDone = false;
    string resultPath = "";

    await SkinDownloader.Download();

    using var window = Window.Create(
        WindowOptions.Default with
        {
            IsVisible = false,
            Size = new(300, 400),
            
            VSync = false
        });

    GL gl = null;
    SkinRenderOpenGL? skin = null;

    window.Load += () =>
    {
        

        gl = window.CreateOpenGL();

        skin = new(new SlikOpenglApi(gl))
        {
            IsGLES = true
        };

        var img = SKBitmap.Decode(skinFile);

        if (img == null)
        {
            MessageBox.Show(
                LocalizationManager.Instance.Translate(
                    "Failed to load skin."));
            window.Close();
            return;
        }

        skin.SetSkinTex(img);

        skin.SkinType = SkinType.NewSlim;
        skin.EnableTop = true;
        skin.RenderType = SkinRenderType.Normal;
        skin.Animation = true;
        skin.EnableCape = true;

        if (havecape && File.Exists("cape.png"))
        {
            var cape = SKBitmap.Decode("cape.png");

            if (cape != null)
            {
                skin.SetCapeTex(cape);
            }
        }

        skin.BackColor = new(0, 0, 0, 0);
        skin.Width = window.FramebufferSize.X;
        skin.Height = window.FramebufferSize.Y;

        skin.OpenGlInit();
       
    };

    window.FramebufferResize += size =>
    {
        if (skin == null)
            return;

        skin.Width = size.X;
        skin.Height = size.Y;
    };

    window.Render += delta =>
    {
        
        if (skin == null || screenshotDone)
            return;

        //skin.Rot(0, 3f);
        //skin.Tick(delta);
        skin.OpenGlRender(0);

        gl.Finish();

        screenshotDone = true;

        int width = skin.Width;
        int height = skin.Height;

        byte[] pixels = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* ptr = pixels)
            {
                gl.ReadPixels(
                    0,
                    0,
                    (uint)width,
                    (uint)height,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr);
            }
        }

        var bitmap = new SKBitmap(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        Marshal.Copy(
            pixels,
            0,
            bitmap.GetPixels(),
            pixels.Length);

        using var flipped = new SKBitmap(width, height);

        using (var canvas = new SKCanvas(flipped))
        {
            canvas.Scale(1, -1);
            canvas.Translate(0, -height);
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        using var image = SKImage.FromBitmap(flipped);
        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        string outputPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource",
                $"{skinName}.png");
        File.WriteAllBytes(
            outputPath,
            data.ToArray());

        

        window.Close();
        resultPath = outputPath;
        
    };

    window.Closing += () =>
    {
        if (skin != null)
        {
            skin.OpenGlDeinit();
        }

        gl?.Dispose();
    };

    window.Run();
    return resultPath;
}
}
