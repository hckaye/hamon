using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>An <see cref="ITicker"/> that fires a callback once after a delay (used to auto-dismiss snackbars and toasts).</summary>
internal sealed class DelayTimer : ITicker
{
    private readonly Action _onElapsed;
    private float _remaining;
    private bool _fired;

    public DelayTimer(float seconds, Action onElapsed)
    {
        _remaining = seconds;
        _onElapsed = onElapsed;
    }

    public bool Tick(float dtSeconds)
    {
        if (_fired)
        {
            return false;
        }

        _remaining -= dtSeconds;
        if (_remaining <= 0f)
        {
            _fired = true;
            _onElapsed();
            return false; // 自動で登録解除
        }

        return true;
    }
}

/// <summary>Extension methods on <see cref="HamonRoot"/> for showing snackbars and toasts (transient notifications).</summary>
public static class Notifications
{
    /// <summary>
    /// Displays a snackbar at the bottom of the screen (the equivalent of Flutter's
    /// <c>ScaffoldMessenger.showSnackBar</c>). It disappears automatically after
    /// <paramref name="seconds"/>. A single action can be added via
    /// <paramref name="actionLabel"/> and <paramref name="onAction"/>. Passing the
    /// returned <see cref="OverlayEntry"/> to <see cref="HamonRoot.RemoveOverlay"/>
    /// removes it immediately.
    /// </summary>
    public static OverlayEntry ShowSnackbar(this HamonRoot root, string message, float seconds = 3f, string? actionLabel = null, Action? onAction = null)
    {
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is not null)
            {
                root.RemoveOverlay(entry);
            }
        }

        entry = root.PushOverlay(progress => BuildSnackbar(root.Theme, message, progress, actionLabel, onAction, Close), 0.25f, Curves.FastOutSlowIn);
        root.RegisterTicker(new DelayTimer(seconds, Close));
        return entry;
    }

    /// <summary>Displays a short, non-interactive toast at the bottom center of the screen. It disappears automatically after <paramref name="seconds"/>.</summary>
    public static OverlayEntry ShowToast(this HamonRoot root, string message, float seconds = 2f)
    {
        OverlayEntry? entry = null;
        entry = root.PushOverlay(progress => BuildToast(root.Theme, message, progress), 0.2f, Curves.FastOutSlowIn);
        root.RegisterTicker(new DelayTimer(seconds, () =>
        {
            if (entry is not null)
            {
                root.RemoveOverlay(entry);
            }
        }));
        return entry;
    }

    private static Widget BuildSnackbar(HamonTheme theme, string message, Func<float> progress, string? actionLabel, Action? onAction, Action close)
    {
        var rowChildren = new List<Widget>(2)
        {
            new Expanded { Child = new Text(message) { FontSize = theme.TextBody, Color = theme.OnSurface } },
        };

        if (actionLabel is not null)
        {
            rowChildren.Add(new Button
            {
                Node = new FocusNode(),
                Background = new Color(0, 0, 0, 0),
                Radius = theme.Radius,
                Padding = EdgeInsets.Symmetric(theme.SpacingM, theme.SpacingS),
                OnPressed = () =>
                {
                    onAction?.Invoke();
                    close();
                },
                Child = new Text(actionLabel) { FontSize = theme.TextLabel, Color = theme.Primary },
            });
        }

        Widget card = new Material
        {
            Color = theme.SurfaceVariant,
            Radius = theme.Radius,
            Elevation = 4f,
            Child = new Container
            {
                Padding = EdgeInsets.Symmetric(theme.SpacingM, theme.SpacingM),
                Child = new Row { CrossAxisAlignment = CrossAxisAlignment.Center, Children = rowChildren },
            },
        };

        return Anchored(card, progress);
    }

    private static Widget BuildToast(HamonTheme theme, string message, Func<float> progress)
    {
        Widget pill = new Material
        {
            Color = theme.SurfaceVariant, // 不透明（半透明だと背後の影が透けて角がもやる）
            Radius = 20f,
            Elevation = 3f,
            Child = new Container
            {
                Padding = EdgeInsets.Symmetric(theme.SpacingL, theme.SpacingS),
                Child = new Text(message) { FontSize = theme.TextLabel, Color = theme.OnSurface },
            },
        };

        return new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f),
                    Right = Dimension.Px(0f),
                    Bottom = Dimension.Px(64f),
                    Child = new Opacity { ValueGetter = progress, Child = new Align { Alignment = Alignment.BottomCenter, Child = pill } },
                },
            },
        };
    }

    // 下部に固定し、下からスライドイン＋フェードで出す共通ラッパ。
    private static Widget Anchored(Widget card, Func<float> progress) => new Stack
    {
        Fit = StackFit.Expand,
        Children = new Widget[]
        {
            new Positioned
            {
                Left = Dimension.Px(16f),
                Right = Dimension.Px(16f),
                Bottom = Dimension.Px(16f),
                Child = new Opacity
                {
                    ValueGetter = progress,
                    Child = new Transform
                    {
                        Origin = Alignment.BottomCenter,
                        TranslateYGetter = () => (1f - progress()) * 32f,
                        Child = card,
                    },
                },
            },
        },
    };
}
