using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Reactive host abstraction (reconcile/state/scheduling services).<b>Does not depend on drawing, layout, or input</b>So,
/// MonoGame drawing<see cref="HamonRoot"/>as well as other backends (e.g. the retained view host that drives Unity uGUI).
/// Can be implemented.
/// （<see cref="BuildContext.Owner"/>is this type).
///
/// Implementation policy for non-visual hosts: create your own atom store (<see cref="GlobalStore"/>), dirty set, post queue, and ticker set,
/// In the update loop, send a post → advance the ticker → remove dirty elements<c>RebuildInPlace</c>Just turn "Rebuild with"
/// (Layout/drawing is not called = that is handled by the backend side = uGUI, etc.).
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
