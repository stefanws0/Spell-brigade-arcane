namespace SpellBrigadeArcane.Game;

using System;
using System.Linq;
using SpellBrigadeArcane.Core;
using UnityEngine.InputSystem;

/// <summary>
/// Test hotkeys (config <c>Debug.TestHotkeys</c>), read every frame in a mission:
/// Ctrl+8 infuses one of the local wizard's spells with the next base element (Fire, Ice, Acid, Lightning, Dark in
/// turn) through the game's own <c>ImprovementsManager.AddSpellInfusion</c>, without the choice screen. For testing Prism.
/// Ctrl+9 drops a Health Potion 3 units in front of the wizard through the game's own <c>PickupSpawner.SpawnPickup</c>
/// (host or solo only). For testing Potion Belt.
/// </summary>
internal static class TestHotkeys
{
    private static readonly ElementType[] Elements =
        { ElementType.Fire, ElementType.Ice, ElementType.Acid, ElementType.Lightning, ElementType.Dark };

    private static bool _keyboardRead;
    private static int _next;

    public static void Update()
    {
        if (!ModConfig.TestHotkeys.Value) return;
        var first = !_keyboardRead;
        _keyboardRead = true; // before the read, so a throwing read can't write this breadcrumb every frame
        if (first) ModLog.Step("TestHotkeys: first Keyboard.current read");
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (!keyboard.leftCtrlKey.isPressed && !keyboard.rightCtrlKey.isPressed) return;
        if (keyboard.digit8Key.wasPressedThisFrame) Infuse();
        else if (keyboard.digit9Key.wasPressedThisFrame) SpawnPotion();
        else if (keyboard.digit7Key.wasPressedThisFrame) HudDump.Write();
    }

    private static void Infuse()
    {
        var identity = MissionBonuses.Identity;
        var stats = identity == null ? null : identity.stats;
        if (stats == null)
        {
            ModLog.Info("TestHotkeys: Ctrl+8 only works in a mission, a moment after it starts.");
            return;
        }

        var element = Elements[_next % Elements.Length];
        var spells = stats.GetEquippedSpells();
        SpellType? target = null;
        // A spell takes at most two elements (None -> X -> X+Y); a third would throw inside the game.
        for (var i = 0; i < spells.Count && target == null; i++)
            if (stats.GetElement(spells[i]) == ElementType.None) target = spells[i];
        for (var i = 0; i < spells.Count && target == null; i++)
        {
            var current = stats.GetElement(spells[i]);
            if (current != element && Prism.BaseElements.Contains(current.ToString())) target = spells[i];
        }

        if (target == null)
        {
            ModLog.Info("TestHotkeys: no spell can take another infusion.");
            return;
        }

        ModLog.Step($"TestHotkeys: Ctrl+8 infusing {target.Value} with {element}");
        var manager = NetworkSingleton<ImprovementsManager>.Instance;
        if (manager == null) throw new InvalidOperationException("the game's ImprovementsManager is missing");
        manager.AddSpellInfusion(target.Value, element);
        _next++;
        ModLog.Info($"TestHotkeys: infused {target.Value} with {element}.");
    }

    private static void SpawnPotion()
    {
        var identity = MissionBonuses.Identity;
        var body = identity == null ? null : identity.currentGameplayPlayer;
        if (body == null)
        {
            ModLog.Info("TestHotkeys: Ctrl+9 only works in a mission, while your wizard is up.");
            return;
        }

        var spawner = NetworkSingleton<PickupSpawner>.Instance;
        if (spawner == null || !spawner.IsServer)
        {
            ModLog.Info("TestHotkeys: Ctrl+9 only works when you host (or play solo).");
            return;
        }

        var position = body.transform.position + new UnityEngine.Vector3(3f, 0f, 0f);
        ModLog.Step("TestHotkeys: Ctrl+9 spawning a Health Potion");
        spawner.SpawnPickup(position, UnityEngine.Quaternion.identity, spawner.HealthPickupPrefab);
        ModLog.Info("TestHotkeys: dropped a Health Potion next to you.");
    }
}
