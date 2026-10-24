using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace JaxClient_v3.BodyRendering;

using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using System.Runtime.InteropServices;




internal class SkinPreviewWindow2
{
    private static readonly SemaphoreSlim PreviewSemaphore = new(1, 1);
    
    public static Action<string>? StartTaskAction;
    public static Action<string>? StopTaskAction;
    
    public static async Task<string> ShowPreview(
        string skinFile,
        string skinName)
{
    await PreviewSemaphore.WaitAsync();
    try
    {
    string gifPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "resource",
        $"{skinName}.gif");

    if (File.Exists(gifPath))
        return gifPath;

    StartTaskAction?.Invoke("Render Skin");
    bool havecape = true;
    bool screenshotDone = false;
    string resultPath = "";
    double recordTime = 0;
    int frameId = 0;
    bool videoDone = false;
    byte[]? pixels = null;
    string framesFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource",
            "render_frames");
    

    Directory.CreateDirectory(framesFolder);

    foreach (var file in Directory.GetFiles(framesFolder))
    {
        File.Delete(file);
    }
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
        
        gl.ClearColor(0f, 0f, 0f, 0f);
      
        
        gl.Clear(
            
        ClearBufferMask.ColorBufferBit |
            
        ClearBufferMask.DepthBufferBit);
        

        
        
        
        skin.Tick(delta);
        skin.OpenGlRender(0);
        //skin.Rot(0. 3f)
        gl.Finish();
        recordTime += delta;
       

        int width = skin.Width;
        int height = skin.Height;

        int pixelLength = checked(width * height * 4);
        if (pixels == null || pixels.Length != pixelLength)
            pixels = new byte[pixelLength];

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

        using var bitmap = new SKBitmap(
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
        string frameFile = Path.Combine(
            framesFolder,
            $"frame_{frameId:D5}.png");

        File.WriteAllBytes(
            frameFile,
            data.ToArray());

        frameId++;
        

        if (recordTime >= 10 && !videoDone)
        {
            videoDone = true;

            try
            {
                using var collection =
                    new ImageMagick.MagickImageCollection();

                var files = Directory.GetFiles(
                        framesFolder,
                        "*.png")
                    .OrderBy(x => x)
                    .ToList();

                foreach (var file in files.Take(27))
                {
                    File.Delete(file);
                }

                foreach (var file in files.Skip(117))
                {
                    File.Delete(file);
                }

                foreach (string file in Directory.GetFiles(
                                 framesFolder,
                                 "*.png")
                             .OrderBy(x => x))
                {
                    var image2 =
                        new ImageMagick.MagickImage(file);

                    image2.AnimationDelay = 2;

                    image2.GifDisposeMethod =
                        ImageMagick.GifDisposeMethod.Background;

                    collection.Add(image2);
                }

                collection.Write(gifPath);
             
                resultPath = gifPath;
            }
            finally
            {
                StopTaskAction?.Invoke("Render Skin");

                try
                {
                    if (Directory.Exists(framesFolder))
                        Directory.Delete(framesFolder, true);
                }
                catch
                {
                }
            }

            window.Close();
            return;
        }
      

        

        
        
        
        
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
    finally
    {
        PreviewSemaphore.Release();
    }
}
}
