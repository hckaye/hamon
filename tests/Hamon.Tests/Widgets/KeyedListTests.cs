using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for keyed virtualization (keyed rows reuse the same element across indexes).</summary>
public class KeyedListTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    [Fact]
    public void KeyedRows_PreserveElementAcrossIndexShift()
    {
        string[] keys = { "A", "B", "C", "D", "E" };
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = keys.Length,
            ItemExtent = 30f,
            Builder = i => new SizedBox { Key = keys[i], Height = Dimension.Px(30) },
        });
        host.Update(new Size(100, 150)); // 5 行ぶん可視

        var list = (ListViewElement)host.Root!;
        Element cBefore = list.ActiveElement(2)!; // Key "C" は index 2

        // 先頭を削除＝"C" は index 1 へ移動。
        keys = new[] { "B", "C", "D", "E" };
        host.Invalidate();
        host.Update(new Size(100, 150));

        Element cAfter = list.ActiveElement(1)!;
        Assert.Same(cBefore, cAfter); // 同一要素が index を跨いで再利用される
    }

    [Fact]
    public void UnkeyedRows_ReuseByIndex_AsBefore()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 100,
            ItemExtent = 30f,
            Builder = i => new SizedBox { Height = Dimension.Px(30) }, // Key 無し
        });
        host.Update(new Size(100, 150));

        var list = (ListViewElement)host.Root!;
        Element atIndex1 = list.ActiveElement(1)!;
        host.Invalidate();
        host.Update(new Size(100, 150));

        // Key 無しは同 index で再利用（従来動作）。
        Assert.Same(atIndex1, list.ActiveElement(1));
    }
}
