using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Mouse hover routing (<see cref="MouseRegion"/>・Determinism test for enter/hover/exit/occlusion/cursor).</summary>
public class HoverTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static HamonRoot Mount(Widget root)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Enter_Hover_Exit_FireInOrder()
    {
        var log = new List<string>();
        Widget root = new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(50),
                    Top = Dimension.Px(50),
                    Width = Dimension.Px(50),
                    Height = Dimension.Px(50),
                    Child = new MouseRegion
                    {
                        OnEnter = _ => log.Add("enter"),
                        OnHover = _ => log.Add("hover"),
                        OnExit = _ => log.Add("exit"),
                    },
                },
            },
        };
        HamonRoot host = Mount(root);

        host.DispatchHover(new Vec2(60, 60)); // 領域内へ入る
        host.DispatchHover(new Vec2(70, 70)); // 領域内で動く
        host.DispatchHover(new Vec2(10, 10)); // 領域外へ出る

        Assert.Equal(new[] { "enter", "hover", "exit" }, log);
    }

    [Fact]
    public void ClearHover_ExitsActiveRegion()
    {
        bool exited = false;
        Widget root = new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0),
                    Top = Dimension.Px(0),
                    Width = Dimension.Px(100),
                    Height = Dimension.Px(100),
                    Child = new MouseRegion { OnExit = _ => exited = true },
                },
            },
        };
        HamonRoot host = Mount(root);

        host.DispatchHover(new Vec2(10, 10));
        Assert.False(exited);
        host.ClearHover(); // マウスがウィンドウ外へ
        Assert.True(exited);
    }

    [Fact]
    public void NestedRegions_BothReceiveEnter()
    {
        var log = new List<string>();
        Widget root = new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0),
                    Top = Dimension.Px(0),
                    Width = Dimension.Px(100),
                    Height = Dimension.Px(100),
                    Child = new MouseRegion
                    {
                        OnEnter = _ => log.Add("outer"),
                        Child = new MouseRegion { OnEnter = _ => log.Add("inner") },
                    },
                },
            },
        };
        HamonRoot host = Mount(root);

        host.DispatchHover(new Vec2(20, 20));
        Assert.Contains("outer", log);
        Assert.Contains("inner", log);
    }

    [Fact]
    public void OpaqueRegion_BlocksRegionBehind()
    {
        bool behindEntered = false;
        bool frontEntered = false;
        Widget root = new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                Cover(new MouseRegion { OnEnter = _ => behindEntered = true }),
                Cover(new MouseRegion { Opaque = true, OnEnter = _ => frontEntered = true }), // 前面・不透明
            },
        };
        HamonRoot host = Mount(root);

        host.DispatchHover(new Vec2(20, 20));
        Assert.True(frontEntered);
        Assert.False(behindEntered); // 不透明な前面に遮られて背後は hover しない
    }

    [Fact]
    public void TranslucentRegion_PassesHoverToBehind()
    {
        bool behindEntered = false;
        Widget root = new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                Cover(new MouseRegion { OnEnter = _ => behindEntered = true }),
                Cover(new MouseRegion { Opaque = false }), // 前面・半透明＝背後へ通す
            },
        };
        HamonRoot host = Mount(root);

        host.DispatchHover(new Vec2(20, 20));
        Assert.True(behindEntered);
    }

    [Fact]
    public void CurrentCursor_ReflectsFrontmostRegion()
    {
        Widget root = new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                Cover(new MouseRegion { Cursor = MouseCursor.Click }),
            },
        };
        HamonRoot host = Mount(root);

        Assert.Equal(MouseCursor.Basic, host.CurrentCursor);
        host.DispatchHover(new Vec2(20, 20));
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);
        host.DispatchHover(new Vec2(150, 150)); // 領域外（このカバーは 0..100）
        Assert.Equal(MouseCursor.Basic, host.CurrentCursor);
    }

    [Fact]
    public void UnmountWhileHovered_FiresExit()
    {
        bool exited = false;
        bool show = true;
        Widget Build() => new Stack
        {
            Fit = StackFit.Expand,
            Children = show
                ? new Widget[] { Cover(new MouseRegion { OnExit = _ => exited = true }) }
                : System.Array.Empty<Widget>(),
        };

        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(Build);
        host.Update(Viewport);
        host.DispatchHover(new Vec2(20, 20));
        Assert.False(exited);

        show = false;
        host.MarkDirty();
        host.Update(Viewport); // reconcile で MouseRegion が Unmount
        Assert.True(exited);
    }

    // 0..100 の正方形を占める Positioned で包む小ヘルパ。
    private static Widget Cover(Widget child) => new Positioned
    {
        Left = Dimension.Px(0),
        Top = Dimension.Px(0),
        Width = Dimension.Px(100),
        Height = Dimension.Px(100),
        Child = child,
    };
}
