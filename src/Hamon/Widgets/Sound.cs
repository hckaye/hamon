using System;

namespace Hamon.Widgets;

/// <summary>
/// Opaque identifier for sound effect (value type = no box). <c>enum→int</c>etc., and map the id to the actual sound.
/// （<see cref="ISoundPlayer"/>）。<b>"Do not ring"<see cref="Nullable{T}"/>（<c>SoundId?</c>represented by null)</b>
/// (0 is not treated specially = 0 is also a regular id).<c>int</c>It can be implicitly converted from<c>(int)Sfx.Click</c>can be handed over as is.
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
/// Sound effect playback abstraction (bridge to game audio). <b>have no specific sound</b>= As a drawing engine independent UI library,
/// Provide only the trigger (hover/press, etc.) for "when to ring."<paramref name="sound"/>teeth<see cref="SoundId"/>(value type = no box),
/// User side maps to actual sound (e.g.<c>id→SoundEffect</c>table).<see cref="HamonRoot.Sound"/>Inject into.
/// </summary>
public interface ISoundPlayer
{
    void Play(SoundId sound, float volume = 1f);
}

/// <summary>
/// Sound effects associated with widget interactions (declarative). <see cref="SoundId"/>(value type),<see cref="HamonRoot.Sound"/>via
/// will be played.<b>Parts that do not sound are null</b>（<c>SoundId?</c>).<c>Nullable</c>Since is a value type, no box is generated.
/// </summary>
public readonly struct InteractionSounds
{
    /// <summary>The sound to play when starting hover (null = no sound).</summary>
    public SoundId? Hover { get; init; }

    /// <summary>The sound to play when pressed (null = no sound).</summary>
    public SoundId? Press { get; init; }
}
