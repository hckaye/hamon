using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for Bind/ValueNotifier (reflects frequent values ​​without full tree reconstruction).</summary>
public class BindTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void ValueNotifier_NotifiesOnlyOnChange()
    {
        var n = new ValueNotifier<int>(0);
        int fired = 0;
        n.AddListener(() => fired++);

        n.Value = 1;
        n.Value = 1; // 同値＝通知なし
        n.Value = 2;
        Assert.Equal(2, fired);

        n.Notify(); // 強制通知
        Assert.Equal(3, fired);
    }

    [Fact]
    public void Bind_RebuildsOnlySubtree_NotWholeTree()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var notifier = new ValueNotifier<int>(0);
        int rootBuilds = 0;
        int bindBuilds = 0;
        int lastValue = -1;

        host.SetRoot(() =>
        {
            rootBuilds++;
            return new Column
            {
                Children = new Widget[]
                {
                    new Bind<int>
                    {
                        Listenable = notifier,
                        Builder = v =>
                        {
                            bindBuilds++;
                            lastValue = v;
                            return new SizedBox();
                        },
                    },
                },
            };
        });

        host.Update(Viewport);
        Assert.Equal(1, rootBuilds);
        Assert.Equal(1, bindBuilds);

        notifier.Value = 5;
        host.Update(Viewport);

        Assert.Equal(1, rootBuilds);  // 全ツリーは再構築されない
        Assert.Equal(2, bindBuilds);  // Bind の部分木だけ再構築
        Assert.Equal(5, lastValue);
    }

    [Fact]
    public void Bind_SameValue_NoRebuild()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var notifier = new ValueNotifier<int>(7);
        int bindBuilds = 0;

        host.SetRoot(() => new Bind<int>
        {
            Listenable = notifier,
            Builder = _ =>
            {
                bindBuilds++;
                return new SizedBox();
            },
        });
        host.Update(Viewport);
        Assert.Equal(1, bindBuilds);

        notifier.Value = 7; // 同値＝通知なし＝再構築なし
        host.Update(Viewport);
        Assert.Equal(1, bindBuilds);
    }

    [Fact]
    public void Bind_SwapsChildType_AcrossRebuild()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var flag = new ValueNotifier<bool>(false);

        host.SetRoot(() => new Bind<bool>
        {
            Listenable = flag,
            Builder = b => b ? new Container { Color = Color.Red } : new SizedBox(),
        });
        host.Update(Viewport);
        var bind = (BindElement<bool>)host.Root!;
        Assert.IsType<SizedBox>(bind.Children[0].Widget);

        flag.Value = true;
        host.Update(Viewport);
        Assert.IsType<Container>(bind.Children[0].Widget); // 型変化でも安定 node の下で差替
        Assert.Single(bind.Children);
    }
}
