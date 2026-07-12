using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Standard modal helpers (the equivalent of Flutter's <c>showDialog</c> and
/// <c>showModalBottomSheet</c>), built on top of the transition-animation
/// infrastructure in <see cref="HamonRoot.PushOverlay(Func{Func{float}, Widget}, float, Curve?)"/>.
/// Works together with <see cref="FocusScope"/>. Each modal type has its own
/// enter/exit animation (scrim = fade, dialog = fade + scale up, sheet = slide in
/// from the bottom). The content builder receives a <c>close</c> action so it can
/// dismiss itself, and setting <c>barrierDismissible</c> also closes the modal when
/// the scrim is tapped.
/// </summary>
public static class Modals
{
    /// <summary>Opens a centered modal dialog. Close it via the returned <see cref="OverlayEntry"/>, or via the close action passed to the content builder.</summary>
    public static OverlayEntry ShowDialog(
        this HamonRoot root,
        Func<Action, Widget> content,
        float transitionDuration = 0.18f,
        bool barrierDismissible = true,
        Color? scrim = null)
    {
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is not null)
            {
                root.RemoveOverlay(entry);
            }
        }

        Color scrimColor = scrim ?? new Color(0, 0, 0, 150);
        entry = root.PushOverlay(
            progress => BuildDialog(content(Close), progress, scrimColor, barrierDismissible ? Close : null),
            transitionDuration);
        return entry;
    }

    /// <summary>Opens a bottom sheet that slides up from below. <paramref name="height"/> is the sheet's height in pixels along the vertical axis.</summary>
    public static OverlayEntry ShowBottomSheet(
        this HamonRoot root,
        Func<Action, Widget> content,
        float height,
        float transitionDuration = 0.22f,
        bool barrierDismissible = true,
        Color? scrim = null)
    {
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is not null)
            {
                root.RemoveOverlay(entry);
            }
        }

        Color scrimColor = scrim ?? new Color(0, 0, 0, 120);
        entry = root.PushOverlay(
            progress => BuildBottomSheet(content(Close), progress, scrimColor, height, barrierDismissible ? Close : null),
            transitionDuration);
        return entry;
    }

    /// <summary>Opens a navigation drawer that slides in from the side. Set <paramref name="fromRight"/> to true to slide in from the right edge instead of the left.</summary>
    public static OverlayEntry ShowDrawer(
        this HamonRoot root,
        Func<Action, Widget> content,
        float width,
        bool fromRight = false,
        float transitionDuration = 0.22f,
        bool barrierDismissible = true,
        Color? scrim = null)
    {
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is not null)
            {
                root.RemoveOverlay(entry);
            }
        }

        Color scrimColor = scrim ?? new Color(0, 0, 0, 120);
        entry = root.PushOverlay(
            progress => BuildDrawer(content(Close), progress, scrimColor, width, fromRight, barrierDismissible ? Close : null),
            transitionDuration);
        return entry;
    }

    private static Widget BuildDrawer(Widget panel, Func<float> progress, Color scrimColor, float width, bool fromRight, Action? onDismiss)
    {
        var positioned = new Positioned
        {
            Top = Dimension.Px(0),
            Bottom = Dimension.Px(0),
            Width = Dimension.Px(width),
            Left = fromRight ? default : Dimension.Px(0),
            Right = fromRight ? Dimension.Px(0) : default,
            Child = new Transform
            {
                // 端の外（左:-width / 右:+width）から定位置へ滑り込む。
                TranslateXGetter = () => (1f - progress()) * (fromRight ? width : -width),
                Child = new FocusScope { Child = panel },
            },
        };

        return new Stack
        {
            Fit = StackFit.Loose,
            Children = new Widget[]
            {
                Fill(new Opacity { ValueGetter = progress, Child = Scrim(scrimColor, onDismiss) }),
                positioned,
            },
        };
    }

    private static Widget BuildDialog(Widget card, Func<float> progress, Color scrimColor, Action? onDismiss) => new Stack
    {
        Fit = StackFit.Loose,
        Alignment = Alignment.Center,
        Children = new Widget[]
        {
            Fill(new Opacity { ValueGetter = progress, Child = Scrim(scrimColor, onDismiss) }),
            new Opacity
            {
                ValueGetter = progress,
                Child = new Transform
                {
                    ScaleGetter = () => 0.9f + (0.1f * progress()),
                    Origin = Alignment.Center,
                    Child = new FocusScope { Child = card },
                },
            },
        },
    };

    private static Widget BuildBottomSheet(Widget sheet, Func<float> progress, Color scrimColor, float height, Action? onDismiss) => new Stack
    {
        Fit = StackFit.Loose,
        Children = new Widget[]
        {
            Fill(new Opacity { ValueGetter = progress, Child = Scrim(scrimColor, onDismiss) }),
            new Positioned
            {
                Left = Dimension.Px(0),
                Right = Dimension.Px(0),
                Bottom = Dimension.Px(0),
                Height = Dimension.Px(height),
                Child = new Transform
                {
                    TranslateYGetter = () => (1f - progress()) * height, // 下から迫り上がる
                    Child = new FocusScope { Child = sheet },
                },
            },
        },
    };

    private static Widget Fill(Widget child) => new Positioned
    {
        Left = Dimension.Px(0),
        Top = Dimension.Px(0),
        Right = Dimension.Px(0),
        Bottom = Dimension.Px(0),
        Child = child,
    };

    // スクリムは常にポインタを吸収する（背後のUIへ入力を漏らさない＝モーダル性）。閉じるのは onDismiss 設定時のみ。
    private static Widget Scrim(Color color, Action? onDismiss) => new GestureDetector
    {
        OnTap = onDismiss,
        Child = new Container { Color = color },
    };
}
