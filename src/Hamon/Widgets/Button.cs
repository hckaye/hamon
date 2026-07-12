using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A button that can be pressed (tap via pointer, or activate via OK on gamepad/keyboard).
/// Feedback is expressed using the Material Design <b>state layer</b> pattern: focus and press are
/// conveyed by overlay <b>color</b>, not by a border. The overlay color darkens while the button is
/// pressed, making the click easier to see. To preserve focus state across rebuilds, <see cref="Node"/>
/// should be retained by the caller rather than recreated each time.
/// </summary>
public sealed class Button : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public Action? OnPressed { get; init; }

    /// <summary>Background color (unspecified = transparent, i.e. a text button; the state layer then appears in the theme's primary color).</summary>
    public Color? Background { get; init; }

    public EdgeInsets Padding { get; init; }

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    /// <summary>Corner radius in pixels (falls back to the theme default, <see cref="HamonTheme.Radius"/>, if not specified).</summary>
    public float? Radius { get; init; }

    /// <summary>Whether the button is operable (false = ignores input, is displayed dimmed, and does not receive activation from pointer/keyboard/gamepad).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Whether the button is included in focus traversal even when disabled (default false, matching
    /// Flutter's behavior of excluding it). When true, the button can still receive focus but will not
    /// activate, which is useful for indicating "disabled but still present" for accessibility.
    /// </summary>
    public bool FocusableWhenDisabled { get; init; }

    /// <summary>Explicitly specifies the background color when disabled (if unspecified, the normal background is used with the theme's disabled opacity applied).</summary>
    public Color? DisabledBackground { get; init; }

    /// <summary>Cursor to show while hovering (default = clickable). If <see cref="ButtonStyle.MouseCursor"/> is specified, it takes priority.</summary>
    public MouseCursor Cursor { get; init; } = MouseCursor.Click;

    /// <summary>Accessibility label read by screen readers (if not specified, the consumer fills it in from the child's text).</summary>
    public string? SemanticLabel { get; init; }

    /// <summary>Appearance by state (equivalent to Flutter's <c>ButtonStyle</c>).</summary>
    public ButtonStyle? Style { get; init; }

    /// <summary>
    /// Builder that <b>assembles the entire look</b> based on the current <see cref="WidgetState"/> (a Hamon-specific
    /// escape hatch). When specified, the default background/state-layer/border drawing is skipped, and only this
    /// builder's output is drawn, giving the user complete control. For standard usage, <see cref="Style"/>
    /// (Flutter-compliant) is sufficient; use this only when a special appearance is needed.
    /// </summary>
    public Func<WidgetState, Widget>? Builder { get; init; }

    /// <summary>
    /// Callback invoked with the new state every time the state (hover/pressed/focused/disabled)
    /// changes (intended for games). The user can play sound effects, or drive a retained
    /// <see cref="AnimationController"/> for custom animations (squash/glow/sprite, etc.), typically in
    /// combination with <see cref="Builder"/>. For the classic scale/opacity transition, use
    /// <see cref="ButtonStyle.Animation"/> (<see cref="ButtonAnimationStyle"/>) instead.
    /// </summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    /// <summary>Sound effects (hover/press) linked to interactions. Played via <see cref="HamonRoot.Sound"/> (silent/no-op if not injected).</summary>
    public InteractionSounds Sounds { get; init; }

    // 基底状態（無効/有効）でレイアウト用の値を解決する（hover/focus による padding 変化はレイアウトを揺らすため基底で固定）。
    private WidgetState LayoutState => Enabled ? WidgetState.None : WidgetState.Disabled;

    private EdgeInsets EffectivePadding => Style?.Padding?.Resolve(LayoutState) ?? Padding;

    // --- IRenderConfig（レイアウトは Box＝padding を内側に・子は中央寄せ。背景/オーバーレイは要素が自前で描く） ---
    Style IRenderConfig.Style
    {
        get
        {
            var style = new Style { Kind = LayoutKind.Box, ChildAlign = Alignment.Center, Padding = EffectivePadding };
            if (Style is { } s)
            {
                // MinimumSize/MaximumSize/FixedSize をレイアウト制約へ反映。
                if (s.FixedSize is Size fixedSize)
                {
                    style = style with { Width = Dimension.Px(fixedSize.Width), Height = Dimension.Px(fixedSize.Height) };
                }

                if (s.MinimumSize is Size min)
                {
                    style = style with { MinWidth = Dimension.Px(min.Width), MinHeight = Dimension.Px(min.Height) };
                }

                if (s.MaximumSize is Size max)
                {
                    style = style with { MaxWidth = Dimension.Px(max.Width), MaxHeight = Dimension.Px(max.Height) };
                }
            }

            return style;
        }
    }

    // Builder 指定時は子を ButtonElement が状態に応じて作る（静的 Child は使わない）。
    IReadOnlyList<Widget>? IRenderConfig.Children => Builder is not null || Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null;

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new ButtonElement(this);

    /// <summary>
    /// A simple helper that builds a style from commonly used constant values (equivalent to Flutter's
    /// <c>ButtonStyle.styleFrom</c>). Values are applied to all states via <see cref="WidgetStateProperty{T}.All"/>
    /// and assembled directly into a <see cref="ButtonStyle"/>.
    /// </summary>
    public static ButtonStyle StyleFrom(
        Color? background = null,
        Color? foreground = null,
        Color? overlay = null,
        float? radius = null,
        EdgeInsets? padding = null,
        BorderSide? side = null,
        MouseCursor? cursor = null,
        Size? minimumSize = null,
        Size? fixedSize = null,
        Size? maximumSize = null,
        ButtonAnimationStyle? animation = null) => new()
        {
            BackgroundColor = background is { } bg ? WidgetStateProperty<Color?>.All(bg) : null,
            ForegroundColor = foreground is { } fg ? WidgetStateProperty<Color?>.All(fg) : null,
            OverlayColor = overlay is { } ov ? WidgetStateProperty<Color?>.All(ov) : null,
            Radius = radius is { } r ? WidgetStateProperty<float?>.All(r) : null,
            Padding = padding is { } p ? WidgetStateProperty<EdgeInsets?>.All(p) : null,
            Side = side is { } sd ? WidgetStateProperty<BorderSide?>.All(sd) : null,
            MouseCursor = cursor is { } c ? WidgetStateProperty<MouseCursor?>.All(c) : null,
            MinimumSize = minimumSize,
            FixedSize = fixedSize,
            MaximumSize = maximumSize,
            Animation = animation,
        };
}

/// <summary>
/// The <see cref="RenderElement"/> that holds a <see cref="Button"/>. Responsible for focus registration,
/// press detection, and drawing/animating the state layer (expressing focus/pressed via color). Implements
/// <see cref="ITicker"/> to create a smooth transition between overlay colors.
/// </summary>
internal sealed class ButtonElement : RenderElement, ITicker, IHoverTarget
{
    private FocusNode? _node;
    private IHamonHost? _owner;
    private bool _pressed;         // ポインタ押下中（アリーナに奪われていない）
    private bool _inside;          // ポインタがボタン内にあるか（プレス表示の点灯条件）
    private bool _hovered;         // マウス hover 中
    private bool _ticking;
    private float _overlay;        // 現在の重ね色の不透明度（0..1）
    private float _targetOverlay;  // 目標（hover/focus/pressed/idle）
    private float _scale = 1f;     // 現在のスケール（押下/hover/focus アニメ）
    private float _targetScale = 1f;
    private float _opacity = 1f;   // 現在の不透明度（押下/hover/focus アニメ）
    private float _targetOpacity = 1f;

    // curve モード（ButtonAnimationStyle.Curve 指定時）：from→to を Duration 秒で曲線補間。
    private float _progress = 1f;  // 0..1
    private float _fromOverlay;
    private float _fromScale = 1f;
    private float _fromOpacity = 1f;

    private WidgetState _lastState = WidgetState.None; // OnStateChanged の発火判定用

    public ButtonElement(Button widget)
        : base(widget)
    {
    }

    /// <summary>Computes the current state flags (hover/focus/pressed/disabled).</summary>
    private WidgetState CurrentState()
    {
        if (!W.Enabled)
        {
            return WidgetState.Disabled;
        }

        WidgetState s = WidgetState.None;
        if (_hovered)
        {
            s |= WidgetState.Hovered;
        }

        if (_node?.HasFocus == true)
        {
            s |= WidgetState.Focused;
        }

        if (_pressed && _inside)
        {
            s |= WidgetState.Pressed;
        }

        return s;
    }

    private float OverlayRate => W.Style?.Animation?.Rate ?? Context.Theme.StateLayerRate;

    // 無効時はポインタを受けない＝押下/フォーカス移動の対象外（hit-test も透過）。
    public override bool WantsPointer => W.Enabled;

    // タップ（ポインタ Down）でこのボタンへフォーカスを移すために公開（DispatchPointer が祖先 FocusNode を探す）。
    // 無効でも FocusableWhenDisabled ならフォーカスは当たる（activate はしない）。
    internal override FocusNode? FocusNodeOrNull => CanFocus ? _node : null;

    bool IHoverTarget.HoverOpaque => true;

    MouseCursor IHoverTarget.HoverCursor => W.Enabled
        ? (W.Style?.MouseCursor?.Resolve(CurrentState()) ?? W.Cursor)
        : (W.Style?.MouseCursor?.Resolve(WidgetState.Disabled) ?? MouseCursor.Forbidden);

    private Button W => (Button)Widget;

    /// <summary>Whether the button is focusable (enabled, or disabled but still included in traversal).</summary>
    private bool CanFocus => W.Enabled || W.FocusableWhenDisabled;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _owner = context.Owner;

        _node = W.Node;
        _node.CanRequestFocus = CanFocus;
        _node.SemanticLabel = W.SemanticLabel;
        _node.OnActivate = () =>
        {
            if (W.Enabled)
            {
                W.OnPressed?.Invoke(); // ゲームパッド OK / Enter（無効時は無反応）
            }
        };
        _node.OnFocusChange = _ => Retarget();
        if (context.Focusable && CanFocus)
        {
            context.Focus?.Register(_node, () => LayoutNode.Bounds, EnclosingScope(), EnclosingScrollable(), context.Traversable);
            if (W.Autofocus && W.Enabled)
            {
                context.Focus?.AutofocusIfNone(_node);
            }
        }

        BuildBuilderChild();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        if (_node is not null)
        {
            _node.CanRequestFocus = CanFocus;
            Retarget(); // Enabled 変化で重ね色目標が変わりうる
        }

        BuildBuilderChild();
    }

    internal override void RebuildInPlace() => BuildBuilderChild();

    // Builder 指定時、現在状態で子を組み直す（状態変化＝Retarget で MarkElementDirty 経由で呼ばれる）。
    private void BuildBuilderChild()
    {
        if (W.Builder is { } builder)
        {
            UpdateChildren(new[] { builder(CurrentState()) });
        }
    }

    public override void Unmount()
    {
        if (_node is not null)
        {
            Context.Focus?.Unregister(_node);
        }

        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);

        if (_ticking)
        {
            _owner?.UnregisterTicker(this);
            _ticking = false;
        }

        base.Unmount();
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (!W.Enabled)
        {
            return;
        }

        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                _pressed = true;
                _inside = true;
                Retarget();
                break;

            case PointerPhase.Move when _pressed:
                // 自前で取り消さない（スクロール等への移譲はアリーナの Cancel が担う）。範囲外なら表示だけ消す。
                bool inside = LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y);
                if (inside != _inside)
                {
                    _inside = inside;
                    Retarget();
                }

                break;

            case PointerPhase.Up:
                if (_pressed && LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y))
                {
                    W.OnPressed?.Invoke();
                }

                _pressed = false;
                _inside = false;
                Retarget();
                break;

            case PointerPhase.Cancel:
                _pressed = false;
                _inside = false;
                Retarget();
                break;
        }
    }

    void IHoverTarget.HoverEnter(Vec2 position)
    {
        if (W.Enabled && !_hovered)
        {
            _hovered = true;
            Retarget();
        }
    }

    void IHoverTarget.HoverMove(Vec2 position)
    {
    }

    void IHoverTarget.HoverExit(Vec2 position)
    {
        if (_hovered)
        {
            _hovered = false;
            Retarget();
        }
    }

    public override void Paint(in PaintContext context)
    {
        Button w = W;
        HamonTheme theme = Context.Theme;
        Rect bounds = LayoutNode.Bounds;
        WidgetState state = CurrentState();
        float radius = w.Style?.Radius?.Resolve(state) ?? w.Radius ?? theme.Radius;

        // スケールアニメ（押下/hover/focus）はボタン中心ピボットで部分木に積む。
        PaintContext ctx = context;
        if (MathF.Abs(_scale - 1f) > 0.0005f)
        {
            var pivot = new Vec2(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
            ctx = context.WithTransform(Transform2D.ScaleAbout(new Vec2(_scale, _scale), pivot, Vec2.Zero));
        }

        // 不透明度アニメ（押下/hover/focus）をボタン全体へ。
        if (_opacity < 0.999f)
        {
            ctx = ctx.WithOpacity(_opacity);
        }

        // Builder（escape hatch）指定時は既定描画をせず、組まれた子だけを（スケール付きで）描く。
        if (w.Builder is not null)
        {
            PaintChildren(ctx);
            return;
        }

        // 背景画像スキン（状態別・ゲーム向け）。指定状態では色背景/ステートレイヤーの代わりに描く（スプライトが状態を表す）。
        if (w.Style?.BackgroundImage?.Resolve(state) is ImageSkin skin && skin.HasValue)
        {
            skin.Paint(ctx, bounds);
            PaintChildren(ctx);
            return;
        }

        Color? background = w.Style?.BackgroundColor?.Resolve(state) ?? w.Background;

        if (!w.Enabled)
        {
            PaintDisabled(ctx, w, theme, bounds, radius, background);
            return;
        }

        if (background is Color bgColor)
        {
            ctx.FillRoundedRect(bounds, bgColor, radius);
        }

        if (_overlay > 0.001f)
        {
            // ステートレイヤー色：スタイル指定があればそれ、無ければ背景の明度から黒/白（背景無しは Primary）。
            Color layer = w.Style?.OverlayColor?.Resolve(state)
                ?? (background is Color bg ? ContrastOverlay(bg) : theme.Primary);
            var tinted = new Color(layer.R, layer.G, layer.B, (int)Math.Clamp(_overlay * 255f, 0f, 255f));
            ctx.FillRoundedRect(bounds, tinted, radius);
        }

        if (w.Style?.Side?.Resolve(state) is BorderSide side && side.Width > 0f)
        {
            ctx.DrawOutline(bounds, side.Color, side.Width);
        }

        PaintChildren(ctx);
    }

    // 無効時の描画：DisabledBackground＞Style の disabled 背景＞通常背景＋子をテーマの淡色不透明度で。
    private void PaintDisabled(in PaintContext context, Button w, HamonTheme theme, Rect bounds, float radius, Color? background)
    {
        if (w.DisabledBackground is Color disabled)
        {
            context.FillRoundedRect(bounds, disabled, radius);
            DrawDisabledBorder(context, w, bounds);
            PaintChildren(context);
            return;
        }

        PaintContext dim = context.WithOpacity(theme.DisabledOpacity);
        if (background is Color bg)
        {
            dim.FillRoundedRect(bounds, bg, radius);
        }

        DrawDisabledBorder(dim, w, bounds);
        PaintChildren(dim);
    }

    private static void DrawDisabledBorder(in PaintContext context, Button w, Rect bounds)
    {
        if (w.Style?.Side?.Resolve(WidgetState.Disabled) is BorderSide side && side.Width > 0f)
        {
            context.DrawOutline(bounds, side.Color, side.Width);
        }
    }

    private void PaintChildren(in PaintContext context)
    {
        IReadOnlyList<Element> children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            children[i].Paint(context);
        }
    }

    public bool Tick(float dt)
    {
        if (dt > 0f)
        {
            ButtonAnimationStyle? anim = W.Style?.Animation;
            if (anim?.Curve is { } curve)
            {
                // 曲線補間（from→to を Duration 秒）。
                float duration = anim.Duration > 0f ? anim.Duration : 0.0001f;
                _progress = MathF.Min(1f, _progress + (dt / duration));
                float t = curve(_progress);
                _overlay = _fromOverlay + ((_targetOverlay - _fromOverlay) * t);
                _scale = _fromScale + ((_targetScale - _fromScale) * t);
                _opacity = _fromOpacity + ((_targetOpacity - _fromOpacity) * t);
                if (_progress >= 1f)
                {
                    _ticking = false;
                }
            }
            else
            {
                float k = 1f - MathF.Exp(-dt * OverlayRate); // 指数追従（滑らか）
                _overlay += (_targetOverlay - _overlay) * k;
                _scale += (_targetScale - _scale) * k;
                _opacity += (_targetOpacity - _opacity) * k;
                if (MathF.Abs(_targetOverlay - _overlay) < 0.004f && MathF.Abs(_targetScale - _scale) < 0.001f && MathF.Abs(_targetOpacity - _opacity) < 0.001f)
                {
                    _overlay = _targetOverlay;
                    _scale = _targetScale;
                    _opacity = _targetOpacity;
                    _ticking = false;
                }
            }

            _owner?.MarkElementDirty(this); // 再描画
        }

        return _ticking;
    }

    private void Retarget()
    {
        HamonTheme theme = Context.Theme;
        ButtonAnimationStyle? anim = W.Style?.Animation;

        // 状態が変わったら通知（効果音/カスタムアニメ駆動の起点）。Builder の組み直しや scale/opacity 遷移とは独立に呼ぶ。
        WidgetState now = CurrentState();
        if (now != _lastState)
        {
            WidgetState gained = now & ~_lastState;
            _lastState = now;
            W.OnStateChanged?.Invoke(now);

            if (gained.Has(WidgetState.Hovered))
            {
                (Context.Owner as HamonRoot)?.PlaySound(W.Sounds.Hover);
            }

            if (gained.Has(WidgetState.Pressed))
            {
                (Context.Owner as HamonRoot)?.PlaySound(W.Sounds.Press);
            }
        }

        if (!W.Enabled)
        {
            _targetOverlay = 0f; // 無効時はステートレイヤーを出さない（淡色表示のみ）
            _targetScale = 1f;
            _targetOpacity = 1f;
        }
        else if (_pressed && _inside)
        {
            _targetOverlay = theme.PressedOverlay;
            _targetScale = anim?.PressedScale ?? 1f;
            _targetOpacity = anim?.PressedOpacity ?? 1f;
        }
        else
        {
            // hover と focus は重ねる（Material のステートレイヤー加算）。クランプして濃くなりすぎないように。
            float target = 0f;
            float scale = 1f;
            float opacity = 1f;
            if (_hovered)
            {
                target += theme.HoverOverlay;
                scale = anim?.HoveredScale ?? scale;
                opacity = anim?.HoveredOpacity ?? opacity;
            }

            if (_node?.HasFocus == true)
            {
                target += theme.FocusOverlay;
                scale = anim?.FocusedScale ?? scale;
                opacity = anim?.FocusedOpacity ?? opacity;
            }

            _targetOverlay = MathF.Min(target, theme.PressedOverlay);
            _targetScale = scale;
            _targetOpacity = opacity;
        }

        bool changed = MathF.Abs(_targetOverlay - _overlay) > 0.001f || MathF.Abs(_targetScale - _scale) > 0.0005f || MathF.Abs(_targetOpacity - _opacity) > 0.0005f;
        if (changed && anim?.Curve is not null)
        {
            // 曲線モード：現在地を始点に取り直して進捗をリセット（途中変化でも滑らかに）。
            _fromOverlay = _overlay;
            _fromScale = _scale;
            _fromOpacity = _opacity;
            _progress = 0f;
        }

        if (changed && !_ticking && _owner is not null)
        {
            _ticking = true;
            _owner.RegisterTicker(this);
        }

        if (W.Builder is not null)
        {
            _owner?.MarkElementDirty(this); // 状態が変わったので次フレームで子を組み直す
        }
    }

    // 背景の相対輝度で重ね色を決める：明るい背景には黒、暗い背景には白を薄く重ねる。
    private static Color ContrastOverlay(Color bg)
    {
        float luminance = ((0.299f * bg.R) + (0.587f * bg.G) + (0.114f * bg.B)) / 255f;
        return luminance > 0.5f ? new Color(0, 0, 0) : new Color(255, 255, 255);
    }
}
