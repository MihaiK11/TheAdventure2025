using Silk.NET.Maths;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.ColorSpaces; 


using TheAdventure.Models;
using Point = Silk.NET.SDL.Point;

namespace TheAdventure;

public unsafe class GameRenderer
{
    private Sdl _sdl;
    private Renderer* _renderer;
    private GameWindow _window;
    private Camera _camera;

    private Dictionary<int, IntPtr> _texturePointers = new();
    private Dictionary<int, TextureData> _textureData = new();
    private int _textureId;

    public GameRenderer(Sdl sdl, GameWindow window)
    {
        _sdl = sdl;
        
        _renderer = (Renderer*)window.CreateRenderer();
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
        
        _window = window;
        var windowSize = window.Size;
        _camera = new Camera(windowSize.Width, windowSize.Height);
    }

    public void SetWorldBounds(Rectangle<int> bounds)
    {
        _camera.SetWorldBounds(bounds);
    }

    public void CameraLookAt(int x, int y)
    {
        _camera.LookAt(x, y);
    }
    
    public (int X, int Y) GetCameraPosition()
    {
        return (_camera.X, _camera.Y);
    }
    
    public (int ScreenWidth, int ScreenHeight) GetScreenSize()
    {
        return (_window.Size.Width, _window.Size.Height);
    }
    public unsafe int DrawText(string text, int x, int y, float fontSize = 24f, Rgba32? color = null)
    {
        var font = SystemFonts.CreateFont("Arial", fontSize);
        var textColor = color ?? new Rgba32(255, 255, 255, 255); // default white

        var richTextOptions = new RichTextOptions(font)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Origin = new PointF(0, 0)
        };

        var textSize = TextMeasurer.MeasureSize(text, richTextOptions);

        using var image = new Image<Rgba32>((int)textSize.Width + 4, (int)textSize.Height + 4);

        image.Mutate(ctx => ctx.DrawText(richTextOptions, text, textColor));

        var pixelData = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixelData);

        fixed (byte* data = pixelData)
        {
            var surface = _sdl.CreateRGBSurfaceWithFormatFrom(
                data, image.Width, image.Height, 32,
                image.Width * 4, (uint)PixelFormatEnum.Rgba32);

            if (surface == null)
                throw new Exception("Failed to create surface from text");

            var texture = _sdl.CreateTextureFromSurface(_renderer, surface);
            _sdl.FreeSurface(surface);

            var textureId = _textureId++;
            _texturePointers[textureId] = (IntPtr)texture;
            _textureData[textureId] = new TextureData
            {
                Width = image.Width,
                Height = image.Height
            };

            // IMPORTANT: Draw text directly to screen coordinates (no camera transform)
            var dst = new Rectangle<int>(x, y, image.Width, image.Height);

            // Use SDL_RenderCopy directly for screen space rendering:
            _sdl.RenderCopy(_renderer, (Silk.NET.SDL.Texture*)texture, null, ref dst);

            return textureId;
        }
    }


    
    public int LoadTexture(string fileName, out TextureData textureInfo)
    {
        using (var fStream = new FileStream(fileName, FileMode.Open))
        {
            var image = Image.Load<Rgba32>(fStream);
            textureInfo = new TextureData()
            {
                Width = image.Width,
                Height = image.Height
            };
            var imageRAWData = new byte[textureInfo.Width * textureInfo.Height * 4];
            image.CopyPixelDataTo(imageRAWData.AsSpan());
            fixed (byte* data = imageRAWData)
            {
                var imageSurface = _sdl.CreateRGBSurfaceWithFormatFrom(data, textureInfo.Width,
                    textureInfo.Height, 8, textureInfo.Width * 4, (uint)PixelFormatEnum.Rgba32);
                if (imageSurface == null)
                {
                    throw new Exception("Failed to create surface from image data.");
                }
                
                var imageTexture = _sdl.CreateTextureFromSurface(_renderer, imageSurface);
                if (imageTexture == null)
                {
                    _sdl.FreeSurface(imageSurface);
                    throw new Exception("Failed to create texture from surface.");
                }
                
                _sdl.FreeSurface(imageSurface);
                
                _textureData[_textureId] = textureInfo;
                _texturePointers[_textureId] = (IntPtr)imageTexture;
            }
        }

        return _textureId++;
    }

    public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
        RendererFlip flip = RendererFlip.None, double angle = 0.0, Point center = default)
    {
        if (_texturePointers.TryGetValue(textureId, out var imageTexture))
        {
            var translatedDst = _camera.ToScreenCoordinates(dst);
            _sdl.RenderCopyEx(_renderer, (Texture*)imageTexture, in src,
                in translatedDst,
                angle,
                in center, flip);
        }
    }

    public Vector2D<int> ToWorldCoordinates(int x, int y)
    {
        return _camera.ToWorldCoordinates(new Vector2D<int>(x, y));
    }

    public void SetDrawColor(byte r, byte g, byte b, byte a)
    {
        _sdl.SetRenderDrawColor(_renderer, r, g, b, a);
    }

    public void ClearScreen()
    {
        _sdl.RenderClear(_renderer);
    }

    public void PresentFrame()
    {
        _sdl.RenderPresent(_renderer);
    }
    
}
