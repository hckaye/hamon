using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for tabs (TabController switching animation + Tabs body replacement).</summary>
public class TabsTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(400, 300);

    private static HamonRoot Mount()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);
        return host;
    }

    // controller を Tabs に載せてマウントする（Tabs.Build が host を Attach＝切替アニメが用意される）。
    private static HamonRoot MountTabs(TabController ctrl, int count)
    {
        var host = new HamonRoot(new StubTextRenderer());
        var items = new TabItem[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = new TabItem(new SizedBox(), () => new SizedBox());
        }

        host.SetRoot(() => new Tabs { Controller = ctrl, Items = items });
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Select_AnimatesFractionAndBodyFade()
    {
        var ctrl = new TabController(3, initial: 0, duration: 0.2f, curve: Curves.Linear);
        HamonRoot host = MountTabs(ctrl, 3); // Tabs マウントで Attach＝アニメ用意

        Assert.Equal(0, ctrl.Index);
        Assert.Equal(0f, ctrl.Fraction, 0.001f);
        Assert.Equal(1f, ctrl.BodyFade, 0.001f); // 初期は落ち着き

        ctrl.Select(2);
        Assert.Equal(2, ctrl.Index);
        Assert.Equal(0f, ctrl.Fraction, 0.001f);  // 切替直後は旧位置
        Assert.Equal(0f, ctrl.BodyFade, 0.001f);

        host.Update(Viewport, 0.1f); // 半分
        Assert.Equal(1.0f, ctrl.Fraction, 0.05f); // 0→2 の中間
        Assert.Equal(0.5f, ctrl.BodyFade, 0.05f);

        host.Update(Viewport, 0.1f); // 完了
        Assert.Equal(2f, ctrl.Fraction, 0.001f);
        Assert.Equal(1f, ctrl.BodyFade, 0.001f);
    }

    [Fact]
    public void Select_SameIndex_IsNoOp_And_Clamps()
    {
        var ctrl = new TabController(3, initial: 1);
        MountTabs(ctrl, 3); // Tabs マウントで Attach

        ctrl.Select(1); // 同じ＝無変化
        Assert.Equal(1, ctrl.Index);
        Assert.Equal(1f, ctrl.BodyFade, 0.001f);

        ctrl.Select(99); // クランプ
        Assert.Equal(2, ctrl.Index);
    }

    [Fact]
    public void Tabs_BuildsOnlyActiveBody_SwapsOnSelect()
    {
        HamonRoot host = Mount();
        var ctrl = new TabController(2);
        int[] built = new int[2];

        host.SetRoot(() => new Tabs
        {
            Controller = ctrl,
            Items = new[]
            {
                new TabItem(new SizedBox(), () => { built[0]++; return new SizedBox(); }),
                new TabItem(new SizedBox(), () => { built[1]++; return new SizedBox(); }),
            },
        });
        host.Update(Viewport);

        Assert.True(built[0] >= 1);   // アクティブ(0)の本体だけ構築
        Assert.Equal(0, built[1]);

        ctrl.Select(1);
        host.Update(Viewport);
        Assert.True(built[1] >= 1);   // 切替で(1)の本体を構築
    }
}
