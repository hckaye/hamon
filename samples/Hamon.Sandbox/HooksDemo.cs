using Hamon.Layout;
using Hamon.Widgets;
using Microsoft.Xna.Framework;

namespace Hamon.Sandbox;

/// <summary>
/// Hook writing demo.<c>UseState</c>counter (+1 automatically rebuilds only this subtree),<c>UseFocusNode</c>in
/// Persist focus without external members.
/// </summary>
public sealed class HooksDemo : HookWidget
{
    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HookState<int> count = hooks.UseState(0);
        FocusNode node = hooks.UseFocusNode(); // 外部に持たなくても再構築で消えない

        return new Container
        {
            Color = HamonTheme.Dark.Background,
            Padding = EdgeInsets.All(24f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Start,
                Spacing = 16f,
                Children = new Widget[]
                {
                    new Text("Hooks（UseState＋UseFocusNode・自動部分再構築）") { FontSize = 22f, Color = HamonTheme.Dark.OnSurface },
                    new Text($"カウント: {count.Value}") { FontSize = 32f, Color = HamonTheme.Dark.Primary },
                    new Button
                    {
                        Node = node,
                        Autofocus = true,
                        Background = HamonTheme.Dark.Primary,
                        Padding = EdgeInsets.Symmetric(20f, 12f),
                        OnPressed = () => count.Value++, // Invalidate 不要：この HookWidget だけ再構築
                        Child = new Text("+1") { FontSize = 18f, Color = HamonTheme.Dark.OnPrimary },
                    },
                },
            },
        };
    }
}
