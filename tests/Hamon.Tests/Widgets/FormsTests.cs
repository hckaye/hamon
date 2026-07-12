using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verify the validation logic of Validators/FormController and the registration (UseEffect) of TextFormField.</summary>
public class FormsTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
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

    [Fact]
    public void Required_RejectsEmpty()
    {
        Func<string, string?> v = Validators.Required();
        Assert.NotNull(v(""));
        Assert.NotNull(v("   "));
        Assert.Null(v("x"));
    }

    [Fact]
    public void MinLength_Checks()
    {
        Func<string, string?> v = Validators.MinLength(3);
        Assert.NotNull(v("ab"));
        Assert.Null(v("abc"));
    }

    [Fact]
    public void Email_ChecksShape()
    {
        Func<string, string?> v = Validators.Email();
        Assert.NotNull(v("nope"));
        Assert.NotNull(v("a@b"));
        Assert.Null(v("a@b.co"));
        Assert.Null(v("")); // 空は Required に任せる
    }

    [Fact]
    public void Compose_ReturnsFirstError()
    {
        Func<string, string?> v = Validators.Compose(Validators.Required("req"), Validators.MinLength(3, "min"));
        Assert.Equal("req", v(""));
        Assert.Equal("min", v("ab"));
        Assert.Null(v("abc"));
    }

    [Fact]
    public void FormController_Validate_SetsErrors()
    {
        var form = new FormController();
        string value = "";
        string? lastError = "init";
        form.Register("email", () => value, Validators.Required("必須"), e => lastError = e);

        Assert.False(form.Validate());
        Assert.Equal("必須", lastError);

        value = "x";
        Assert.True(form.Validate());
        Assert.Null(lastError);
    }

    [Fact]
    public void FormController_Reset_ClearsErrors()
    {
        var form = new FormController();
        string? lastError = null;
        form.Register("a", () => "", Validators.Required(), e => lastError = e);
        form.Validate();
        Assert.NotNull(lastError);
        form.Reset();
        Assert.Null(lastError);
    }

    [Fact]
    public void TextFormField_RegistersWithForm()
    {
        var host = new HamonRoot(new StubText());
        var form = new FormController();
        var tec = new TextEditingController(string.Empty);
        host.SetRoot(() => new TextFormField { Form = form, Name = "email", Controller = tec, Validator = Validators.Required("必須") });
        host.Update(new Size(300, 300));

        // UseEffect でフィールドが登録され、Validate がフィールドを評価できる。
        Assert.True(form.Values().ContainsKey("email"));
        Assert.False(form.Validate()); // 空＋Required → 不正
        host.Update(new Size(300, 300)); // エラー状態で部分木が再構築される

        var painter = new NullPainter();
        host.Render(painter); // エラー表示込みで描画が例外を投げない
    }
}
