using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for focus (directional movement, OK/Cancel, autofocus) (GPU independent).</summary>
public class FocusTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    [Fact]
    public void MoveFocus_Right_PicksNearestInDirection()
    {
        var m = new FocusManager();
        var a = new FocusNode();
        var b = new FocusNode();
        var c = new FocusNode();
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.Register(b, () => new Rect(20, 0, 10, 10));
        m.Register(c, () => new Rect(40, 0, 10, 10));
        m.RequestFocus(a);

        Assert.True(m.MoveFocus(FocusDirection.Right));
        Assert.Same(b, m.Focused);
        Assert.True(m.MoveFocus(FocusDirection.Right));
        Assert.Same(c, m.Focused);
        Assert.False(m.MoveFocus(FocusDirection.Right)); // これ以上右は無い
    }

    [Fact]
    public void MoveFocus_ExplicitLink_OverridesNearest()
    {
        var m = new FocusManager();
        var a = new FocusNode { Id = 1, NavRight = 3 }; // 明示で id=3 へ（b を飛ばす）
        var b = new FocusNode { Id = 2 };               // 幾何的には b が最近傍
        var c = new FocusNode { Id = 3 };
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.Register(b, () => new Rect(20, 0, 10, 10));
        m.Register(c, () => new Rect(40, 0, 10, 10));
        m.RequestFocus(a);

        Assert.True(m.MoveFocus(FocusDirection.Right));
        Assert.Same(c, m.Focused);
    }

    [Fact]
    public void MoveFocus_NoExplicitLink_FallsBackToNearest()
    {
        var m = new FocusManager();
        var a = new FocusNode(); // 明示リンク無し → オート近傍
        var b = new FocusNode();
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.Register(b, () => new Rect(20, 0, 10, 10));
        m.RequestFocus(a);

        Assert.True(m.MoveFocus(FocusDirection.Right));
        Assert.Same(b, m.Focused);
    }

    [Fact]
    public void MoveFocus_Down_PicksNearestVertically()
    {
        var m = new FocusManager();
        var top = new FocusNode();
        var bottom = new FocusNode();
        m.Register(top, () => new Rect(0, 0, 10, 10));
        m.Register(bottom, () => new Rect(0, 30, 10, 10));
        m.RequestFocus(top);

        Assert.True(m.MoveFocus(FocusDirection.Down));
        Assert.Same(bottom, m.Focused);
    }

    [Fact]
    public void MoveFocus_FromNone_PicksFirst()
    {
        var m = new FocusManager();
        var a = new FocusNode();
        var b = new FocusNode();
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.Register(b, () => new Rect(20, 0, 10, 10));

        Assert.True(m.MoveFocus(FocusDirection.Right));
        Assert.Same(a, m.Focused);
    }

    [Fact]
    public void Activate_And_Dismiss_RouteToFocused()
    {
        var m = new FocusManager();
        bool activated = false;
        bool dismissed = false;
        var a = new FocusNode { OnActivate = () => activated = true, OnDismiss = () => dismissed = true };
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.RequestFocus(a);

        m.Activate();
        m.Dismiss();

        Assert.True(activated);
        Assert.True(dismissed);
    }

    [Fact]
    public void DispatchButton_RoutesArbitraryPhysicalButtonToFocused()
    {
        var m = new FocusManager();
        GamepadButton? down = null;
        GamepadButton? up = null;
        var a = new FocusNode { OnButtonDown = b => down = b, OnButtonUp = b => up = b };
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.RequestFocus(a);

        m.DispatchButtonDown(GamepadButton.RightShoulder);
        Assert.Equal(GamepadButton.RightShoulder, down);

        m.DispatchButtonUp(GamepadButton.Y);
        Assert.Equal(GamepadButton.Y, up);
    }

    [Fact]
    public void DispatchAnalog_RoutesTriggerAndStickToFocused()
    {
        var m = new FocusManager();
        float trigger = 0f;
        Vec2 stick = Vec2.Zero;
        var a = new FocusNode
        {
            OnTrigger = (side, v) => trigger = v,
            OnStick = (side, v) => stick = v,
        };
        m.Register(a, () => new Rect(0, 0, 10, 10));
        m.RequestFocus(a);

        m.DispatchTrigger(GamepadSide.Right, 0.75f);
        m.DispatchStick(GamepadSide.Left, new Vec2(0.5f, -0.5f));

        Assert.Equal(0.75f, trigger, 3);
        Assert.Equal(new Vec2(0.5f, -0.5f), stick);
    }

    [Fact]
    public void HandleButtonDown_DefaultBinding_MovesFocusOnDpad()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var left = new FocusNode();
        var right = new FocusNode();
        host.SetRoot(() => new Row
        {
            Spacing = 10f,
            Children = new Widget[]
            {
                new Focus { Node = left, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(20), Height = Dimension.Px(20) } },
                new Focus { Node = right, Child = new SizedBox { Width = Dimension.Px(20), Height = Dimension.Px(20) } },
            },
        });
        host.Update(new Size(200, 50));

        host.HandleButtonDown(GamepadButton.DpadRight); // 既定バインド=方向移動
        Assert.Same(right, host.Focus.Focused);
    }

    [Fact]
    public void HandleButtonDown_ActivateButton_FiresOnActivate()
    {
        var host = new HamonRoot(new StubTextRenderer());
        bool activated = false;
        var node = new FocusNode { OnActivate = () => activated = true };
        host.SetRoot(() => new Focus { Node = node, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) } });
        host.Update(new Size(100, 100));

        host.HandleButtonDown(GamepadButton.A); // 既定 Activate=A
        Assert.True(activated);
    }

    [Fact]
    public void RequestFocus_RaisesFocusChange()
    {
        var m = new FocusManager();
        bool? aState = null;
        var a = new FocusNode { OnFocusChange = v => aState = v };
        m.Register(a, () => new Rect(0, 0, 10, 10));

        m.RequestFocus(a);
        Assert.True(a.HasFocus);
        Assert.True(aState);
    }

    // テキスト編集中（OnEditKey を持つ＝TextField）は、左右はキャレット移動に充て、フォーカスは動かさない。
    // 上下は通常どおりフォーカス移動（欄から出られる）。
    [Fact]
    public void MoveFocus_TextEditor_LeftRight_MovesCaretNotFocus()
    {
        var m = new FocusManager();
        var keys = new List<TextEditKey>();
        var field = new FocusNode { OnEditKey = keys.Add };
        var right = new FocusNode();
        var below = new FocusNode();
        m.Register(field, () => new Rect(0, 0, 10, 10));
        m.Register(right, () => new Rect(20, 0, 10, 10)); // 右隣（編集中でなければここへ移る）
        m.Register(below, () => new Rect(0, 20, 10, 10)); // 真下
        m.RequestFocus(field);

        Assert.True(m.MoveFocus(FocusDirection.Right));
        Assert.Same(field, m.Focused); // フォーカスは動かない
        Assert.True(m.MoveFocus(FocusDirection.Left));
        Assert.Same(field, m.Focused);
        Assert.Equal(new[] { TextEditKey.Right, TextEditKey.Left }, keys); // キャレット移動へ充てられた

        Assert.True(m.MoveFocus(FocusDirection.Down)); // 上下は通常どおりフォーカス移動
        Assert.Same(below, m.Focused);
    }

    [Fact]
    public void Autofocus_ViaHamonRoot_FocusesNode()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var node = new FocusNode();
        host.SetRoot(() => new Focus
        {
            Node = node,
            Autofocus = true,
            Child = new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) },
        });

        host.Update(new Size(100, 100));

        Assert.Same(node, host.Focus.Focused);
        Assert.True(node.HasFocus);
    }

    [Fact]
    public void HamonRoot_MoveFocus_UsesLaidOutBounds()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var left = new FocusNode();
        var right = new FocusNode();
        host.SetRoot(() => new Row
        {
            Spacing = 10f,
            Children = new Widget[]
            {
                new Focus { Node = left, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(20), Height = Dimension.Px(20) } },
                new Focus { Node = right, Child = new SizedBox { Width = Dimension.Px(20), Height = Dimension.Px(20) } },
            },
        });

        host.Update(new Size(200, 50));
        Assert.Same(left, host.Focus.Focused);

        Assert.True(host.MoveFocus(FocusDirection.Right));
        Assert.Same(right, host.Focus.Focused);
    }
}
