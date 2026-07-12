using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// The reactive host abstraction (reconcile/state/scheduling services). <b>It does not depend on drawing, layout, or
/// input</b>, so it can be implemented by MonoGame-based <see cref="HamonRoot"/> as well as by other backends (e.g. a
/// retained-view host driving Unity uGUI). This is the type exposed as <see cref="BuildContext.Owner"/>.
///
/// Implementation policy for non-visual hosts: create your own atom store (<see cref="GlobalStore"/>), dirty set,
/// post queue, and ticker set. In the update loop, drain the post queue, advance the tickers, and then rebuild each
/// dirty element by calling <c>RebuildInPlace</c> (layout and drawing are not invoked here — that is handled by the
/// backend side, e.g. uGUI).
/// </summary>
public interface IHamonHost
{
    /// <summary>Request a rebuild of the entire tree (reconciliate on next frame).</summary>
    void MarkDirty();

    /// <summary>Locally rebuild only specified elements (targeted update of hooks/Bind).</summary>
    void MarkElementDirty(Element element);

    /// <summary>Post an action to run on the UI thread (marshalling async continuations).</summary>
    void Post(Action action);

    /// <summary>Register a ticker (anime, etc.) that advances with each update.</summary>
    void RegisterTicker(ITicker ticker);

    /// <summary>Unregister ticker.</summary>
    void UnregisterTicker(ITicker ticker);

    /// <summary>Generate an animation controller driven by this host.</summary>
    AnimationController CreateAnimation(float durationSeconds, Curve? curve = null);

    /// <summary>Global atom store (default store when Provider/StoreProvider is not specified).</summary>
    AtomStore GlobalStore { get; }

    /// <summary>Is the focus cursor display enabled (when enabled, the focus ring is suppressed)? </summary>
    bool CursorEnabled { get; }

    /// <summary>Start text input (when text field gets focus; IME/soft keyboard enabled).</summary>
    void BeginTextInput();

    /// <summary>End text input (when it loses focus).</summary>
    void EndTextInput();

    /// <summary>Conveys the caret absolute rectangle of the focused field (IME candidate window position).</summary>
    void SetTextInputCaret(Rect caret);
}
