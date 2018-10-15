// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-framework/master/LICENCE

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using Image = SixLabors.ImageSharp.Image;

namespace osu.Framework.Graphics.Sprites
{
    public class Svg : Drawable
    {
        private SvgDocument document;
        private Texture displayedTexture;
        private Texture pendingTexture;
        private Size textureSize;
        private Shader textureShader;
        private Shader roundedTextureShader;

        private readonly object textureLock = new object();

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders) {
            textureShader = shaders?.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            roundedTextureShader = shaders?.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE_ROUNDED);
            document = SvgDocument.Open(@"sample.svg");
            rasterize();
        }

        private void rasterize() => rasterize(
            (int)Math.Max(
                (ScreenSpaceDrawQuad.TopLeft - ScreenSpaceDrawQuad.TopRight).Length,
                (ScreenSpaceDrawQuad.BottomLeft - ScreenSpaceDrawQuad.BottomRight).Length),
            (int)Math.Max(
                (ScreenSpaceDrawQuad.TopLeft - ScreenSpaceDrawQuad.BottomLeft).Length,
                (ScreenSpaceDrawQuad.TopRight - ScreenSpaceDrawQuad.BottomRight).Length));

        private void rasterize(int width, int height) {
            // no need to rasterize at the same resolution twice
            if(width == textureSize.Width && height == textureSize.Height) return;
            Console.WriteLine($"Rastering svg at dimensions {width}x{height}");

            var bitmap = document.Draw(width, height);
            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[data.Height * data.Stride];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var image = Image.LoadPixelData<Bgra32>(pixels, width, height);
            bitmap.UnlockBits(data);

            var tex = new Texture(width, height);
            tex.SetData(new TextureUpload(image.CloneAs<Rgba32>()));
            uploadTexture(tex, width, height);
        }

        private void uploadTexture(Texture texture, int width, int height) {
            lock(textureLock) {
                updateDisplayedTexture();
                pendingTexture?.Dispose();
                pendingTexture = texture;
                textureSize = new Size(width, height);
            }
        }

        private void updateDisplayedTexture() {
            lock(textureLock) {
                if(pendingTexture != null && pendingTexture.Available) {
                    displayedTexture?.Dispose();
                    displayedTexture = pendingTexture;
                    pendingTexture = null;
                }
            }
        }

        protected override DrawNode CreateDrawNode() => new SpriteDrawNode();

        protected override void ApplyDrawNode(DrawNode node) {
            updateDisplayedTexture();
            SpriteDrawNode n = (SpriteDrawNode)node;

            n.ScreenSpaceDrawQuad = ScreenSpaceDrawQuad;
            n.DrawRectangle = DrawRectangle;
            n.Texture = displayedTexture;

            n.TextureShader = textureShader;
            n.RoundedTextureShader = roundedTextureShader;

            base.ApplyDrawNode(node);
        }

        protected override void Update() {
            rasterize();
            Invalidate(Invalidation.DrawNode);
        }
    }
}
