namespace SpellBrigadeArcane.Game;

using System;
using System.Reflection;
using HarmonyLib;
using SpellBrigadeArcane.Core;
using UnityEngine;

/// <summary>
/// Long Shot (Reach's Mastery): the mod's only patch. One Harmony prefix on
/// <c>EnemyHealthContainer.TakeDamage(float amount, DamageInfo damageInfo, bool hitIsCritical)</c> multiplies the damage
/// of the local player's direct spell hits by <see cref="LongShot"/> of the distance wizard → enemy. That method is
/// where the game hard-codes its own Damage Mastery; burn ticks and ricochets don't pass through it.
/// Installed only in the first mission that has Long Shot, only if <see cref="NativeCode.CanPatch"/> accepts the
/// method and [General] LongShot is on. As a co-op client it only changes the damage popup: enemy health is the host's.
/// </summary>
internal static class LongShotHook
{
    private const int MaxErrors = 3;
    private const int LoggedHits = 5;

    private static bool _installTried;
    private static bool _installed;
    private static bool _active;
    private static ulong _localClientId;
    private static Vector3 _wizard;
    private static int _errors;
    private static int _hits;

    /// <summary>Every frame: decides whether Long Shot applies right now and caches what the prefix needs.</summary>
    public static void Update()
    {
        var identity = MissionBonuses.Identity;
        var wanted = identity != null && MissionBonuses.LongShotActive && ModConfig.LongShot.Value && _errors < MaxErrors;
        if (!wanted)
        {
            if (_active && _hits > 0) ModLog.Info($"LongShot: {_hits} hit(s) scaled this mission.");
            _active = false;
            _hits = 0;
            return;
        }

        if (!_installTried) Install();
        if (!_installed) return;
        var body = identity!.currentGameplayPlayer;
        if (body == null)
        {
            _active = false; // down or between bodies: no wizard position
            return;
        }

        _wizard = body.transform.position;
        _localClientId = identity.OwnerClientId;
        _active = true;
    }

    /// <summary>Installs the prefix once, behind the patch guard.</summary>
    private static void Install()
    {
        _installTried = true;
        const string label = "EnemyHealthContainer.TakeDamage(float, DamageInfo, bool)";
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var method = typeof(EnemyHealthContainer).GetMethod(nameof(EnemyHealthContainer.TakeDamage), flags, null,
                new[] { typeof(float), typeof(DamageInfo), typeof(bool) }, null);
            if (method == null)
            {
                ModLog.Warn($"LongShot: {label} not found in this game version; Long Shot is off.");
                return;
            }

            if (!NativeCode.CanPatch(method, label, out var why))
            {
                ModLog.Warn($"LongShot: not patching {label}: {why}. Long Shot is off.");
                return;
            }

            ModLog.Step($"LongShot: patching {label} ({why})");
            Plugin.Harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(LongShotHook), nameof(Prefix))));
            ModLog.Step("LongShot: patch ok");
            _installed = true;
            ModLog.Info($"LongShot: installed on {label} ({why}).");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"LongShot: could not patch {label}: {ex.GetType().Name}: {ex.Message}. Long Shot is off.");
        }
    }

    /// <summary>Runs before every enemy TakeDamage on this PC. Must never throw and never block the original.</summary>
    private static void Prefix(EnemyHealthContainer __instance, ref float amount, DamageInfo damageInfo)
    {
        if (!_active) return;
        try
        {
            if (__instance == null || damageInfo == null) return;
            var fromPlayer = damageInfo.TryCast<PlayerDamageInfo>();
            if (fromPlayer == null || fromPlayer.OwnerClientId != _localClientId) return;
            var distance = Vector3.Distance(_wizard, __instance.transform.position);
            var scaled = LongShot.Apply(amount, distance);
            if (scaled.Equals(amount)) return;
            if (_hits < LoggedHits)
                ModLog.Info(FormattableString.Invariant($"LongShot: hit at {distance:0.#} units: {amount:0.#} -> {scaled:0.#} (x{scaled / amount:0.###})."));
            _hits++;
            amount = scaled;
        }
        catch (Exception ex)
        {
            _errors++;
            ModLog.ErrorThrottled("long-shot", ex, Time.unscaledTime);
            if (_errors >= MaxErrors) ModLog.Warn("LongShot: turned off for this session after repeated errors.");
        }
    }
}
