namespace SpellBrigadeArcane.Game;

using System;
using Il2CppInterop.Runtime;
using SpellBrigadeArcane.Core;

/// <summary>
/// Potion Belt (Alchemy's Mastery): a Health Potion picked up at full Health is kept and drunk automatically once Health
/// drops below 30%. The game says "health was full" in LocalEvents.OnHealthPotionCollected and, in the same call right
/// after it, the amount in HealthPickup.OnPickUp (which already includes Alchemy's bonus); we pair the two. Drinking uses
/// the potion's own heal call. Cleared when the mission's wizard changes; revives keep it.
/// </summary>
internal static class PotionBeltRunner
{
    private const float Interval = 0.1f;

    private static readonly PotionBelt Belt = new();
    private static Il2CppSystem.Action<HealthPotionCollectedEventArgs>? _onCollected;
    private static Il2CppSystem.Action<HealthPickUpEventData>? _onPickUp;
    private static IntPtr _collectedOn;
    private static IntPtr _pickUpOn;
    private static bool? _pendingFull;
    private static int _beltFor;
    private static float _next;

    public static void Tick(float now)
    {
        if (now < _next) return;
        _next = now + Interval;

        var identity = MissionBonuses.Identity;
        if (identity == null || !MissionBonuses.PotionBeltActive)
        {
            if (Belt.Stored > 0) ModLog.Info("PotionBelt: mission over, the stored potion is gone.");
            Belt.Clear();
            _beltFor = 0;
            return;
        }

        EnsureSubscribed();
        var id = identity.GetInstanceID();
        if (id != _beltFor)
        {
            Belt.Clear();
            _beltFor = id;
        }

        if (Belt.Stored == 0) return;
        var health = PlayerAccess.Health(identity);
        if (health == null || health.DepletionState != HealthDepletionState.Active) return;
        var current = health.CurrentHealth;
        var max = health.MaxHealth;
        var heal = Belt.TryDrink(current, max);
        if (heal == null) return;

        ModLog.Step("PotionBelt: drinking the stored potion");
        health.AddHealthFromPlayer(heal.Value, identity.OwnerClientId);
        ModLog.Info(FormattableString.Invariant($"PotionBelt: drank the stored potion at {current:0}/{max:0} Health (+{heal.Value:0})."));
    }

    /// <summary>Subscribes once to the game's two potion events (again if the game ever replaces an event object).</summary>
    private static void EnsureSubscribed()
    {
        var collected = LocalEvents.OnHealthPotionCollected;
        var pickUp = HealthPickup.OnPickUp;
        if (collected == null || pickUp == null) return;
        if (collected.Pointer == _collectedOn && pickUp.Pointer == _pickUpOn) return;

        ModLog.Step("PotionBelt: subscribing to the game's potion events");
        _onCollected ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<HealthPotionCollectedEventArgs>>(new Action<HealthPotionCollectedEventArgs>(OnCollected))
            ?? throw new InvalidOperationException("DelegateSupport returned no delegate");
        _onPickUp ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<HealthPickUpEventData>>(new Action<HealthPickUpEventData>(OnPickUp))
            ?? throw new InvalidOperationException("DelegateSupport returned no delegate");
        if (collected.Pointer != _collectedOn)
        {
            collected.Subscribe(_onCollected);
            _collectedOn = collected.Pointer;
        }

        if (pickUp.Pointer != _pickUpOn)
        {
            pickUp.Subscribe(_onPickUp);
            _pickUpOn = pickUp.Pointer;
        }

        ModLog.Info("PotionBelt: listening for Health Potions.");
    }

    /// <summary>Only on the collecting player's PC, after the heal: remembers whether Health was full.</summary>
    private static void OnCollected(HealthPotionCollectedEventArgs args)
    {
        try
        {
            _pendingFull = args?.WasHealthFullOnCollection;
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("potion-collected", ex, UnityEngine.Time.unscaledTime);
        }
    }

    /// <summary>On every PC for every player's potion, right after OnCollected: stores ours if Health was full.</summary>
    private static void OnPickUp(HealthPickUpEventData data)
    {
        try
        {
            var full = _pendingFull;
            _pendingFull = null;
            if (full == null || data == null) return;
            var identity = MissionBonuses.Identity;
            if (identity == null || !MissionBonuses.PotionBeltActive || data.PickedUpByClientId != identity.OwnerClientId) return;
            if (Belt.OnPotionCollected(full.Value, data.AmountOfHealth))
                ModLog.Info(FormattableString.Invariant($"PotionBelt: stored a Health Potion (+{data.AmountOfHealth:0} Health) picked up at full Health."));
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("potion-pickup", ex, UnityEngine.Time.unscaledTime);
        }
    }
}
