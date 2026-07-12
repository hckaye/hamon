using Hamon.Layout;
using Hamon.MonoGame;
using Hamon.Widgets;

// Hamon だけで作る最小アプリ。配線はゼロ：ルート Widget を渡して Run() するだけ。
// フォントは自動解決（OS システムフォント等）。明示したいときは:
//   new HamonGame(new CounterApp(), new HamonAppOptions { FontPath = "MyFont.ttf" })
// ダークにしたいときは: new HamonAppOptions { DarkTheme = HamonTheme.Dark, ThemeMode = ThemeMode.Dark }
using var game = new HamonGame(new CounterApp());
game.Run();

/// <summary>A small screen that counts in +/-. </summary>
public sealed class CounterApp : HookWidget
{
    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HookState<int> count = hooks.UseState(0);
        HamonTheme t = context.Theme;

        return new Center
        {
            Child = new Material
            {
                Color = t.Surface,
                Radius = t.Radius,
                Elevation = 3f,
                Child = new Container
                {
                    Padding = EdgeInsets.All(t.SpacingL),
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Center,
                        Spacing = t.SpacingM,
                        Children = new Widget[]
                        {
                            new Text("Hamon 波紋") { FontSize = t.TextHeadline, Color = t.OnSurface },
                            new Text($"Count: {count.Value}") { FontSize = t.TextTitle, Color = t.Primary },
                            new Row
                            {
                                Spacing = t.SpacingS,
                                Children = new Widget[]
                                {
                                    new Button
                                    {
                                        Node = new FocusNode(),
                                        Background = t.SurfaceVariant,
                                        Radius = t.Radius,
                                        Padding = EdgeInsets.Symmetric(22f, 12f),
                                        OnPressed = () => count.Value--,
                                        Child = new Text("-1") { FontSize = t.TextBody, Color = t.OnSurface },
                                    },
                                    new Button
                                    {
                                        Node = new FocusNode(),
                                        Autofocus = true,
                                        Background = t.Primary,
                                        Radius = t.Radius,
                                        Padding = EdgeInsets.Symmetric(22f, 12f),
                                        OnPressed = () => count.Value++,
                                        Child = new Text("+1") { FontSize = t.TextBody, Color = t.OnPrimary },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
    }
}
