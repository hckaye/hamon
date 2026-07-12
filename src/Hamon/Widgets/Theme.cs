using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Light or dark color scheme (equivalent to Flutter's <c>Brightness</c>).</summary>
public enum Brightness
{
    /// <summary>Bright color scheme (light background).</summary>
    Light,

    /// <summary>Dark color scheme (dark background).</summary>
    Dark,
}

/// <summary>
/// Theme selection mode (equivalent to Flutter's <c>ThemeMode</c>), specified via <see cref="HamonRoot.ThemeMode"/>.
/// Dark mode is <b>opt-in</b>: the default is <see cref="System"/>, but the UI will not switch to dark unless
/// <see cref="HamonRoot.DarkTheme"/> is set.
/// </summary>
public enum ThemeMode
{
    /// <summary>Follows the OS brightness (<see cref="HamonRoot.PlatformBrightness"/>), but only switches to dark when <see cref="HamonRoot.DarkTheme"/> is configured.</summary>
    System,

    /// <summary>Always light (uses <see cref="HamonRoot.Theme"/>).</summary>
    Light,

    /// <summary>Always dark (uses <see cref="HamonRoot.DarkTheme"/>, falling back to <see cref="HamonRoot.Theme"/> if not set).</summary>
    Dark,
}

/// <summary>
/// Hamon's default UI style tokens (colors, spacing, font sizes, shadows), propagated via <see cref="BuildContext.Theme"/>.
/// Each widget uses these values by default unless a color is explicitly specified. The active theme can be
/// replaced via <see cref="HamonRoot.Theme"/>. A Material-like collection of tokens; <see cref="Default"/> is a
/// bright, semi-transparent light color scheme.
/// </summary>
public sealed class HamonTheme
{
    /// <summary>The brightness of this color scheme (equivalent to Flutter's <c>ThemeData.brightness</c>). Defaults to <see cref="Brightness.Light"/>.</summary>
    public Brightness Brightness { get; init; } = Brightness.Light;

    /// <summary>The screen's backmost layer (a pale, cool white reminiscent of a water surface).</summary>
    public Color Background { get; init; } = new(237, 245, 250);

    /// <summary>Color for cards and panels (the default surface).</summary>
    public Color Surface { get; init; } = new(255, 255, 255, 235);

    /// <summary>Surface variant color, used for elements like tracks, input fields, and tonal buttons.</summary>
    public Color SurfaceVariant { get; init; } = new(224, 238, 245, 232);

    /// <summary>The primary accent color (emphasis, selection, focus).</summary>
    public Color Primary { get; init; } = new(8, 157, 178);

    /// <summary>Foreground color used on top of <see cref="Primary"/> (text/icons).</summary>
    public Color OnPrimary { get; init; } = new(255, 255, 255);

    /// <summary>Main text on surface (deep cool slate).</summary>
    public Color OnSurface { get; init; } = new(24, 42, 52);

    /// <summary>Weak text (placeholder/auxiliary) on surfaces.</summary>
    public Color OnSurfaceVariant { get; init; } = new(92, 116, 128);

    /// <summary>Borders/separators (soft cool translucent edges).</summary>
    public Color Border { get; init; } = new(188, 214, 224, 210);

    /// <summary>Color for destructive actions (such as deletion).</summary>
    public Color Danger { get; init; } = new(226, 88, 102);

    // --- タイポグラフィスケール ---
    public float TextHeadline { get; init; } = 28f;

    public float TextTitle { get; init; } = 22f;

    public float TextBody { get; init; } = 16f;

    public float TextLabel { get; init; } = 13f;

    public float TextCaption { get; init; } = 11f;

    // --- スペーシングスケール ---
    public float SpacingXs { get; init; } = 4f;

    public float SpacingS { get; init; } = 8f;

    public float SpacingM { get; init; } = 16f;

    public float SpacingL { get; init; } = 24f;

    public float SpacingXl { get; init; } = 40f;

    /// <summary>Standard corner radius (px).</summary>
    public float Radius { get; init; } = 12f;

    // --- 影 / elevation（Material 風のドロップシャドウ。<see cref="Material"/>/<see cref="Card"/> が使う） ---

    /// <summary>Shadow color (translucent). Combined with <see cref="Material.Elevation"/> to determine the blur width and downward offset.</summary>
    public Color Shadow { get; init; } = new(28, 74, 102, 40);

    /// <summary>Shadow blur width added per elevation level (px): <c>2 + Elevation * ShadowBlurPerElevation</c>.</summary>
    public float ShadowBlurPerElevation { get; init; } = 2.2f;

    /// <summary>Downward shadow offset added per elevation level (px).</summary>
    public float ShadowOffsetPerElevation { get; init; } = 1.0f;

    // --- インタラクション状態のステートレイヤー不透明度（Material 準拠。Button/コントロール共通） ---

    /// <summary>Opacity applied in the disabled state (default 0.38).</summary>
    public float DisabledOpacity { get; init; } = 0.38f;

    /// <summary>Opacity of the hover state layer.</summary>
    public float HoverOverlay { get; init; } = 0.08f;

    /// <summary>Opacity of the focus state layer.</summary>
    public float FocusOverlay { get; init; } = 0.12f;

    /// <summary>Opacity of the pressed state layer.</summary>
    public float PressedOverlay { get; init; } = 0.24f;

    /// <summary>Opacity of the selected (on) state layer.</summary>
    public float SelectedOverlay { get; init; } = 0.16f;

    /// <summary>Tracking speed for state layer and scale animations (units per second).</summary>
    public float StateLayerRate { get; init; } = 18f;

    /// <summary>Default scroll physics (sensitivity, rubber-banding, inertia, etc.). Can be overridden per-widget via <c>Physics</c>.</summary>
    public ScrollPhysics ScrollPhysics { get; init; } = ScrollPhysics.Default;

    /// <summary>The default theme (Hamon Ripple).</summary>
    public static HamonTheme Default { get; } = new();

    /// <summary>Light color scheme (the default Ripple theme); same as <see cref="Default"/>.</summary>
    public static HamonTheme Light => Default;

    /// <summary>Built-in dark color scheme (Hamon Dark), for use with <see cref="HamonRoot.DarkTheme"/>.</summary>
    public static HamonTheme Dark { get; } = new()
    {
        Brightness = Brightness.Dark,
        Background = new Color(15, 17, 22),
        Surface = new Color(26, 29, 36),
        SurfaceVariant = new Color(40, 44, 54),
        Primary = new Color(134, 186, 255),
        OnPrimary = new Color(8, 24, 46),
        OnSurface = new Color(231, 235, 243),
        OnSurfaceVariant = new Color(158, 166, 180),
        Border = new Color(54, 60, 74),
        Danger = new Color(240, 110, 128),
        Shadow = new Color(0, 0, 0, 90),
    };
}
