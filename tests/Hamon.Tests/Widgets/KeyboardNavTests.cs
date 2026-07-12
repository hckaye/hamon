using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Exhaustive test of single keyboard operation: Tab linear movement, explicit Order, Enter launch, Esc cancel, all UI reached.</summary>
public class KeyboardNavTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 400);

    private static HamonRoot MountColumn(params FocusNode[] nodes)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() =>
        {
            var items = new Widget[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                items[i] = new Focus { Node = nodes[i], Child = new SizedBox { Width = Dimension.Px(180), Height = Dimension.Px(40) } };
            }

            return new Column { Children = items };
        });
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Tab_CyclesThroughInRegistrationOrder()
    {
        var a = new FocusNode();
        var b = new FocusNode();
        var c = new FocusNode();
        HamonRoot host = MountColumn(a, b, c);

        Assert.True(host.MoveNext());
        Assert.True(a.HasFocus);
        host.MoveNext();
        Assert.True(b.HasFocus);
        host.MoveNext();
        Assert.True(c.HasFocus);
        host.MoveNext(); // 巡回して先頭へ
        Assert.True(a.HasFocus);
    }

    [Fact]
    public void ShiftTab_GoesBackward_AndWraps()
    {
        var a = new FocusNode();
        var b = new FocusNode();
        HamonRoot host = MountColumn(a, b);

        host.MovePrevious(); // 未フォーカス→末尾
        Assert.True(b.HasFocus);
        host.MovePrevious();
        Assert.True(a.HasFocus);
        host.MovePrevious(); // 巡回して末尾
        Assert.True(b.HasFocus);
    }

    [Fact]
    public void ExplicitOrder_OverridesRegistrationOrder()
    {
        // 登録順 a,b,c だが Order で c→a→b の順に辿らせる。
        var a = new FocusNode { Order = 20 };
        var b = new FocusNode { Order = 30 };
        var c = new FocusNode { Order = 10 };
        HamonRoot host = MountColumn(a, b, c);

        host.MoveNext();
        Assert.True(c.HasFocus); // Order 最小
        host.MoveNext();
        Assert.True(a.HasFocus);
        host.MoveNext();
        Assert.True(b.HasFocus);
    }

    [Fact]
    public void Enter_Activates_Esc_Dismisses_FocusedControl()
    {
        int activated = 0;
        int dismissed = 0;
        var node = new FocusNode { OnActivate = () => activated++, OnDismiss = () => dismissed++ };
        HamonRoot host = MountColumn(node);
        host.MoveNext(); // フォーカス

        // 既定バインド：A=Activate, B=Dismiss（キーボードは Enter→A, Esc→B に写像する想定をエミュレート）。
        host.HandleButtonDown(GamepadButton.A);
        host.HandleButtonDown(GamepadButton.B);

        Assert.Equal(1, activated);
        Assert.Equal(1, dismissed);
    }

    [Fact]
    public void KeyboardOnly_CanReachEveryControl()
    {
        var nodes = new[] { new FocusNode(), new FocusNode(), new FocusNode(), new FocusNode() };
        HamonRoot host = MountColumn(nodes);

        var reached = new HashSet<FocusNode>();
        for (int i = 0; i < nodes.Length; i++)
        {
            host.MoveNext();
            reached.Add(host.Focus.Focused!);
        }

        Assert.Equal(nodes.Length, reached.Count); // 全コントロールに到達
    }
}
