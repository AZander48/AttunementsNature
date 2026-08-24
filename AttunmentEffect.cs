using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HadeanTactics;
using UnityEngine;

namespace AttunmentsNature;

/// <summary>
/// Attunment buff: PoisonClaw carrier with args "attunment:{burn|decay|frostbite|shock}".
/// On attack applies that ailment for the buff's current value; value decays 10% per attack.
/// Frostbite duration scales 1:1 with value.
/// </summary>
public static class AttunmentsEffects
{
    public const string ArgsPrefix = "attunment:";

    public const string Burn = "burn";
    public const string Decay = "decay";
    public const string Frostbite = "frostbite";
    public const string Shock = "shock";

    public static readonly string[] Elements = { Burn, Decay, Frostbite, Shock };

    public static string ArgsFor(string element) => ArgsPrefix + element;

    public static bool TryGetElement(string? args, out string element)
    {
        element = "";
        if (string.IsNullOrEmpty(args) || !args.StartsWith(ArgsPrefix, StringComparison.Ordinal))
            return false;

        element = args.Substring(ArgsPrefix.Length);
        for (int i = 0; i < Elements.Length; i++)
        {
            if (element == Elements[i])
                return true;
        }

        return false;
    }

    public static bool TryGetAttunment(UnitBehaviour unit, out UnitStatus status)
    {
        status = null!;
        if (unit == null)
            return false;

        UnitStatus? found = unit.GetStatus(EffectType.PoisonClaw);
        if (found?._eRemove == null || !TryGetElement(found._eRemove.args, out _))
            return false;

        status = found;
        return true;
    }

    public static Effect CreateBuff(string element, int value)
    {
        return new Effect(EffectType.PoisonClaw, value)
        {
            args = ArgsFor(element),
        };
    }

    public static EffectContainer CreateSkill(string element, int value)
    {
        return new EffectContainer
        {
            id = $"skill_attunment_{element}",
            containerType = EffectContainerType.skill,
            targetType = TargetType.AllyOnly,
            effects = new List<Effect> { CreateBuff(element, value) },
        };
    }

    public static void ApplyOnHit(UnitBehaviour source, UnitBehaviour target, Effect buff)
    {
        if (source == null || target == null || buff == null || buff.value <= 0)
            return;
        if (!TryGetElement(buff.args, out string element))
            return;

        var dMod = new DamageMod(SourceType.Unit, source.unit.appearId, source.unit.title, source.Team);
        Effect ailment = element switch
        {
            Burn => new Effect(EffectType.Burn, buff.value) { dMod = dMod },
            Decay => new Effect(EffectType.Decay, buff.value) { dMod = dMod },
            Shock => new Effect(EffectType.Shock, buff.value) { dMod = dMod },
            Frostbite => new Effect(EffectType.Frostbite, buff.value) { dMod = dMod },
            _ => null!,
        };

        if (ailment != null)
            target.AddStatus(ailment, source);
    }

    public static Effect CreateOnHitEffect(Effect buff)
    {
        if (buff == null || buff.value <= 0 || !TryGetElement(buff.args, out string element))
            return new Effect(EffectType.Decay, 0);

        return element switch
        {
            Burn => new Effect(EffectType.Burn, buff.value),
            Decay => new Effect(EffectType.Decay, buff.value),
            Shock => new Effect(EffectType.Shock, buff.value),
            Frostbite => new Effect(EffectType.Frostbite, buff.value),
            _ => new Effect(EffectType.Decay, buff.value),
        };
    }
}

public class AttunmentsEffectsManager
{
    private readonly ManualLogSource _log;
    private readonly ConfigEntry<bool> _debug;
    private readonly ConfigEntry<int> _buffValue;
    private readonly ConfigEntry<string> _element;

    private CardManager? _cardManager;
    private bool _skillsRegistered;

    public AttunmentsEffectsManager(ManualLogSource log, ConfigFile config)
    {
        _log = log;
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(AttunmentHandleMeleePatch).Assembly);

        _debug = config.Bind("Attunment", "Debug", false, "Enable or disable debug logging");
        _buffValue = config.Bind("Attunment", "Buff Value", 50, "Stacks / frostbite duration applied by the attunment buff.");
        _element = config.Bind(
            "Attunment",
            "Element",
            AttunmentsEffects.Burn,
            "Fixed element for the test card: burn, decay, frostbite, or shock.");

        config.Bind(
            "Attunment",
            "Add buff card to hand",
            false,
            new ConfigDescription(
                "Register attunment skills and add a card that applies the selected element buff to an ally.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = _ =>
                    {
                        if (GUILayout.Button("Add to hand", GUILayout.ExpandWidth(false)))
                            AddBuffCardToHand();
                    },
                    HideDefaultButton = true,
                }));

        EnsureSkillsRegistered();
    }

    private void EnsureSkillsRegistered()
    {
        if (_skillsRegistered)
            return;
        if (RegisterSkills())
            _skillsRegistered = true;
    }

    private bool RegisterSkills()
    {
        var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (relicManager == null)
            return false;

        foreach (string element in AttunmentsEffects.Elements)
        {
            EffectContainer skill = AttunmentsEffects.CreateSkill(element, _buffValue.Value);
            relicManager.AddOrReplaceEffectContainer(skill);
            if (_debug.Value)
                _log.LogInfo($"Registered {skill.id}");
        }

        return true;
    }

    private void AddBuffCardToHand()
    {
        _skillsRegistered = false;
        EnsureSkillsRegistered();

        string element = (_element.Value ?? AttunmentsEffects.Burn).Trim().ToLowerInvariant();
        if (!AttunmentsEffects.TryGetElement(AttunmentsEffects.ArgsFor(element), out _))
        {
            _log.LogError($"Invalid Element '{_element.Value}'. Use burn, decay, frostbite, or shock.");
            return;
        }

        var cardManager = GetCardManager();
        if (cardManager == null)
        {
            _log.LogError("Card manager not found");
            return;
        }

        Card card = new Card
        {
            title = $"Attunment ({element})",
            id = $"card_attunment_{element}",
            heroId = "any",
            cardType = CardType.enchant,
            cardTargetType = TargetType.AllyOnly,
            baseCost = 0,
            deplete = 1,
            repeat = 1,
            IsMod = false,
            modId = PluginInfo.PLUGIN_GUID,
            effects = new List<Effect> { AttunmentsEffects.CreateBuff(element, _buffValue.Value) },
        };

        if (string.IsNullOrEmpty(card.heroId))
            card.heroId = "any";
        cardManager.AddCardToAllCards(card);
        cardManager.DrawCardSimple(card);

        if (_debug.Value)
            _log.LogInfo($"Added attunment card ({element}, value={_buffValue.Value}) to hand.");
    }

    private CardManager? GetCardManager()
    {
        if (_cardManager != null)
            return _cardManager;
        _cardManager = UnityEngine.Object.FindObjectOfType<CardManager>();
        return _cardManager;
    }
}

[HarmonyPatch(typeof(UnitBehaviour), "HandleMelee")]
static class AttunmentHandleMeleePatch
{
    private static readonly MethodInfo? CheckAttackActivations =
        AccessTools.Method(typeof(UnitBehaviour), "CheckAttackActivations");

    static bool Prefix(UnitBehaviour __instance, UnitBehaviour targetUnit, int damage, bool crit)
    {
        if (!AttunmentsEffects.TryGetAttunment(__instance, out UnitStatus status))
            return true;

        if (!targetUnit.CheckMiss(__instance))
        {
            AttunmentsEffects.ApplyOnHit(__instance, targetUnit, status._eRemove);

            int dmg = damage;
            if (__instance.unit.overkill > 0 && targetUnit.unit.currentHp <= targetUnit.unit.MaxHP / 2)
                dmg *= __instance.unit.overkill;

            CheckAttackActivations?.Invoke(__instance, null);
            targetUnit.TakeDamageChecking(
                dmg,
                __instance,
                crit,
                ignoreShield: false,
                display: true,
                SourceType.Unit,
                __instance.unit.appearId,
                __instance.unit.title);
            __instance._animHandler?.OnAttackHit(dmg);
        }

        return false;
    }
}

[HarmonyPatch(typeof(ProjectileBehaviour), nameof(ProjectileBehaviour.Init), new[]
{
    typeof(UnitBehaviour),
    typeof(UnitBehaviour),
    typeof(List<Effect>),
    typeof(bool),
    typeof(DamageMod),
})]
static class AttunmentProjectileInitPatch
{
    static void Prefix(UnitBehaviour source, List<Effect> effects)
    {
        if (source == null || effects == null)
            return;
        if (!AttunmentsEffects.TryGetAttunment(source, out UnitStatus status))
            return;

        Effect replacement = AttunmentsEffects.CreateOnHitEffect(status._eRemove);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].effectType == EffectType.Decay && effects[i].value == source.unit.poisonClaw)
                effects[i] = replacement;
        }
    }
}

[HarmonyPatch(typeof(UnitStatus), nameof(UnitStatus.CheckDuration))]
static class AttunmentCheckDurationPatch
{
    static bool Prefix(UnitStatus __instance, StatusDurationType dt)
    {
        if (dt != StatusDurationType.OnAttack)
            return true;
        if (__instance?._eRemove == null || __instance._eRemove.effectType != EffectType.PoisonClaw)
            return true;
        if (!AttunmentsEffects.TryGetElement(__instance._eRemove.args, out _))
            return true;

        int value = __instance._eRemove.value;
        int loss = Mathf.Max(1, Mathf.CeilToInt(value * 0.1f));
        __instance._eRemove.value = Mathf.Max(0, value - loss);

        if (__instance.target?.unit != null)
            __instance.target.unit.poisonClaw = __instance._eRemove.value;

        __instance._OnValueChanged?.Invoke();

        if (__instance._eRemove.value <= 0)
            __instance.Remove();

        return false;
    }
}
