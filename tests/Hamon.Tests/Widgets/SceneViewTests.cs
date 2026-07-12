using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Determinism tests for SceneView (layout filling, pointer transfer, Focusable).
/// Since a GraphicsDevice is required, check the PNG visually in Sandbox.
/// </summary>
public class SceneViewTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class RecordingTextRenderer : ITextRenderer
    {
        public string? LastText { get; private set; }

        public float LastPixelSize { get; private set; }

        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
            LastText = text;
            LastPixelSize = pixelSize;
        }
    }

    private sealed class NullPainter : IPainter
    {
        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color)
        {
        }

        public void FillRoundedRect(Rect rect, Color color, float radius)
        {
        }

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    // SceneDrawContext がテキストレンダラを運び、ctx.DrawText がそれへ到達する（シーン内の world 空間テキスト＝
    // フローティングダメージ等の前提）。Painter はテキストを持たないため、この経路が無いとシーンに文字を描けない。
    [Fact]
    public void Paint_ThreadsTextRendererIntoScene_AndDrawTextReachesRenderer()
    {
        var text = new RecordingTextRenderer();
        var host = new HamonRoot(text);
        ITextRenderer? seen = null;
        host.SetRoot(() => new SceneView
        {
            OnDraw = ctx =>
            {
                seen = ctx.Text;
                ctx.DrawText("42", new Vec2(10, 20), 12f, Color.White);
            },
        });
        host.Update(new Size(200, 200));
        host.Render(new NullPainter());

        Assert.Same(text, seen);
        Assert.Equal("42", text.LastText);
        Assert.Equal(12f, text.LastPixelSize); // Scale=1 ＝論理ptそのまま渡る
    }

    [Fact]
    public void FillsAvailable_WhenSizeAuto()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SceneView());
        host.Update(new Size(320, 180));

        Rect bounds = host.Root!.LayoutNode.Bounds;
        Assert.Equal(320f, bounds.Width);
        Assert.Equal(180f, bounds.Height);
    }

    [Fact]
    public void UsesExplicitSize_WhenGiven()
    {
        // ルート直下は Tight 制約で上書きされるため、Column の子（緩い制約）として検証する。
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                new SceneView { Width = Dimension.Px(200), Height = Dimension.Px(120) },
            },
        });
        host.Update(new Size(400, 400));

        Rect bounds = host.Root!.LayoutNode.Children[0].Bounds;
        Assert.Equal(200f, bounds.Width);
        Assert.Equal(120f, bounds.Height);
    }

    [Fact]
    public void Pointer_ForwardedIntoScene_WhenOnPointerSet()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int received = 0;
        PointerPhase lastPhase = PointerPhase.Move;
        host.SetRoot(() => new SceneView
        {
            OnPointer = p => { received++; lastPhase = p.Phase; },
        });
        host.Update(new Size(200, 200));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));

        Assert.Equal(1, received);
        Assert.Equal(PointerPhase.Down, lastPhase);
    }

    [Fact]
    public void NoPointer_NotHitTested_WhenOnPointerNull()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SceneView());
        host.Update(new Size(200, 200));

        // WantsPointer=false なので捕捉されない（例外を投げず素通り）
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));
        Assert.Null(host.Focus.Focused);
    }

    [Fact]
    public void Focusable_RegistersAndAutofocuses()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var node = new FocusNode();
        host.SetRoot(() => new SceneView { Focusable = true, Autofocus = true, Node = node });
        host.Update(new Size(200, 200));

        Assert.Same(node, host.Focus.Focused);
        Assert.True(node.HasFocus);
    }

    [Fact]
    public void Focusable_TapFocusesScene()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var node = new FocusNode();
        host.SetRoot(() => new SceneView { Focusable = true, Node = node, OnPointer = _ => { } });
        host.Update(new Size(200, 200));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));

        Assert.Same(node, host.Focus.Focused);
    }
}
