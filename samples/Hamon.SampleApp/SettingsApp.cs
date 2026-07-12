using Hamon.Layout;
using Hamon.Widgets;
using System.Globalization;

namespace Hamon.SampleApp;

/// <summary>
/// Standard sample: Settings screen.<see cref="Switch"/>/<see cref="Slider"/>/<see cref="Checkbox"/>state (<c>UseState</c>),
/// In "Version information"<c>ShowDialog</c>(modal), with "action"<c>ShowBottomSheet</c>Open (bottom sheet).
/// The ≡ (hamburger) on the top bar is<see cref="Scaffold"/>via<c>ShowDrawer</c>Open (drawer).
/// </summary>
public sealed class SettingsApp : HookWidget
{
    private readonly HamonRoot _host;

    public SettingsApp(HamonRoot host) => _host = host;

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<bool> notifications = hooks.UseState(true);
        HookState<bool> autosave = hooks.UseState(false);
        HookState<float> volume = hooks.UseState(0.6f);

        Widget Setting(string label, Widget control) => new Container
        {
            Padding = EdgeInsets.Symmetric(4f, 10f),
            Child = new Row
            {
                MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Children = new Widget[]
                {
                    new Text(label) { FontSize = 17f, Color = theme.OnSurface },
                    control,
                },
            },
        };

        Widget ActionButton(string label, Color bg, Color fg, Action onPressed) => new Button
        {
            Node = new FocusNode(),
            Background = bg,
            Radius = theme.Radius,
            Padding = EdgeInsets.Symmetric(16f, 12f),
            OnPressed = onPressed,
            Child = new Text(label) { FontSize = 16f, Color = fg },
        };

        return new Stack
        {
            Fit = StackFit.Expand,
            Background = theme.Background,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(20f),
                    Top = Dimension.Px(20f),
                    Right = Dimension.Px(20f),
                    Bottom = Dimension.Px(20f),
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Stretch,
                        Spacing = 8f,
                        Children = new Widget[]
                        {
                                    Setting("通知", new Switch
                                    {
                                        Node = new FocusNode(),
                                        Autofocus = true,
                                        Value = notifications.Value,
                                        OnChanged = v => notifications.Value = v,
                                    }),
                                    Setting("自動セーブ", new Checkbox
                                    {
                                        Node = new FocusNode(),
                                        Value = autosave.Value,
                                        OnChanged = v => autosave.Value = v,
                                    }),
                                    Setting("音量", new Slider
                                    {
                                        Node = new FocusNode(),
                                        Value = volume.Value,
                                        Step = 0.1f,
                                        OnChanged = v => volume.Value = v,
                                    }),
                                    new Text($"音量 {(volume.Value * 100f).ToString("0", CultureInfo.InvariantCulture)}%")
                                    {
                                        FontSize = 13f,
                                        Color = theme.OnSurfaceVariant,
                                    },
                                    new SizedBox { Height = Dimension.Px(12f) },
                                    ActionButton("アクション（ボトムシート）", theme.SurfaceVariant, theme.OnSurface, OpenSheet),
                                    ActionButton("バージョン情報（ダイアログ）", theme.Primary, theme.OnPrimary, OpenAbout),
                        },
                    },
                },
            },
        };
    }

    private void OpenAbout() => _host.ShowDialog(close => new AboutCard(close));

    private void OpenSheet() => _host.ShowBottomSheet(
        close => new ActionSheet(close),
        height: 220f);
}

/// <summary>Contents of the "Version Information" dialog.</summary>
public sealed class AboutCard : StatelessWidget
{
    private readonly Action _close;

    public AboutCard(Action close) => _close = close;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        return new Container
        {
            Color = theme.Surface,
            Radius = theme.Radius,
            Padding = EdgeInsets.All(20f),
            Width = Dimension.Px(300f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                MainAxisSize = MainAxisSize.Min,
                Spacing = 12f,
                Children = new Widget[]
                {
                    new Text("Hamon SampleApp") { FontSize = 20f, Color = theme.OnSurface },
                    new Text("リアクティブ宣言的 UI ライブラリのデモ。") { FontSize = 14f, Color = theme.OnSurfaceVariant },
                    new Button
                    {
                        Node = new FocusNode(),
                        Autofocus = true,
                        Background = theme.Primary,
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(16f, 12f),
                        OnPressed = _close,
                        Child = new Text("OK") { FontSize = 16f, Color = theme.OnPrimary },
                    },
                },
            },
        };
    }
}

/// <summary>Contents of the "Action" bottom sheet.</summary>
public sealed class ActionSheet : StatelessWidget
{
    private readonly Action _close;

    public ActionSheet(Action close) => _close = close;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        Widget Item(string label, Color fg) => new Button
        {
            Node = new FocusNode(),
            Background = theme.SurfaceVariant,
            Radius = theme.Radius,
            Padding = EdgeInsets.Symmetric(16f, 12f),
            OnPressed = _close,
            Child = new Text(label) { FontSize = 16f, Color = fg },
        };

        return new Container
        {
            Color = theme.Surface,
            Padding = EdgeInsets.All(16f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                Spacing = 10f,
                Children = new Widget[]
                {
                    new Text("アクション") { FontSize = 18f, Color = theme.OnSurface },
                    Item("共有", theme.OnSurface),
                    Item("複製", theme.OnSurface),
                    Item("削除", theme.Danger),
                },
            },
        };
    }
}
