using Hamon.Layout;
using Hamon.MonoGame;
using Hamon.Widgets;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Hamon.SampleApp;

/// <summary>
/// Standard sample: Image gallery.<b><see cref="GridView"/>(2D virtualization)+<see cref="Image"/></b>Arrange the thumbnails with
/// with a tap<c>ShowDialog</c>(modally). <see cref="CreateTextures"/>）。
/// </summary>
public sealed class GalleryApp : StatelessWidget
{
    private readonly HamonRoot _host;
    private readonly ITexture[] _textures;

    public GalleryApp(HamonRoot host, ITexture[] textures)
    {
        _host = host;
        _textures = textures;
    }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;

        Widget Cell(int index) => new Button
        {
            Node = new FocusNode(),
            Autofocus = index == 0,
            Background = theme.SurfaceVariant,
            Radius = theme.Radius,
            OnPressed = () => OpenViewer(index),
            Child = new Image
            {
                Texture = _textures[index],
                Width = Dimension.Percent(100f),
                Height = Dimension.Percent(100f),
                Fit = BoxFit.Cover,
            },
        };

        return new Stack
        {
            Fit = StackFit.Expand,
            Background = theme.Background,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(12f),
                    Top = Dimension.Px(12f),
                    Right = Dimension.Px(12f),
                    Bottom = Dimension.Px(12f),
                    Child = new GridView
                    {
                        ItemCount = _textures.Length,
                        CrossAxisCount = 3,
                        MainAxisExtent = 140f, // セル高を固定（行間が空かないように）
                        CrossAxisSpacing = 10f,
                        MainAxisSpacing = 10f,
                        Builder = Cell,
                    },
                },
            },
        };
    }

    private void OpenViewer(int index) => _host.ShowDialog(close => new ViewerCard(_textures[index], close));

    /// <summary>3072 color gradation tiles (dummy images for gallery).</summary>
    public static ITexture[] CreateTextures(GraphicsDevice device)
    {
        const int count = 12;
        const int size = 96;
        var result = new ITexture[count];
        for (int n = 0; n < count; n++)
        {
            var tex = new Texture2D(device, size, size);
            var data = new XnaColor[size * size];
            int r0 = 40 + (n * 17 % 200);
            int g0 = 60 + (n * 53 % 180);
            int b0 = 90 + (n * 31 % 160);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    data[(y * size) + x] = new XnaColor(
                        Clamp(r0 + (x * 2) - y),
                        Clamp(g0 + (y * 2) - x),
                        Clamp(b0 + x + y));
                }
            }

            tex.SetData(data);
            result[n] = new MonoGameTexture(tex);
        }

        return result;
    }

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
}

/// <summary>Modal expanded view of the gallery (dialog contents).</summary>
public sealed class ViewerCard : StatelessWidget
{
    private readonly ITexture _texture;
    private readonly Action _close;

    public ViewerCard(ITexture texture, Action close)
    {
        _texture = texture;
        _close = close;
    }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        return new Container
        {
            Color = theme.Surface,
            Radius = theme.Radius,
            Padding = EdgeInsets.All(16f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Center,
                MainAxisSize = MainAxisSize.Min,
                Spacing = 14f,
                Children = new Widget[]
                {
                    new Image
                    {
                        Texture = _texture,
                        Width = Dimension.Px(260f),
                        Height = Dimension.Px(260f),
                        Fit = BoxFit.Contain,
                    },
                    new Button
                    {
                        Node = new FocusNode(),
                        Autofocus = true,
                        Background = theme.Primary,
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(20f, 12f),
                        OnPressed = _close,
                        Child = new Text("閉じる") { FontSize = 16f, Color = theme.OnPrimary },
                    },
                },
            },
        };
    }
}
