namespace SpellBrigadeArcane;

using System;
using SpellBrigadeArcane.Game;
using UnityEngine;

/// <summary>
/// The only MonoBehaviour this mod injects. It forwards Unity's frame callback to plain C# code.
/// IL2CPP needs the (IntPtr) constructor: Unity creates the native object and the interop layer wraps it.
/// </summary>
public class ArcaneRunner : MonoBehaviour
{
    public ArcaneRunner(IntPtr ptr) : base(ptr)
    {
    }

    public void Update()
    {
        var now = Time.unscaledTime;
        try
        {
            ArcaneShop.Tick(now);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("arcane-shop", ex, now);
        }

        if (!ModConfig.Scan.Value) return;
        try
        {
            EnchantmentScan.Tick(now);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("enchantment-scan", ex, now);
        }

        try
        {
            StatScan.Tick(now);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("stat-scan", ex, now);
        }
    }

    /// <summary>Marks a normal quit in the log, so the next start can tell a crash from a clean exit.</summary>
    public void OnApplicationQuit() => ModLog.Step(ModLog.CleanExit);
}
