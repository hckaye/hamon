using Hamon.Fonts;
using Hamon.Layout;
using Hamon.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Hamon.Layout.Color;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Hamon.MonoGame;

/// <summary>
/// <see cref="HamonApp"/>Build options.
/// Things that can be changed when running a theme etc.<see cref="HamonApp.Theme"/>It can be replaced later with properties such as.
/// </summary>
public sealed class HamonAppOptions
{
    /// <summary>Font (TTF/OTF) byte string. <see cref="FontPath"/>→HAMON_FONT→adjacent *.ttf→OS font is resolved in this order.</summary>
    public byte[]? Font { get; init; }

    /// <summary>Font file path (TTF/OTF).</summary>
    public string? FontPath { get; init; }

    /// <summary>Glyph raster resolution factor (1-4, default 3 = high quality). </summary>
    public int FontQuality { get; init; } = 3;

    /// <summary>Completely replace the text renderer (for advanced users. Skip font resolution when specified).</summary>
    public ITextRenderer? TextRenderer { get; init; }

    /// <summary>Enable IME (preedit + candidate position during conversion) (default true; DesktopGL/SDL required).</summary>
    public bool EnableIme { get; init; } = true;

    /// <summary><see cref="HamonApp.Draw"/>Clear the screen with (default true). </summary>
    public bool ClearBackground { get; init; } = true;

    /// <summary>Clear color (if not specified, the background color of the actual theme).</summary>
    public Color? ClearColor { get; init; }

    /// <summary>Initial theme (ripple light if not specified)<see cref="HamonTheme.Default"/>）。</summary>
    public HamonTheme? Theme { get; init; }

    /// <summary>Dark color scheme (opt-in). </summary>
    public HamonTheme? DarkTheme { get; init; }

    /// <summary>Theme mode (default<see cref="Widgets.ThemeMode.System"/>）。</summary>
    public ThemeMode ThemeMode { get; init; } = ThemeMode.System;

    /// <summary>Wheel 1 notch → px magnification (default 0.22).</summary>
    public float WheelScale { get; init; } = 0.22f;

    // --- ウィンドウ。すべて null 許容＝「未指定」。既存 <see cref="Game"/> 同居版では、未指定の項目は適用せず
    //     Game が本来持つ値をそのまま使う（吸い出せない＝Game/manager が無ければ何も適用しない）。
    //     <see cref="HamonGame"/> は Hamon 主体のホストなので使い勝手の良い既定を敷いた上で、指定分を上書きする。 ---

    /// <summary>Window width (px/logical pt). </summary>
    public int? WindowWidth { get; init; }

    /// <summary>Window height (px/logical pt). </summary>
    public int? WindowHeight { get; init; }

    /// <summary>Whether to allow the user to resize the window. </summary>
    public bool? AllowResize { get; init; }

    /// <summary>window title. </summary>
    public string? WindowTitle { get; init; }

    /// <summary>Full screen display. </summary>
    public bool? Fullscreen { get; init; }

    /// <summary>Vertical sync. </summary>
    public bool? VSync { get; init; }

    /// <summary>Whether to display the mouse cursor. </summary>
    public bool? MouseVisible { get; init; }

    /// <summary>Is it a fixed time step (false = real DT drive, animation follows high refresh rate)? </summary>
    public bool? FixedTimeStep { get; init; }
}

/// <summary>
/// A high-level facade connecting MonoGame and Hamon. <see cref="HamonRoot"/> /
/// Input (mouse, keyboard, gamepad, IME), HiDPI tracking, and background clear are wired internally,
/// <see cref="Update"/>and<see cref="Draw"/>It works just by calling. <see cref="Game"/>Use to coexist with.
/// When fine control is required<see cref="Ui"/>（<see cref="HamonRoot"/>all APIs) and<see cref="Input"/>touch directly.
/// </summary>
/// <remarks>
/// <para>Because it requires a GraphicsDevice<see cref="Game.LoadContent"/>(below).</para>
/// <para>example:<c>_app = new HamonApp(this, () =&gt; new MyRoot()) { Theme = HamonTheme.Dark };</c></para>
/// </remarks>
public sealed class HamonApp : IDisposable
{
    private readonly Game? _game;
    private readonly GraphicsDevice _device;
    private readonly GameWindow? _window;
    private readonly SpriteBatch _batch;
    private readonly MonoGamePainter _painter;
    private readonly ITextRenderer _text;
    private readonly bool _ownsText;
    private bool _disposed;

    /// <summary>existing<see cref="Game"/>Let them live together (<c>game.GraphicsDevice</c>/<c>game.Window</c>). </summary>
    public HamonApp(Game game, Func<Widget>? root = null, HamonAppOptions? options = null)
        : this(game, GraphicsOf(game), game.Window, root, options)
    {
    }

    /// <summary>A low-level version that builds directly from GraphicsDevices (and any Windows). </summary>
    public HamonApp(GraphicsDevice device, GameWindow? window = null, Func<Widget>? root = null, HamonAppOptions? options = null)
        : this(null, device, window, root, options)
    {
    }

    private HamonApp(Game? game, GraphicsDevice device, GameWindow? window, Func<Widget>? root, HamonAppOptions? options)
    {
        ArgumentNullException.ThrowIfNull(device);
        options ??= new HamonAppOptions();

        _game = game;
        _device = device;
        _window = window;
        _batch = new SpriteBatch(device);
        _painter = new MonoGamePainter(device, _batch);

        if (options.TextRenderer is not null)
        {
            _text = options.TextRenderer;
            _ownsText = false;
        }
        else
        {
            _text = BuildText(device, _batch, options);
            _ownsText = true;
        }

        Ui = new HamonRoot(_text)
        {
            Theme = options.Theme ?? HamonTheme.Default,
            DarkTheme = options.DarkTheme,
            ThemeMode = options.ThemeMode,
        };
        Input = new HamonInput(Ui) { WheelScale = options.WheelScale };
        ClearBackground = options.ClearBackground;
        ClearColor = options.ClearColor;

        if (window is not null)
        {
            // 物理キーボードの確定文字（TextField 等）。args 型は DesktopGL/KNI で名前空間が違うため、型名を書かず lambda で受ける。
            window.TextInput += (_, e) =>
            {
                if (!_disposed)
                {
                    Ui.DispatchText(e.Character);
                }
            };
            // IME（変換中＋候補位置）。SDL 非対応環境では諦める（致命ではない）。
            if (options.EnableIme)
            {
                try
                {
                    Ui.TextInput = new SdlTextInput((preedit, caret) => Ui.DispatchComposition(preedit, caret));
                }
                catch
                {
                    // IME なしで続行。
                }
            }
        }

        if (_game is not null)
        {
            // 既存 Game 同居時はここでウィンドウ設定を適用（HamonGame の場合は ctor で先に適用済みなので差分があれば反映）。
            ApplyWindow(_game, _window, ManagerOf(_game), options, applyChanges: true);
        }

        if (root is not null)
        {
            Ui.SetRoot(root);
        }
    }

    /// <summary>subordinate<see cref="HamonRoot"/>(Escape hatch for all APIs: Overlay/Navigator/Focus, etc.).</summary>
    public HamonRoot Ui { get; }

    /// <summary>input pump.<see cref="HamonInput.OnBack"/>``Return'' processing is assigned to .</summary>
    public HamonInput Input { get; }

    /// <summary>Current light (default) theme. </summary>
    public HamonTheme Theme
    {
        get => Ui.Theme;
        set => Ui.Theme = value;
    }

    /// <summary>Dark color scheme (opt-in).</summary>
    public HamonTheme? DarkTheme
    {
        get => Ui.DarkTheme;
        set => Ui.DarkTheme = value;
    }

    /// <summary>Theme mode (System/Light/Dark).</summary>
    public ThemeMode ThemeMode
    {
        get => Ui.ThemeMode;
        set => Ui.ThemeMode = value;
    }

    /// <summary>Handler for "Back" (Esc / Pad B).</summary>
    public Action? OnBack
    {
        get => Input.OnBack;
        set => Input.OnBack = value;
    }

    /// <summary><see cref="Draw"/>Do you want to clear the background?</summary>
    public bool ClearBackground { get; set; }

    /// <summary>Clear color (if null, the background of the actual theme).</summary>
    public Color? ClearColor { get; set; }

    /// <summary>Replace the root widget (builder returns the same tree every time).</summary>
    public void SetRoot(Func<Widget> root) => Ui.SetRoot(root);

    /// <summary>Replace the root widget (fixed tree).</summary>
    public void SetRoot(Widget root) => Ui.SetRoot(() => root);

    /// <summary>Take input, update HiDPI ratio and size, then update UI.<see cref="Game.Update"/>call from.</summary>
    public void Update(GameTime gameTime)
    {
        Input.Update();

        Viewport vp = _device.Viewport;
        Size size;
        float dpr = 1f;
        if (_window is not null)
        {
            // HiDPI/Retina: viewport(物理px) ではなく ClientBounds(論理pt) でレイアウトし、比率で描画スケールする。
            Rectangle cb = _window.ClientBounds;
            dpr = cb.Width > 0 ? vp.Width / (float)cb.Width : 1f;
            size = new Size(cb.Width, cb.Height);
        }
        else
        {
            size = new Size(vp.Width, vp.Height);
        }

        Ui.DevicePixelRatio = dpr;
        Ui.Update(size, (float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    /// <summary>Background clear (optional) + UI drawing.<see cref="Game.Draw"/>call from.</summary>
    public void Draw(GameTime gameTime)
    {
        if (ClearBackground)
        {
            Color c = ClearColor ?? Ui.EffectiveTheme.Background;
            _device.Clear(new XnaColor(c.R, c.G, c.B, c.A));
        }

        Ui.Render(_painter);
    }

    /// <summary>Draw only the UI without clearing it (use to superimpose it as a HUD on top of the game world).</summary>
    public void Render() => Ui.Render(_painter);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true; // window.TextInput への購読は残るが、以降は no-op（破棄後の Ui 参照を防ぐ）。

        (Ui.TextInput as IDisposable)?.Dispose();
        if (_ownsText)
        {
            (_text as IDisposable)?.Dispose();
        }

        _painter.Dispose();
        _batch.Dispose();
    }

    private static GraphicsDevice GraphicsOf(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.GraphicsDevice
            ?? throw new InvalidOperationException("Hamon: GraphicsDevice が未生成です。HamonApp は LoadContent 以降で生成してください。");
    }

    /// <summary><see cref="Game"/>holds<see cref="GraphicsDeviceManager"/>(via service). </summary>
    internal static GraphicsDeviceManager? ManagerOf(Game game) =>
        game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager;

    /// <summary>
    /// Applies only the specified (non-null) window options.
    /// Use the original value as is.<paramref name="manager"/>If there is no size/full screen/VSync will not be applied (cannot download = does nothing).
    /// <paramref name="applyChanges"/>Only when there is a difference in size/fullscreen/VSync<see cref="GraphicsDeviceManager.ApplyChanges"/>do
    /// (before device generation<see cref="HamonGame"/>In ctor, just set false=preferred).
    /// </summary>
    internal static void ApplyWindow(Game game, GameWindow? window, GraphicsDeviceManager? manager, HamonAppOptions o, bool applyChanges)
    {
        if (o.MouseVisible is bool mouse)
        {
            game.IsMouseVisible = mouse;
        }

        if (o.FixedTimeStep is bool fixedStep)
        {
            game.IsFixedTimeStep = fixedStep;
        }

        if (window is not null)
        {
            if (o.AllowResize is bool resize)
            {
                window.AllowUserResizing = resize;
            }

            if (o.WindowTitle is string title)
            {
                window.Title = title;
            }
        }

        if (manager is null)
        {
            return;
        }

        bool changed = false;
        if (o.WindowWidth is int w && manager.PreferredBackBufferWidth != w)
        {
            manager.PreferredBackBufferWidth = w;
            changed = true;
        }

        if (o.WindowHeight is int h && manager.PreferredBackBufferHeight != h)
        {
            manager.PreferredBackBufferHeight = h;
            changed = true;
        }

        if (o.VSync is bool vsync && manager.SynchronizeWithVerticalRetrace != vsync)
        {
            manager.SynchronizeWithVerticalRetrace = vsync;
            changed = true;
        }

        if (o.Fullscreen is bool full && manager.IsFullScreen != full)
        {
            manager.IsFullScreen = full;
            changed = true;
        }

        if (changed && applyChanges)
        {
            manager.ApplyChanges();
        }
    }

    private static ITextRenderer BuildText(GraphicsDevice device, SpriteBatch batch, HamonAppOptions options)
    {
        foreach (byte[] bytes in HamonFont.Candidates(options))
        {
            try
            {
                return new FontStashTextRenderer(device, bytes, batch, options.FontQuality);
            }
            catch
            {
                // 読めない候補（.ttc 等 FontStash 非対応・破損）は飛ばして次へ。
            }
        }

        throw new InvalidOperationException(
            "Hamon: 使用可能なフォントが見つかりません。HamonAppOptions.Font / FontPath か 環境変数 HAMON_FONT で TTF/OTF を指定するか、" +
            "実行ファイルと同じ場所に *.ttf を配置してください。");
    }
}
