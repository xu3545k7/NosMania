using UnityEngine;

/// <summary>
/// The last thing that happens to a velocity before it becomes sound: lifting
/// the quiet end into a range that can actually be heard.
/// </summary>
/// <remarks>
/// **Why anything is needed.** The bank is faithful to a real piano, and a real
/// piano at velocity 16 is very nearly silent: the softest band is rendered at
/// velocity 18 and peaks at −35.9 dBFS, and the measured curve then attenuates
/// inside the band on top of that (velocity 1 sits at gain 0.0064, about −44 dB
/// under its own band's reference). Under headphones in a quiet room that is
/// music; over gameplay, with the song playing underneath, those notes simply
/// are not there. <see cref="SettingsManager.PianoDynamicExpansion"/> makes it
/// worse by design — it pushes everything below the chart's middle further down.
///
/// **What this does.** Squeezes the whole velocity range into
/// [<see cref="MinSoundingVelocity"/>, 127] instead of [1, 127], so the softest
/// note lands where the bank still speaks. The map is linear and monotonic, so
/// louder is still louder and the shape of a phrase survives; it just starts
/// from a floor. Full velocity is untouched.
///
///     1 → 37    16 → 48    32 → 59    64 → 82    100 → 108    127 → 127
///
/// **Why a constant and not a setting.** It was chosen by ear (the alternative
/// tried was a floor of 64, which flattened the dynamics into "every note the
/// same"). A serialized field would also not reach the player: the settings
/// asset already saved on disk keeps whatever default it was created with.
///
/// **Where it is applied.** <see cref="PianoVoiceManager"/>, at the two points
/// where a velocity turns into a sample: the note-on itself and the prewarm that
/// decides which clips to load. Both have to agree, or every soft note would be
/// played from a clip that was never warmed. Nothing else sees it — scoring, the
/// dynamics wash and the note shells all keep reading the chart's own numbers.
/// </remarks>
public static class PianoDynamics
{
    /// <summary>Where velocity 1 ends up. The softest velocity the bank plays properly.</summary>
    public const int MinSoundingVelocity = 37;

    /// <summary>Maps a 1-127 velocity onto MinSoundingVelocity-127.</summary>
    public static int Lift(int velocity)
    {
        velocity = Mathf.Clamp(velocity, 1, 127);
        const int span = 127 - MinSoundingVelocity;
        return Mathf.Clamp(
            Mathf.RoundToInt(MinSoundingVelocity + span * (velocity - 1) / 126f), 1, 127);
    }
}
