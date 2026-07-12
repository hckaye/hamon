using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verify ImageSkin (9-slice/sprite/stretch) and game widget (VirtualJoystick/Dpad) skin application/fallback.</summary>
public class ImageSkinTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class StubTexture : ITexture
    {
        public int Width => 32;

        public int Height => 32;
    }

    private sealed class Recorder : IPainter
    {
        public int RoundedRects;
        public int Rects;
        public int Textures;

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Rects++;

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedRects++;

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint) => Textures++;

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static readonly Size Viewport = new(300, 300);

    private static Recorder Render(Widget root)
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = root });
        host.Update(Viewport);
        var rec = new Recorder();
        host.Render(rec);
        return rec;
    }

    [Fact]
    public void ImageSkin_Stretch_OneTextureDraw()
    {
        var rec = new Recorder();
        var skin = new ImageSkin(new StubTexture());
        skin.Paint(new PaintContext(rec), new Rect(0, 0, 100, 50));
        Assert.Equal(1, rec.Textures);
    }

    [Fact]
    public void ImageSkin_NineSlice_NineTextureDraws()
    {
        var rec = new Recorder();
        var skin = new ImageSkin(new StubTexture(), EdgeInsets.All(8f));
        skin.Paint(new PaintContext(rec), new Rect(0, 0, 100, 100));
        Assert.Equal(9, rec.Textures); // 角4＋辺4＋中央1
    }

    [Fact]
    public void ImageSkin_NotSet_DoesNothing()
    {
        var rec = new Recorder();
        default(ImageSkin).Paint(new PaintContext(rec), new Rect(0, 0, 10, 10));
        Assert.Equal(0, rec.Textures);
    }

    [Fact]
    public void VirtualJoystick_WithSkins_DrawsTextures()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new VirtualJoystick { BaseSkin = new ImageSkin(tex), KnobSkin = new ImageSkin(tex) });
        Assert.Equal(2, rec.Textures);      // ベース＋ノブ
        Assert.Equal(0, rec.RoundedRects);  // 色描画は使わない
    }

    [Fact]
    public void VirtualJoystick_NoSkin_DrawsColors()
    {
        Recorder rec = Render(new VirtualJoystick());
        Assert.Equal(0, rec.Textures);
        Assert.Equal(2, rec.RoundedRects); // ベース＋ノブの色描画
    }

    [Fact]
    public void Dpad_WithBaseSkin_DrawsTexture_NotArms()
    {
        Recorder rec = Render(new Dpad { BaseSkin = new ImageSkin(new StubTexture()) });
        Assert.Equal(1, rec.Textures);     // 全体スプライト
        Assert.Equal(0, rec.RoundedRects); // 4腕の色描画は使わない
    }

    [Fact]
    public void Dpad_NoSkin_DrawsArms()
    {
        Recorder rec = Render(new Dpad());
        Assert.Equal(0, rec.Textures);
        Assert.True(rec.RoundedRects >= 4); // 4腕
    }

    [Fact]
    public void Checkbox_OnSkin_DrawsTexture()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new Checkbox { Value = true, OnSkin = new ImageSkin(tex), OffSkin = new ImageSkin(tex) });
        Assert.Equal(1, rec.Textures);
        Assert.Equal(0, rec.Rects); // 枠線（FillRect×4）は描かない
    }

    [Fact]
    public void Switch_Skins_DrawTwoTextures()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new Switch { Value = true, TrackSkin = new ImageSkin(tex), KnobSkin = new ImageSkin(tex) });
        Assert.Equal(2, rec.Textures);     // トラック＋ノブ
        Assert.Equal(0, rec.RoundedRects);
    }

    [Fact]
    public void Slider_Skins_DrawThreeTextures()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new Slider { Value = 0.5f, TrackSkin = new ImageSkin(tex), FillSkin = new ImageSkin(tex), ThumbSkin = new ImageSkin(tex) });
        Assert.Equal(3, rec.Textures);     // レール＋進捗＋つまみ
        Assert.Equal(0, rec.RoundedRects);
    }

    [Fact]
    public void Radio_OnSkin_DrawsTexture()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new Radio<int> { Value = 1, GroupValue = 1, OnSkin = new ImageSkin(tex), OffSkin = new ImageSkin(tex) });
        Assert.Equal(1, rec.Textures);
    }

    [Fact]
    public void ProgressBar_Skins_DrawTwoTextures()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new ProgressBar { Value = 0.5f, TrackSkin = new ImageSkin(tex), FillSkin = new ImageSkin(tex) });
        Assert.Equal(2, rec.Textures);     // レール＋進捗
        Assert.Equal(0, rec.RoundedRects);
    }

    [Fact]
    public void ProgressBar_NoSkin_DrawsColors()
    {
        Recorder rec = Render(new ProgressBar { Value = 0.5f });
        Assert.Equal(0, rec.Textures);
        Assert.True(rec.RoundedRects >= 2); // レール＋進捗の色描画
    }

    [Fact]
    public void Spinner_Sprite_DrawsTexture()
    {
        Recorder rec = Render(new CircularProgressIndicator { Sprite = new ImageSkin(new StubTexture()) });
        Assert.Equal(1, rec.Textures); // 回転スプライト
    }

    [Fact]
    public void Button_BackgroundImage_DrawsTexture_NotColor()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new Button
        {
            OnPressed = () => { },
            Style = new ButtonStyle { BackgroundImage = WidgetStateProperty<ImageSkin?>.All(new ImageSkin(tex)) },
            Child = new SizedBox { Width = Dimension.Px(80), Height = Dimension.Px(40) },
        });
        Assert.Equal(1, rec.Textures);
        Assert.Equal(0, rec.RoundedRects); // 色背景/ステートレイヤーは描かない
    }

    [Fact]
    public void SlotButton_FrameSkin_DrawsTexture()
    {
        Recorder rec = Render(new SlotButton { FrameSkin = new ImageSkin(new StubTexture()) });
        Assert.True(rec.Textures >= 1); // 枠スプライト（Button 背景スキン経由）
    }

    [Fact]
    public void CooldownButton_FrameSkin_DrawsTexture()
    {
        Recorder rec = Render(new CooldownButton { Progress = 1f, FrameSkin = new ImageSkin(new StubTexture()) });
        Assert.True(rec.Textures >= 1);
    }

    [Fact]
    public void Material_Skin_DrawsTexture_NotColor()
    {
        var tex = new StubTexture();
        Recorder rec = Render(new Material { Skin = new ImageSkin(tex), Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(60) } });
        Assert.Equal(1, rec.Textures);
        Assert.Equal(0, rec.RoundedRects);
        Assert.Equal(0, rec.Rects);
    }

    [Fact]
    public void Card_Skin_DrawsTexture()
    {
        Recorder rec = Render(new Card { Skin = new ImageSkin(new StubTexture()), Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(60) } });
        Assert.True(rec.Textures >= 1);
    }
}
