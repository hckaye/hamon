using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// The platform text input/IME bridging abstraction (the backend implements this and injects it via
/// <see cref="HamonRoot.TextInput"/>). Notifies the OS IME of the focused text field's state: input start/end (to
/// enable/disable the IME or soft keyboard) and the candidate window position (caret rectangle). Composition and
/// committed text from the IME are fed back into the core via <see cref="HamonRoot.DispatchComposition"/> and
/// <see cref="HamonRoot.DispatchText"/>. On desktop this is implemented with SDL
/// (<c>SDL_StartTextInput</c>/<c>SDL_SetTextInputRect</c> plus TEXTEDITING event monitoring); on mobile it is
/// implemented via the soft keyboard.
/// </summary>
public interface ITextInput
{
    /// <summary>Start entering text (IME enabled/soft keyboard displayed on mobile).</summary>
    void Start();

    /// <summary>Finish text input.</summary>
    void Stop();

    /// <summary>Tells the absolute rectangle of the caret (the display position of the IME candidate window).</summary>
    void SetCaretRect(Rect caret);
}
