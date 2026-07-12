using Hamon.Widgets;
using Microsoft.Xna.Framework.Graphics;

namespace Hamon.MonoGame;

/// <summary><see cref="ITexture"/>MonoGame implementation of (<see cref="Texture2D"/>thin trumpet). </summary>
public sealed class MonoGameTexture : ITexture
{
    public MonoGameTexture(Texture2D texture) => Texture = texture;

    public Texture2D Texture { get; }

    public int Width => Texture.Width;

    public int Height => Texture.Height;
}
