using Raylib_cs;

/// <summary>
/// An activatable ability bound to a number key. Pressing the key turns it on for <see cref="Duration"/> seconds
/// (pressing again cuts it short), after which it sits on cooldown. The HUD lists every power in the bottom centre.
/// </summary>
abstract class Power(KeyboardKey key, string keyLabel, string name, float duration, float cooldown)
{
    public KeyboardKey Key => key;
    public string KeyLabel => keyLabel;
    public string Name => name;
    public float Duration => duration;
    public float Cooldown => cooldown;

    float activeLeft;
    float cooldownLeft;

    public bool IsActive => activeLeft > 0;
    public bool IsReady => !IsActive && cooldownLeft <= 0;
    /// <summary>1 when just activated, shrinking to 0 as the effect runs out.</summary>
    public float ActiveFraction => Math.Clamp(activeLeft / duration, 0f, 1f);
    /// <summary>1 when the cooldown just started, shrinking to 0 when the power is ready again.</summary>
    public float CooldownFraction => Math.Clamp(cooldownLeft / cooldown, 0f, 1f);

    /// <summary>Key press handler: starts the effect when ready, or ends it early while it is running.</summary>
    public void Toggle()
    {
        if (IsActive) End();
        else if (IsReady) activeLeft = duration;
    }

    void End()
    {
        activeLeft = 0;
        cooldownLeft = cooldown;
    }

    /// <param name="dt">Real time: a power's own clock must not be slowed by the powers that warp the world.</param>
    public void Update(float dt)
    {
        if (IsActive)
        {
            activeLeft -= dt;
            if (activeLeft <= 0) End();
        }
        else if (cooldownLeft > 0)
        {
            cooldownLeft = Math.Max(0, cooldownLeft - dt);
        }
    }

    public void Reset()
    {
        activeLeft = 0;
        cooldownLeft = 0;
    }

    /// <summary>Draws the power's icon centred in <paramref name="slot"/>; <paramref name="tint"/> is dimmed while it is unavailable.</summary>
    public abstract void DrawIcon(Rectangle slot, Color tint);
}

/// <summary>[1] Everything but the player runs at half speed: enemies, their shots and the popups.</summary>
class SlowTimePower() : Power(KeyboardKey.One, "1", "Slow time", duration: 5f, cooldown: 10f)
{
    public const float Scale = 0.5f;

    public override void DrawIcon(Rectangle slot, Color tint) => HudIcons.DrawClock(slot, tint);
}
