﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Glade2d.Graphics.SelfRenderer.BufferTransferring;
using Glade2d.Profiling;
using Glade2d.Services;
using Meadow.Foundation;
using Meadow.Foundation.Graphics;
using Meadow.Foundation.Graphics.Buffers;

namespace Glade2d.Graphics.SelfRenderer
{
    /// <summary>
    /// Glade renderer which translates scene data into pixels on a frame buffer itself.
    /// </summary>
    public class GladeSelfRenderer : IRenderer
    {
        internal const int BytesPerPixel = 2;
        private readonly TextureManager _textureManager;
        private readonly LayerManager _layerManager;
        private readonly Layer _spriteLayer;
        private readonly Profiler _profiler;
        private readonly IBufferTransferrer _bufferTransferrer;
        private readonly IPixelBuffer _pixelBuffer;
        private readonly IGraphicsDisplay _display;

        public int Height { get; }

        public Color BackgroundColor
        {
            get => _spriteLayer.BackgroundColor;
            set => _spriteLayer.BackgroundColor = value;
        }

        public bool ShowPerf { get; set; }
        public RotationType Rotation { get; set; }
        public int Width { get; }
        public int Scale { get; }
        public IFont CurrentFont { get; set; }

        public GladeSelfRenderer(IGraphicsDisplay display, 
            TextureManager textureManager,
            LayerManager layerManager,
            Profiler profiler,
            int scale = 1,
            RotationType rotation = RotationType.Default) 
        {
            if (display.PixelBuffer.ColorMode != ColorMode.Format16bppRgb565)
            {
                var message = $"Only color mode rgb565 is supported, but {display.PixelBuffer.ColorMode} " +
                              "was given.";

                throw new InvalidOperationException(message);
            }

            _display = display;
            _layerManager = layerManager;
            _textureManager = textureManager;
            _profiler = profiler;
            Scale = scale;
            Rotation = rotation;
        
            // If we are rendering at a different resolution than our
            // device, or we are rotating our display, we need to create
            // a new buffer as our primary drawing buffer so we draw at the
            // scaled resolution and rotate the final render
            if (scale > 1 || Rotation != RotationType.Default) 
            {
                var scaledWidth = display.Width / scale;
                var scaledHeight = display.Height / scale;
                
                // If we are rotated 90 or 270 degrees, we need to swap width and height
                var swapHeightAndWidth = Rotation is RotationType._90Degrees or RotationType._270Degrees;
                if (swapHeightAndWidth)
                {
                    (scaledWidth, scaledHeight) = (scaledHeight, scaledWidth);
                }

                Width = scaledWidth;
                Height = scaledHeight;

                _pixelBuffer = new BufferRgb565(Width, Height);
                LogService.Log.Trace($"Initialized renderer with custom buffer: {scaledWidth}x{scaledHeight}");
            }
            else
            {
                _pixelBuffer = display.PixelBuffer;
                LogService.Log.Trace($"Initialized renderer using default display driver buffer: {display.Width}x{display.Height}");
            }

            CurrentFont = new Font4x6();

            _spriteLayer = Layer.FromExistingBuffer((BufferRgb565)_pixelBuffer, textureManager);

            _bufferTransferrer = rotation switch
            {
                RotationType.Default => new NoRotationBufferTransferrer(),
                RotationType._90Degrees => new Rotation90BufferTransferrer(),
                RotationType._180Degrees => new Rotation180BufferTransferrer(),
                RotationType._270Degrees => new Rotation270BufferTransferrer(),
                _ => throw new NotImplementedException($"Rotation type of {rotation} not implemented"),
            };
        }

        public void Reset()
        {
            _pixelBuffer.Fill(BackgroundColor);
            _spriteLayer.Clear();
            
            // Should we be clearing the display buffer too???
        }

        /// <summary>
        /// Renders the current scene
        /// </summary>
        public ValueTask RenderAsync(List<Sprite> sprites)
        {
            Reset();
            
            _profiler.StartTiming("Renderer.Render");
            _profiler.StartTiming("LayerManager.RenderBackgroundLayers");

            var backgroundLayerEnumerator = _layerManager.BackgroundLayerEnumerator();
            while (backgroundLayerEnumerator.MoveNext())
            {
                var layer = (Layer)backgroundLayerEnumerator.Current;
                layer!.RenderToBuffer((BufferRgb565)_pixelBuffer);
            }
            
            _profiler.StopTiming("LayerManager.RenderBackgroundLayers");

            _profiler.StartTiming("Renderer.DrawSprites");
            if (sprites != null)
            {
                foreach (var sprite in sprites)
                {
                    // Use direct indexing instead of foreach for performance
                    // due to IEnumerable allocations.
                    DrawSprite(sprite);
                }
            }

            _profiler.StopTiming("Renderer.DrawSprites");
            
            _profiler.StartTiming("LayerManager.RenderForegroundLayers");

            var foregroundLayerEnumerator = _layerManager.ForegroundLayerEnumerator();
            while (foregroundLayerEnumerator.MoveNext())
            {
                var layer = (Layer)foregroundLayerEnumerator.Current;
                layer!.RenderToBuffer((BufferRgb565)_pixelBuffer);
            }
            
            _profiler.StopTiming("LayerManager.RenderForegroundLayers");
           
            _profiler.StartTiming("Renderer.RenderToDisplay");
            RenderToDisplay();
            _profiler.StopTiming("Renderer.RenderToDisplay");
            _profiler.StartTiming("Renderer.Render");

            return new ValueTask();
        }

        private void Show()
        {
            GameService.Instance.GameInstance.Profiler.StartTiming("Renderer.Show");
            if (_pixelBuffer != _display.PixelBuffer || Rotation != RotationType.Default)
            {
                GameService.Instance.GameInstance.Profiler.StartTiming("");
                var sourceBuffer = (BufferRgb565)_pixelBuffer;
                var targetBuffer = (BufferRgb565)_display.PixelBuffer;
                _bufferTransferrer.Transfer(sourceBuffer, targetBuffer, Scale);
                GameService.Instance.GameInstance.Profiler.StopTiming("BufferTransferrer.Transfer");
            }

            GameService.Instance.GameInstance.Profiler.StartTiming("Display.Show");
            _display.Show();
            GameService.Instance.GameInstance.Profiler.StopTiming("Display.Show");
            GameService.Instance.GameInstance.Profiler.StopTiming("Renderer.Show");
        }

        /// <summary>
        /// Creates a new Layer with the specified dimensions
        /// </summary>
        public ILayer CreateLayer(Dimensions dimensions)
        {
            var layerBuffer = new BufferRgb565(dimensions.Width, dimensions.Height);
            return new Layer(layerBuffer, GameService.Instance.GameInstance.TextureManager);
        }

        /// <summary>
        /// Renders the contents of the internal buffer to the driver buffer and
        /// then blits the driver buffer to the device
        /// </summary>
        private void RenderToDisplay()
        {
            // draw the FPS counter
            if (ShowPerf)
            {
                // TODO: Re-enable now that micrographics support is out
                // DrawRectangle(0, 0, Width, CurrentFont.Height, Color.Black, true);
                // DrawText(0, 0, $"{GameService.Instance.Time.FPS:n1}fps", Color.White);
            }

            // send the driver buffer to device
            Show();
        }
        
        /// <summary>
        /// Draws a sprite's CurrentFrame into the graphics buffer
        /// </summary>
        /// <param name="sprite">The sprite to draw</param>
        private void DrawSprite(Sprite sprite)
        {
            if (sprite.CurrentFrame != null)
            {
                var spriteOrigin = new Point((int)sprite.X, (int)sprite.Y);
                var textureOrigin = new Point(sprite.CurrentFrame.X, sprite.CurrentFrame.Y);
                var dimensions = new Dimensions(sprite.CurrentFrame.Width, sprite.CurrentFrame.Height);

                var texture = _textureManager.GetTexture(sprite.CurrentFrame.TextureName);
                _spriteLayer.DrawTexture(texture, textureOrigin, spriteOrigin, dimensions);
            }
        }
    }
}