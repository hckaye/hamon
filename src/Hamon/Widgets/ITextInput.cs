using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Platform text input/IME bridging abstraction (backend implements<see cref="HamonRoot.TextInput"/>injection).
/// Notify the state of the focused text field to the OS IME: input start/end (IME/soft keyboard enablement)
/// Candidate window position (caret rectangle).
/// <see cref="HamonRoot.DispatchComposition"/>/<see cref="HamonRoot.DispatchText"/>and send it to the core.
/// Desktop = SDL (<c>SDL_StartTextInput</c>/<c>SDL_SetTextInputRect</c>+ TEXTEDITING monitoring), implemented with mobile = soft keyboard.
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
