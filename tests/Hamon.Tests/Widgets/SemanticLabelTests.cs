using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification that the semantic label is transmitted to the FocusNode and can be read from the focused node (a11y foundation).</summary>
public class SemanticLabelTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static HamonRoot Mount(Widget w)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => w);
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Button_SemanticLabel_OnFocusedNode()
    {
        var node = new FocusNode();
        HamonRoot host = Mount(new Button { Node = node, Autofocus = true, SemanticLabel = "Confirm", OnPressed = () => { }, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(20) } });
        Assert.Equal("Confirm", host.Focus.Focused!.SemanticLabel);
    }

    [Fact]
    public void Checkbox_SemanticLabel_Propagates()
    {
        var node = new FocusNode();
        HamonRoot host = Mount(new Checkbox { Node = node, Autofocus = true, SemanticLabel = "Agree to terms", OnChanged = _ => { } });
        Assert.Equal("Agree to terms", host.Focus.Focused!.SemanticLabel);
    }

    [Fact]
    public void TextField_FallsBackToPlaceholder()
    {
        var node = new FocusNode();
        var ctrl = new TextEditingController();
        HamonRoot host = Mount(new TextField { Controller = ctrl, Node = node, Autofocus = true, Placeholder = "Search" });
        Assert.Equal("Search", host.Focus.Focused!.SemanticLabel);
    }

    [Fact]
    public void Slider_SemanticLabel_Propagates()
    {
        var node = new FocusNode();
        HamonRoot host = Mount(new Slider { Node = node, Autofocus = true, SemanticLabel = "Volume", OnChanged = _ => { } });
        Assert.Equal("Volume", host.Focus.Focused!.SemanticLabel);
    }
}
