using HarmonyLib;
using HadeanTactics;

namespace AttunementsNature;

/// <summary>
/// HP-threshold execute: hijacks EffectType.Execution when args == "mod_execute".
/// If target HP is below value% of max HP, kill them. Otherwise do nothing
/// (vanilla Execution's DPS hit is skipped for our marked effects).
/// </summary>
public static class ExecuteEffects
{
    public const string ArgsMarker = "mod_execute";

    /// <param name="thresholdPercent">Kill if currentHp &lt; MaxHP * (percent / 100).</param>
    public static Effect Create(int thresholdPercent)
    {
        return new Effect(EffectType.Execution, thresholdPercent)
        {
            args = ArgsMarker,
        };
    }

    public static bool IsOurs(Effect effect)
    {
        return effect != null
            && effect.effectType == EffectType.Execution
            && effect.args == ArgsMarker;
    }

    public static void TryExecute(UnitBehaviour target, UnitBehaviour source, Effect effect)
    {
        if (target == null || !target.isAlive || effect == null || effect.value <= 0)
            return;

        if (!target.unit.IsHpBellowPercentage(effect.value))
            return;

        var dMod = source != null
            ? new DamageMod(SourceType.Unit, source.unit.appearId, source.unit.title, source.Team)
            : new DamageMod();

        target.OnBeginDeath(source, dMod);
    }
}

/// <summary>
/// Intercepts the unit-targeted Execute overload so our marked Execution effects
/// do threshold kill instead of vanilla "deal my DPS as crit + mana on kill".
/// </summary>
[HarmonyPatch(typeof(Effect), nameof(Effect.Execute),
    typeof(GameManager), typeof(UnitBehaviour), typeof(UnitBehaviour), typeof(EffectAttr))]
public static class ExecuteEffectPatch
{
    static bool Prefix(Effect __instance, UnitBehaviour targetUnit, UnitBehaviour source)
    {
        if (!ExecuteEffects.IsOurs(__instance))
            return true; // not ours — run vanilla

        ExecuteEffects.TryExecute(targetUnit, source, __instance);
        return false; // skip vanilla Execution
    }
}
