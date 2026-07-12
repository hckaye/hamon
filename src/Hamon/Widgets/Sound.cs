using System;

namespace Hamon.Widgets;

/// <summary>
/// An opaque identifier for a sound effect (a value type = no boxing), e.g. cast from an <c>enum</c> to
/// <c>int</c>; the id is then mapped to the actual sound elsewhere (<see cref="ISoundPlayer"/>).
/// <b>"Do not play" is represented via <see cref="Nullable{T}"/> (<c>SoundId?</c> = null)</b> — 0 is not treated
/// specially (0 is a regular id too). Implicitly convertible from <c>int</c>, so you can pass <c>(int)Sfx.Click</c>
/// directly.
/// </summary>
public readonly struct SoundId : IEquatable<SoundId>
{
    public SoundId(int value) => Value = value;

    public int Value { get; }

    public static implicit operator SoundId(int value) => new(value);

    public bool Equals(SoundId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is SoundId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(SoundId a, SoundId b) => a.Value == b.Value;

    public static bool operator !=(SoundId a, SoundId b) => a.Value != b.Value;

    public override string ToString() => $"SoundId({Value})";
}

/// <summary>
/// Sound-effect playback abstraction (a bridge to game audio). <b>Has no knowledge of specific sounds</b> — as a
/// rendering-engine-independent UI library, Hamon only provides the trigger for "when to play" (hover, press,
/// etc.). <paramref name="sound"/> is a <see cref="SoundId"/> (value type = no boxing); the user side maps it to
/// the actual sound (e.g. an id-to-SoundEffect table). Inject an implementation via <see cref="HamonRoot.Sound"/>.
/// </summary>
public interface ISoundPlayer
{
    void Play(SoundId sound, float volume = 1f);
}

/// <summary>
/// Sound effects associated with widget interactions (declarative). Uses <see cref="SoundId"/> (value type) and is
/// played via <see cref="HamonRoot.Sound"/>. <b>Interactions that should not play a sound are left null</b>
/// (<c>SoundId?</c>) — since <c>Nullable&lt;T&gt;</c> is a value type, no boxing occurs.
/// </summary>
public readonly struct InteractionSounds
{
    /// <summary>The sound to play when starting hover (null = no sound).</summary>
    public SoundId? Hover { get; init; }

    /// <summary>The sound to play when pressed (null = no sound).</summary>
    public SoundId? Press { get; init; }
}
