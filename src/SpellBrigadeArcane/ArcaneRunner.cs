namespace SpellBrigadeArcane;

using System;
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
    }

    /// <summary>Marks a normal quit in the log, so the next start can tell a crash from a clean exit.</summary>
    public void OnApplicationQuit() => ModLog.Step(ModLog.CleanExit);
}
