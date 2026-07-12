using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Light and dark color scheme (Flutter<c>Brightness</c>compliant). </summary>
public enum Brightness
{
    /// <summary>Bright color scheme (light background).</summary>
    Light,

    /// <summary>Dark color scheme (dark background).</summary>
    Dark,
}

/// <summary>
/// Theme selection mode (Flutter<c>ThemeMode</c>compliant).<see cref="HamonRoot.ThemeMode"/>Specify with.
/// dark mode is<b>opt-in</b>:default<see cref="System"/>but<see cref="HamonRoot.DarkTheme"/>Will not darken unless set.
/// </summary>
public enum ThemeMode
{
    /// <summary>OS brightness (<see cref="HamonRoot.PlatformBrightness"/>) follows. <see cref="HamonRoot.DarkTheme"/>Only during configuration.</summary>
    System,

    /// <summary>Always light (<see cref="HamonRoot.Theme"/>）。</summary>
    Light,

    /// <summary>Always dark (<see cref="HamonRoot.DarkTheme"/>, if not set<see cref="HamonRoot.Theme"/>).</summary>
    Dark,
}

/// <summary>
/// Hamon's default UI style tokens (color, margins, font size, shadow).<see cref="BuildContext.Theme"/>It is propagated by
/// Each widget will take a default value from here unless the color is specified (= it can be used as is).<see cref="HamonRoot.Theme"/>Can be replaced with
/// A collection of material-like tokens. <see cref="Default"/>) = Bright and transparent light color scheme.
/// </summary>
public sealed class HamonTheme
{
    /// <summary>Light and dark of this color scheme (Flutter<c>ThemeData.brightness</c>equivalent). <see cref="Brightness.Light"/>。</summary>
    public Brightness Brightness { get; init; } = Brightness.Light;

    /// <summary>The backmost part of the screen (pale cool white like the surface of water).</summary>
    public Color Background { get; init; } = new(237, 245, 250);

    /// <summary>Card/panel side (default row). </summary>
    public Color Surface { get; init; } = new(255, 255, 255, 235);

    /// <summary>Surface variants (tracks/input fields/tonal buttons, etc.). </summary>
    public Color SurfaceVariant { get; init; } = new(224, 238, 245, 232);

    /// <summary>Primary accent (emphasis/selection/focus). </summary>
    public Color Primary { get; init; } = new(8, 157, 178);

    /// <summary><see cref="Primary"/>Top foreground (text/icon).</summary>
    public Color OnPrimary { get; init; } = new(255, 255, 255);

    /// <summary>Main text on surface (deep cool slate).</summary>
    public Color OnSurface { get; init; } = new(24, 42, 52);

    /// <summary>Weak text (placeholder/auxiliary) on surfaces.</summary>
    public Color OnSurfaceVariant { get; init; } = new(92, 116, 128);

    /// <summary>Borders/separators (soft cool translucent edges).</summary>
    public Color Border { get; init; } = new(188, 214, 224, 210);

    /// <summary>Destructive operations (such as deletion). </summary>
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

    /// <summary>Shadow color (translucent).<see cref="Material.Elevation"/>This determines the bleeding width and downward offset. </summary>
    public Color Shadow { get; init; } = new(28, 74, 102, 40);

    /// <summary>elevation Shadow blur width per level (px). <c>2 + Elevation * ShadowBlurPerElevation</c>。</summary>
    public float ShadowBlurPerElevation { get; init; } = 2.2f;

    /// <summary>elevation Downward offset of shadow per level (px).</summary>
    public float ShadowOffsetPerElevation { get; init; } = 1.0f;

    // --- インタラクション状態のステートレイヤー不透明度（Material 準拠。Button/コントロール共通） ---

    /// <summary>Opacity in disabled state (default 0.38). </summary>
    public float DisabledOpacity { get; init; } = 0.38f;

    /// <summary>hover state layer opacity.</summary>
    public float HoverOverlay { get; init; } = 0.08f;

    /// <summary>focus state layer opacity.</summary>
    public float FocusOverlay { get; init; } = 0.12f;

    /// <summary>pressed state layer opacity.</summary>
    public float PressedOverlay { get; init; } = 0.24f;

    /// <summary>selected (ON/selected) state layer opacity.</summary>
    public float SelectedOverlay { get; init; } = 0.16f;

    /// <summary>State layer/scale tracking speed (1/sec).</summary>
    public float StateLayerRate { get; init; } = 18f;

    /// <summary>A set of default values ​​for scroll movement (sensitivity/rubber band/inertia, etc.). <c>Physics</c>Can be overwritten individually.</summary>
    public ScrollPhysics ScrollPhysics { get; init; } = ScrollPhysics.Default;

    /// <summary>Default theme = Hamon Ripple. </summary>
    public static HamonTheme Default { get; } = new();

    /// <summary>Light color scheme (default ripple theme).<see cref="Default"/>).</summary>
    public static HamonTheme Light => Default;

    /// <summary>Traditional dark color scheme (Hamon Dark). <see cref="HamonRoot.DarkTheme"/>）。</summary>
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
