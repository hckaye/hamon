using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// An app bar at the top of the screen (the equivalent of Flutter's <c>AppBar</c>).
/// Has a fixed height. <see cref="Title"/> is a plain string; use
/// <see cref="TitleWidget"/> for a custom title widget, which takes priority over
/// <see cref="Title"/> when set.
/// </summary>
public sealed class AppBar : StatelessWidget
{
    public Widget? Leading { get; init; }

    public string? Title { get; init; }

    public Widget? TitleWidget { get; init; }

    public IReadOnlyList<Widget>? Actions { get; init; }

    public Color? Background { get; init; }

    public Color? ForegroundColor { get; init; }

    public float Height { get; init; } = 56f;

    public float Elevation { get; init; } = 3f;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        Color fg = ForegroundColor ?? theme.OnSurface;

        var children = new List<Widget>(4);
        if (Leading is not null)
        {
            children.Add(Leading);
            children.Add(new SizedBox { Width = Dimension.Px(theme.SpacingS) });
        }

        Widget title = TitleWidget ?? new Text(Title ?? string.Empty) { FontSize = theme.TextTitle, Color = fg };
        children.Add(new Expanded { Child = new Align { Alignment = Alignment.CenterLeft, Child = title } });

        if (Actions is not null)
        {
            for (int i = 0; i < Actions.Count; i++)
            {
                children.Add(Actions[i]);
            }
        }

        return new Material
        {
            Color = Background ?? theme.Surface,
            Elevation = Elevation,
            Child = new Container
            {
                Height = Dimension.Px(Height),
                Padding = EdgeInsets.Symmetric(theme.SpacingM, 0f),
                Alignment = Alignment.CenterLeft,
                Child = new Row { CrossAxisAlignment = CrossAxisAlignment.Center, Children = children },
            },
        };
    }
}

/// <summary>One item in the bottom navigation bar: an icon (a glyph character or arbitrary widget) plus a label.</summary>
public sealed class NavigationDestination
{
    public required string Label { get; init; }

    /// <summary>Glyph or emoji character for the icon (optional). Ignored if <see cref="IconWidget"/> is set.</summary>
    public string? Icon { get; init; }

    public Widget? IconWidget { get; init; }
}

/// <summary>
/// A bottom navigation bar (the equivalent of Flutter's <c>NavigationBar</c> /
/// <c>BottomNavigationBar</c>). The currently selected item is emphasized using
/// <see cref="HamonTheme.Primary"/>. Selection changes are reported via
/// <see cref="OnDestinationSelected"/>.
/// </summary>
public sealed class NavigationBar : StatelessWidget
{
    public required IReadOnlyList<NavigationDestination> Destinations { get; init; }

    public int SelectedIndex { get; init; }

    public Action<int>? OnDestinationSelected { get; init; }

    public Color? Background { get; init; }

    public float Height { get; init; } = 64f;

    public float Elevation { get; init; } = 3f;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        var items = new Widget[Destinations.Count];
        for (int i = 0; i < Destinations.Count; i++)
        {
            NavigationDestination d = Destinations[i];
            bool selected = i == SelectedIndex;
            Color tint = selected ? theme.Primary : theme.OnSurfaceVariant;
            int index = i;

            Widget icon = d.IconWidget ?? new Text(d.Icon ?? string.Empty) { FontSize = 20f, Color = tint };
            items[i] = new Expanded
            {
                Child = new Button
                {
                    Node = new FocusNode(),
                    Background = new Color(0, 0, 0, 0), // 透明＝下の Material 面が見える
                    Radius = theme.Radius,
                    Padding = EdgeInsets.Symmetric(theme.SpacingXs, theme.SpacingS),
                    OnPressed = () => OnDestinationSelected?.Invoke(index),
                    Child = new Column
                    {
                        MainAxisSize = MainAxisSize.Min,
                        CrossAxisAlignment = CrossAxisAlignment.Center,
                        Spacing = 2f,
                        Children = new Widget[]
                        {
                            icon,
                            new Text(d.Label) { FontSize = theme.TextCaption, Color = tint },
                        },
                    },
                },
            };
        }

        return new Material
        {
            Color = Background ?? theme.Surface,
            Elevation = Elevation,
            Child = new Container
            {
                Height = Dimension.Px(Height),
                Padding = EdgeInsets.Symmetric(theme.SpacingS, theme.SpacingXs),
                Child = new Row { CrossAxisAlignment = CrossAxisAlignment.Center, Children = items },
            },
        };
    }
}

/// <summary>
/// A screen skeleton (the equivalent of Flutter's <c>Scaffold</c>). Stacks
/// <see cref="AppBar"/> (top), <see cref="Body"/> (expanded to fill the remaining
/// space), and <see cref="BottomNavigationBar"/> (bottom) vertically.
/// <see cref="Background"/> falls back to the theme's background if unspecified.
/// </summary>
public sealed class Scaffold : StatelessWidget
{
    public Widget? AppBar { get; init; }

    public Widget? Body { get; init; }

    public Widget? BottomNavigationBar { get; init; }

    public Color? Background { get; init; }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        var column = new List<Widget>(3);
        if (AppBar is not null)
        {
            column.Add(AppBar);
        }

        column.Add(new Expanded { Child = Body ?? new SizedBox() });

        if (BottomNavigationBar is not null)
        {
            column.Add(BottomNavigationBar);
        }

        return new Stack
        {
            Fit = StackFit.Expand,
            Background = Background ?? theme.Background,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f),
                    Top = Dimension.Px(0f),
                    Right = Dimension.Px(0f),
                    Bottom = Dimension.Px(0f),
                    Child = new Column { CrossAxisAlignment = CrossAxisAlignment.Stretch, Children = column },
                },
            },
        };
    }
}
