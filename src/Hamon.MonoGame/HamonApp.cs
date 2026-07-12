using Hamon.Fonts;
using Hamon.Layout;
using Hamon.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Hamon.Layout.Color;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Hamon.MonoGame;

/// <summary>
/// Build options for <see cref="HamonApp"/>. Values that can also be changed at runtime, such as the
/// theme, can be set here initially and replaced later via properties like <see cref="HamonApp.Theme"/>.
/// </summary>
public sealed class HamonAppOptions
{
    /// <summary>Font (TTF/OTF) bytes, tried first. If not set (or unreadable), resolution falls through in order: <see cref="FontPath"/> → the HAMON_FONT environment variable → an adjacent *.ttf file → an OS font.</summary>
    public byte[]? Font { get; init; }

    /// <summary>Font file path (TTF/OTF).</summary>
    public string? FontPath { get; init; }

    /// <summary>Glyph raster resolution factor (1-4, default 3 = high quality). </summary>
    public int FontQuality { get; init; } = 3;

    /// <summary>Completely replaces the text renderer (advanced use; when set, font resolution is skipped).</summary>
    public ITextRenderer? TextRenderer { get; init; }

    /// <summary>Enables IME support (preedit text and candidate window positioning during conversion). Default true; requires DesktopGL/SDL.</summary>
    public bool EnableIme { get; init; } = true;

    /// <summary>Whether <see cref="HamonApp.Draw"/> clears the screen before rendering (default true).</summary>
    public bool ClearBackground { get; init; } = true;

    /// <summary>Clear color. If not specified, the effective theme's background color is used.</summary>
    public Color? ClearColor { get; init; }

    /// <summary>Initial theme. Defaults to <see cref="HamonTheme.Default"/> (the light theme) if not specified.</summary>
    public HamonTheme? Theme { get; init; }

    /// <summary>Dark color scheme (opt-in). </summary>
    public HamonTheme? DarkTheme { get; init; }

    /// <summary>Theme mode (default <see cref="Widgets.ThemeMode.System"/>).</summary>
    public ThemeMode ThemeMode { get; init; } = ThemeMode.System;

    /// <summary>Multiplier converting one mouse wheel notch to pixels (default 0.22).</summary>
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

    /// <summary>Window title.</summary>
    public string? WindowTitle { get; init; }

    /// <summary>Whether to display full screen.</summary>
    public bool? Fullscreen { get; init; }

    /// <summary>Whether to enable vertical sync.</summary>
    public bool? VSync { get; init; }

    /// <summary>Whether to display the mouse cursor. </summary>
    public bool? MouseVisible { get; init; }

    /// <summary>Whether to use a fixed time step (false = real delta-time driven, so animation follows high refresh rates).</summary>
    public bool? FixedTimeStep { get; init; }
}

/// <summary>
/// A high-level facade connecting MonoGame and Hamon. Input (mouse, keyboard, gamepad, IME), HiDPI
/// tracking, and background clearing are wired up internally, so it works just by calling
/// <see cref="Update"/> and <see cref="Draw"/>. Use this to let Hamon coexist with an existing
/// <see cref="Game"/>. When finer control is needed, access <see cref="Ui"/> (all <see cref="HamonRoot"/>
/// APIs) and <see cref="Input"/> directly.
/// </summary>
/// <remarks>
/// <para>Because a <c>GraphicsDevice</c> is required, construct this from <see cref="Game.LoadContent"/> or later.</para>
/// <para>Example: <c>_app = new HamonApp(this, () =&gt; new MyRoot()) { Theme = HamonTheme.Dark };</c></para>
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

    /// <summary>Coexists with an existing <see cref="Game"/> (uses its <c>game.GraphicsDevice</c>/<c>game.Window</c>).</summary>
    public HamonApp(Game game, Func<Widget>? root = null, HamonAppOptions? options = null)
        : this(game, GraphicsOf(game), game.Window, root, options)
    {
    }

    /// <summary>A low-level constructor that builds directly from a <c>GraphicsDevice</c> (and an optional <c>GameWindow</c>).</summary>
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

    /// <summary>The underlying <see cref="HamonRoot"/> (an escape hatch for all its APIs: Overlay, Navigator, Focus, etc.).</summary>
    public HamonRoot Ui { get; }

    /// <summary>The input pump. Its <see cref="HamonInput.OnBack"/> handler is exposed via the <see cref="OnBack"/> property below.</summary>
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

    /// <summary>Whether <see cref="Draw"/> should clear the background.</summary>
    public bool ClearBackground { get; set; }

    /// <summary>Clear color. If null, uses the effective theme's background color.</summary>
    public Color? ClearColor { get; set; }

    /// <summary>Replaces the root widget with a builder function (called to (re)build the tree).</summary>
    public void SetRoot(Func<Widget> root) => Ui.SetRoot(root);

    /// <summary>Replaces the root widget with a fixed widget instance (wrapped as a builder that always returns the same instance).</summary>
    public void SetRoot(Widget root) => Ui.SetRoot(() => root);

    /// <summary>Polls input, updates the HiDPI ratio and size, then updates the UI. Call from <see cref="Game.Update"/>.</summary>
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

    /// <summary>Clears the background (optional) and draws the UI. Call from <see cref="Game.Draw"/>.</summary>
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
            ?? throw new InvalidOperationException("Hamon: GraphicsDevice has not been created yet. Access HamonApp from LoadContent or later.");
    }

    /// <summary>Retrieves the <see cref="GraphicsDeviceManager"/> held by a <see cref="Game"/> (via its service container).</summary>
    internal static GraphicsDeviceManager? ManagerOf(Game game) =>
        game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager;

    /// <summary>
    /// Applies only the specified (non-null) window options; unspecified options are left at their current
    /// value. If <paramref name="manager"/> is null, size/fullscreen/VSync are not applied (there's nothing
    /// to apply them to, so this is a no-op for those). When <paramref name="applyChanges"/> is true,
    /// <see cref="GraphicsDeviceManager.ApplyChanges"/> is called, but only if size, fullscreen, or VSync
    /// actually changed. (Before the device is created — i.e. in <see cref="HamonGame"/>'s constructor —
    /// pass false, which is preferred there.)
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
            "Hamon: No usable font was found. Specify a TTF/OTF via HamonAppOptions.Font / FontPath or the HAMON_FONT " +
            "environment variable, or place a *.ttf next to the executable.");
    }
}
