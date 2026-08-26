using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HadeanTactics;
using UnityEngine;
using UnityEngine.UI;

namespace AttunementsNature;

public class HeroUnitManager
{
    internal static HeroUnitManager? Instance { get; private set; }

    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _debug = null!;

    private UnitManager _unitManager = null!;
    private bool _heroRegistered = false;

    // Levels per style the game can upgrade into (vanilla uses {id}_{style}_{level}).
    private const int MaxStyleLevel = 3;

    // [0] is the picker portrait. The rest are class toggles on that portrait (skins).
    private readonly HeroDefinition[] _heroes =
    {
        new AttunmentHero(),
        new BeastHero(),
        new HumanHero(),
    };

    internal HeroDefinition PortraitHero => _heroes[0];

    public HeroUnitManager(ManualLogSource log, ConfigFile config)
    {
        Instance = this;
        _log = log;
        InitConfigEntries(config);
        // Don't spawn here — only try to register data if managers already exist.
        EnsureHeroUnitRegistered();
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("Hero Unit", "Debug", false, "Enable or disable debug logging");
    }

    /// <summary>Base hero: styleQnt = class count, skills seeded per the "start with all" toggle.</summary>
    private Unit BuildBaseHero(HeroDefinition hero, string visualDonorId, UnitPool pool)
    {
        var skills = new List<EffectContainer>();
        var styleLevel = new int[Mathf.Max(4, hero.Classes.Count)];

        return new Unit
        {
            id = hero.Id,
            appearId = visualDonorId,
            assetRef = visualDonorId,
            title = hero.Title,
            flavor = hero.Title,
            isCustomUnit = true,
            // rarity 11 → ProcessUnitUpgrade offers every style (not just 0 and 1).
            rarity = 11,
            pool = pool,
            team = TeamType.Team1,
            MaxHP = hero.MaxHP,
            currentHp = hero.MaxHP,
            BaseDamage = hero.BaseDamage,
            BaseAttackRange = hero.BaseAttackRange,
            baseAttackSpeed = hero.BaseAttackSpeed,
            movementSpeed = hero.MovementSpeed,
            MaxMana = hero.MaxMana,
            ManaRegen = hero.ManaRegen,
            currentMana = 0f,
            skillId = hero.Classes[0].BuildSkill(hero.Id, 1, 10).id, // TODO: change to the first skill of the hero / check value
            skillLevel = 1,
            skills = skills,
            styleQnt = hero.Classes.Count,
            styleLevel = styleLevel,
        };
    }

    /// <summary>
    /// Upgrade unit for a class/level: {heroId}_{classIndex}_{level}. The vanilla upgrade
    /// system (GetUnitUpgradeEntry + MergeAndUpgrade) merges these in on level-up,
    /// adding the class's skill to the hero's skills list (→ multiple skills).
    /// </summary>
    private Unit BuildUpgradeHeroUnit(HeroDefinition hero, IHeroClass cls, int level, string donorId)
    {
        EffectContainer skill = cls.BuildSkill(hero.Id, level, 10);
        return new Unit
        {
            id = $"{hero.Id}_{cls.Index}_{level}",
            appearId = donorId,
            assetRef = donorId,
            title = $"{cls.Name} {level}",
            // Not UnitPool.hero — otherwise ConstructHeroSelection shows these as extra portraits.
            pool = UnitPool.special,
            team = TeamType.Team1,
            // Stat deltas ADDED on merge (MergeAndUpgrade sums these into the hero).
            MaxHP = hero.UpgradeHp,
            BaseDamage = hero.UpgradeDamage,
            skillId = skill.id,
            skillLevel = level,
            // Only this class's skill — never the starter Attunment skill.
            skills = new List<EffectContainer> { skill },
            // MergeAndUpgrade always AddRange's alliances — only level 1 should grant the tag.
            alliances = level == 1
                ? new List<AllianceType> { cls.Alliance }
                : new List<AllianceType>(),
        };
    }

    /// <summary>Spawn path — AddUnitToTeam + isHero. Call from the config button, not during register.</summary>
    /*private void AddHeroUnitToBench()
    {
        var unitManager = GetUnitManager();
        if (unitManager == null)
        {
            _log.LogError("Unit manager not found");
            return;
        }

        _heroRegistered = false;
        EnsureHeroUnitRegistered();
        if (!_heroRegistered)
        {
            _log.LogError("Hero unit failed to register. Check Visual Donor Id / that you are in a run.");
            return;
        }

        HeroDefinition hero = _heroes[0];
        Unit unit = unitManager.GetUnitById(hero.Id);
        if (unit == null)
        {
            _log.LogError($"'{hero.Id}' not found after registration.");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Spawning hero (donor={hero.Instance.AppearId})");

        UnitBehaviour behaviour = unitManager.AddUnitToTeam(unit, wanderer: false);
        if (behaviour == null)
        {
            _log.LogError($"AddUnitToTeam failed for '{hero.Id}'.");
            return;
        }

        behaviour.isHero = true;

        if (_debug.Value)
            _log.LogInfo($"Spawned '{hero.Id}' as hero.");
    }*/

    private UnitManager GetUnitManager()
    {
        if (_unitManager != null) return _unitManager;
        _unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        return _unitManager;
    }

    /// <summary>
    /// Puts the base hero in unitPool[hero] (via AddUnitToAllUnits) so ConstructHeroSelection
    /// can build a portrait. Safe to call more than once; FeedData wipes the pool on load.
    /// </summary>
    internal void RegisterForPicker(UnitManager? unitManager = null)
    {
        try
        {
            unitManager ??= GetUnitManager();
            if (unitManager == null)
                return;
            _unitManager = unitManager;

            bool portraitReady = IsOnPicker(unitManager, PortraitHero.Id)
                && unitManager.GetUnitById(PortraitHero.Id) != null;
            bool skinsReady = true;
            for (int i = 1; i < _heroes.Length; i++)
            {
                if (unitManager.GetUnitById(_heroes[i].Id) == null)
                    skinsReady = false;
            }

            if (portraitReady && skinsReady)
            {
                EnsureProgressionEntry(unitManager, PortraitHero);
                RegisterAllSkills(unitManager);
                return;
            }

            _heroRegistered = false;
            if (RegisterHeroUnits(PortraitHero, unitManager))
            {
                _heroRegistered = true;
                EnsureProgressionEntry(unitManager, PortraitHero);
            }
        }
        catch (Exception e)
        {
            _log.LogError($"RegisterForPicker failed: {e}");
        }
    }

    /// <summary>Re-add class skills to RelicManager (FeedData wipes the container dictionary).</summary>
    private void RegisterAllSkills(UnitManager unitManager)
    {
        RelicManager? relicManager = unitManager._manager?._relicManager
            ?? UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (relicManager == null)
            return;

        foreach (HeroDefinition hero in _heroes)
        {
            foreach (IHeroClass cls in hero.Classes)
            {
                for (int level = 1; level <= MaxStyleLevel; level++)
                    relicManager.AddOrReplaceEffectContainer(cls.BuildSkill(hero.Id, level, 10));
            }
        }
    }

    private bool IsOnPicker(UnitManager unitManager, string heroId)
    {
        if (unitManager.unitPool == null)
            return false;
        if (!unitManager.unitPool.TryGetValue(UnitPool.hero, out List<Unit> heroes) || heroes == null)
            return false;
        for (int i = 0; i < heroes.Count; i++)
        {
            if (heroes[i] != null && heroes[i].id == heroId)
                return true;
        }
        return false;
    }

    /// <summary>Data-only: register class skills, base heroes, and per-class upgrade units.</summary>
    private bool RegisterHeroUnits(HeroDefinition heroDef, UnitManager? unitManager = null)
    {
        unitManager ??= UnityEngine.Object.FindObjectOfType<UnitManager>();
        RelicManager relicManager = unitManager?._manager?._relicManager
            ?? UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (unitManager == null) return false;

        bool allOk = true;
        foreach (HeroDefinition hero in _heroes)
        {
            bool onPortrait = hero.Id == PortraitHero.Id;
            if (!onPortrait)
                RemoveFromHeroPicker(unitManager, hero.Id);
            if (!RegisterOneHero(unitManager, relicManager, hero, hero.AppearId, onPortrait))
                allOk = false;
        }

        return allOk;
    }

    private bool RegisterOneHero(UnitManager unitManager, RelicManager? relicManager, HeroDefinition hero, string configDonor, bool onPortrait)
    {
        string donorId = !string.IsNullOrEmpty(configDonor) ? configDonor : hero.AppearId;
        if (string.IsNullOrEmpty(donorId))
        {
            _log.LogError($"AppearId is empty for '{hero.Id}'.");
            return false;
        }

        if (relicManager != null)
        {
            foreach (IHeroClass cls in hero.Classes)
            {
                for (int level = 1; level <= MaxStyleLevel; level++)
                    relicManager.AddOrReplaceEffectContainer(cls.BuildSkill(hero.Id, level, 10));
            }   
        }

        Unit baseHero = BuildBaseHero(hero, donorId, onPortrait ? UnitPool.hero : UnitPool.special);

        GameObject model = PoolManager.GetUnitPrefab(donorId);
        if (model != null)
            PoolManager.AddOrReplaceUnitPrefab($"{hero.Id}_prefab", model);

        unitManager.AddUnitToAllUnits(baseHero);
        // Unit(Unit) copy ctor does not copy skillLevel — restore it on the stored unit.
        Unit storedBase = unitManager.GetUnitById(hero.Id);
        if (storedBase != null)
            storedBase.skillLevel = baseHero.skillLevel;

        if (storedBase == null)
        {
            _log.LogError($"'{hero.Id}' missing after AddUnitToAllUnits.");
            return false;
        }

        foreach (IHeroClass cls in hero.Classes)
        {
            for (int level = 1; level <= MaxStyleLevel; level++)
            {
                Unit upgrade = BuildUpgradeHeroUnit(hero, cls, level, donorId);
                unitManager.AddUnitToAllUnits(upgrade);
                Unit stored = unitManager.GetUnitById(upgrade.id);
                if (stored != null)
                    stored.skillLevel = level;
            }
        }

        if (_debug.Value)
            _log.LogInfo($"Registered '{hero.Id}' donor={donorId} classes={hero.Classes.Count}.");

        return true;
    }

    private static void RemoveFromHeroPicker(UnitManager unitManager, string heroId)
    {
        if (unitManager.unitPool == null)
            return;
        if (!unitManager.unitPool.TryGetValue(UnitPool.hero, out List<Unit> heroes) || heroes == null)
            return;
        heroes.RemoveAll(u => u != null && u.id == heroId);
    }

    /// <summary>
    /// Extra class identities shown as skin toggles on the single portrait.
    /// A run starts with currentHeroSkin, so these are full units (just not in UnitPool.hero).
    /// </summary>
    internal void AppendClassSkins(Unit heroUnit, List<Unit> skins)
    {
        if (heroUnit == null || skins == null || heroUnit.id != PortraitHero.Id)
            return;

        UnitManager? unitManager = GetUnitManager();
        if (unitManager == null)
            return;

        for (int i = 1; i < _heroes.Length; i++)
        {
            Unit skin = unitManager.GetUnitById(_heroes[i].Id);
            if (skin == null)
                continue;
            bool already = false;
            for (int s = 0; s < skins.Count; s++)
            {
                if (skins[s] != null && skins[s].id == skin.id)
                {
                    already = true;
                    break;
                }
            }
            if (!already)
                skins.Add(skin);
        }
    }

    /// <summary>
    /// Vanilla EnsureHeroDataExists calls GetHeroData first, which enumerates crypt lists
    /// and NREs if progression/crypt is not ready. Write into heroesData only.
    /// </summary>
    private void EnsureProgressionEntry(UnitManager unitManager, HeroDefinition hero)
    {
        MetaManager? meta = unitManager._manager?._metaManager
            ?? UnityEngine.Object.FindObjectOfType<MetaManager>();
        List<HeroData>? heroes = meta?._progressionData?.heroesData;
        if (heroes == null)
            return;

        for (int i = 0; i < heroes.Count; i++)
        {
            if (heroes[i] != null && heroes[i].id == hero.Id)
                return;
        }

        heroes.Add(new HeroData
        {
            id = hero.Id,
            level = 0,
            currentXp = 0,
            corruptionLevel = 0,
            customHero = true,
        });
    }

    private void EnsureHeroUnitRegistered()
    {
        RegisterForPicker();
        _heroRegistered = true;
    }

    /// <summary>
    /// Rebuild skills from unlocked styles only (styleLevel[i] &gt; 0).
    /// The free starter Attunment skill (styleLevel[0] == 0) is dropped once another class
    /// is unlocked, so it is not carried onto Element Cloak / Roots / etc.
    /// </summary>
    internal void SyncSkillsFromStyles(Unit unit)
    {
        if (unit == null)
            return;

        HeroDefinition? hero = null;
        for (int i = 0; i < _heroes.Length; i++)
        {
            if (_heroes[i].Id == unit.id)
            {
                hero = _heroes[i];
                break;
            }
        }
        if (hero == null)
            return;

        var next = new List<EffectContainer>();
        for (int i = 0; i < hero.Classes.Count; i++)
        {
            int level = i < unit.styleLevel.Length ? unit.styleLevel[i] : 0;
            if (level <= 0)
                continue;

            IHeroClass cls = hero.Classes[i];
            EffectContainer probe = cls.BuildSkill(hero.Id, 1, 10);
            string stablePureId = CustomHeroSkillDisplay.PureIdForStyle(hero.Id, i);
            bool stacksAcrossLevels = probe.pureId == stablePureId;

            if (stacksAcrossLevels)
            {
                next.Add(cls.BuildSkill(hero.Id, level, 10));
            }
            else
            {
                for (int lv = 1; lv <= level; lv++)
                    next.Add(cls.BuildSkill(hero.Id, lv, 10));
            }
        }

        if (next.Count > 0)
            unit.skills = next;
    }
}

/// <summary>
/// Custom hero skills have no loca entries. Tooltips use GetDescription(), which overwrites
/// title via GetLocaPure(id + "_title") — so setting EffectContainer.title alone never shows.
/// </summary>
static class CustomHeroSkillDisplay
{
    static readonly Dictionary<string, string> TitlesBySkillId = new();
    static readonly Dictionary<string, string> DescsBySkillId = new();

    internal static void Register(EffectContainer skill)
    {
        if (skill == null || string.IsNullOrEmpty(skill.id))
            return;
        if (!string.IsNullOrEmpty(skill.title))
            TitlesBySkillId[skill.id] = skill.title;
        if (!string.IsNullOrEmpty(skill.description))
            DescsBySkillId[skill.id] = skill.description;
    }

    internal static bool TryGetTitle(string? skillId, out string title)
    {
        title = "";
        return !string.IsNullOrEmpty(skillId) && TitlesBySkillId.TryGetValue(skillId, out title!);
    }

    internal static bool TryGetDescription(string? skillId, out string description)
    {
        description = "";
        return !string.IsNullOrEmpty(skillId) && DescsBySkillId.TryGetValue(skillId, out description!);
    }

    internal static bool IsModHeroId(string? id) =>
        id == "elemental" || id == "beast" || id == "scholar";

    internal static bool IsModSkill(EffectContainer? skill) =>
        skill?.id != null &&
        (skill.id.StartsWith("skill_elemental_")
         || skill.id.StartsWith("skill_beast_")
         || skill.id.StartsWith("skill_scholar_"));

    internal static bool IsModUpgradePrefix(string id) =>
        id.StartsWith("elemental_")
        || id.StartsWith("beast_")
        || id.StartsWith("scholar_");

    internal static string PureIdForStyle(string heroId, int style) =>
        $"skill_{heroId}_{style}";
}

[HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetLocaPure))]
static class CustomHeroSkillLocaPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (string.IsNullOrEmpty(key) || __result != key)
            return;

        // Missing loca → raw key. Swap in our registered skill titles / descriptions.
        if (key.EndsWith("_title", StringComparison.Ordinal))
        {
            string skillId = key.Substring(0, key.Length - "_title".Length);
            if (CustomHeroSkillDisplay.TryGetTitle(skillId, out string title))
                __result = title;
        }
        else if (key.EndsWith("_desc", StringComparison.Ordinal))
        {
            string skillId = key.Substring(0, key.Length - "_desc".Length);
            if (CustomHeroSkillDisplay.TryGetDescription(skillId, out string desc))
                __result = desc;
        }
    }
}

[HarmonyPatch(typeof(EffectContainer), nameof(EffectContainer.GetTitle))]
static class CustomHeroSkillGetTitlePatch
{
    static void Postfix(EffectContainer __instance, ref string __result)
    {
        if (!CustomHeroSkillDisplay.IsModSkill(__instance))
            return;
        if (CustomHeroSkillDisplay.TryGetTitle(__instance.id, out string registered))
            __result = registered;
        else if (!string.IsNullOrEmpty(__instance.title))
            __result = __instance.title;
    }
}

[HarmonyPatch(typeof(EffectContainer), nameof(EffectContainer.GetDescription))]
static class CustomHeroSkillGetDescriptionPatch
{
    static void Prefix(EffectContainer __instance, out string __state)
    {
        __state = CustomHeroSkillDisplay.IsModSkill(__instance) ? (__instance.title ?? "") : "";
    }

    static void Postfix(EffectContainer __instance, ref string __result, string __state)
    {
        if (string.IsNullOrEmpty(__state))
            return;

        // GetDescription overwrites title with the loca key; put our name back into the string.
        __instance.title = __state;
        if (!string.IsNullOrEmpty(__instance.id))
        {
            string bad = __instance.id + "_title";
            if (__result != null && __result.Contains(bad))
                __result = __result.Replace(bad, __state);
        }
    }
}

[HarmonyPatch(typeof(EffectContainer), nameof(EffectContainer.GetDescriptionAndTitle))]
static class CustomHeroSkillGetDescriptionAndTitlePatch
{
    static void Prefix(EffectContainer __instance, out string __state)
    {
        __state = CustomHeroSkillDisplay.IsModSkill(__instance) ? (__instance.title ?? "") : "";
    }

    static void Postfix(EffectContainer __instance, ref string __result, string __state)
    {
        if (string.IsNullOrEmpty(__state))
            return;
        __instance.title = __state;
        if (!string.IsNullOrEmpty(__instance.id) && __result != null)
        {
            string bad = __instance.id + "_title";
            if (__result.Contains(bad))
                __result = __result.Replace(bad, __state);
        }
    }
}

[HarmonyPatch(typeof(UnitManager), nameof(UnitManager.GetUpgradeTitle))]
static class CustomHeroGetUpgradeTitlePatch
{
    static void Postfix(UnitManager __instance, string id, int level, ref string __result)
    {
        if (!CustomHeroSkillDisplay.IsModUpgradePrefix(id))
            return;

        Unit upgrade = __instance.GetUnitById($"{id}_{level}");
        if (upgrade != null && !string.IsNullOrEmpty(upgrade.title))
            __result = upgrade.title;
    }
}

[HarmonyPatch(typeof(EffectContainer), nameof(EffectContainer.Stack))]
static class AttunmentEffectContainerStackPatch
{
    static void Postfix(EffectContainer __instance, EffectContainer ec, ref bool __result)
    {
        // Only sync id/title for our custom skills. Vanilla Stack leaves id alone so
        // GetLocaPure(id + "_title") still finds entries like skill_companion_skill_title.
        // Rewriting id to skill_companion_skill_2 breaks Nightshade Occultist (and similar).
        if (!__result || ec == null || !CustomHeroSkillDisplay.IsModSkill(__instance))
            return;
        if (string.IsNullOrEmpty(ec.pureId) || ec.pureId != __instance.pureId)
            return;

        if (!string.IsNullOrEmpty(ec.id))
            __instance.id = ec.id;
        if (!string.IsNullOrEmpty(ec.title))
        {
            __instance.title = ec.title;
            CustomHeroSkillDisplay.Register(__instance);
        }
    }
}

[HarmonyPatch(typeof(Unit), nameof(Unit.MergeAndUpgrade))]
static class CustomHeroMergeAndUpgradePatch
{
    static void Postfix(Unit __instance, Unit u, int style)
    {
        if (__instance?.skills == null)
            return;
        if (!CustomHeroSkillDisplay.IsModHeroId(__instance.id))
            return;

        if (style >= 0)
            HeroUnitManager.Instance?.SyncSkillsFromStyles(__instance);
    }
}

/// <summary>
/// Upgrade previews merge the whole hero, so class 0's skill icon appears on every class's
/// upgrade panel. For our heroes, only show the skill for the style being upgraded.
/// </summary>
[HarmonyPatch(typeof(PanelUnitInfoBehaviour), nameof(PanelUnitInfoBehaviour.InitAsUpgrade))]
static class CustomHeroUpgradePanelSkillsPatch
{
    static void Postfix(PanelUnitInfoBehaviour __instance, Unit oldu, Unit u, GameManager manager, string an, int si, bool willUpgrade)
    {
        if (oldu == null || !CustomHeroSkillDisplay.IsModHeroId(oldu.id))
            return;
        if (__instance.skills == null)
            return;

        // Merged preview already ran SyncSkillsFromStyles — only the unlocked styles remain.
        // Still hide any leftover icons that aren't this style.
        string wantPureId = CustomHeroSkillDisplay.PureIdForStyle(oldu.id, si);
        for (int i = 0; i < __instance.skills.Length; i++)
        {
            RelicBehavior slot = __instance.skills[i];
            if (slot == null)
                continue;
            EffectContainer relic = slot.relic;
            bool show = relic != null && !string.IsNullOrEmpty(relic.pureId) &&
                (relic.pureId == wantPureId
                 || relic.pureId.StartsWith(wantPureId + "_", StringComparison.Ordinal));
            slot.gameObject.SetActive(show);
        }
    }
}

[HarmonyPatch(typeof(RelicManager), nameof(RelicManager.DownloadData))]
static class AttunmentHeroRelicDownloadPatch
{
    static void Postfix()
    {
        // Relic FeedData clears containers then FillUnitsWithSkillsAndTitle — re-inject after.
        HeroUnitManager.Instance?.RegisterForPicker();
    }
}

[HarmonyPatch(typeof(UnitManager), nameof(UnitManager.DownloadData))]
static class AttunmentHeroDownloadDataPatch
{
    static void Postfix(UnitManager __instance)
    {
        // FeedData just rebuilt vanilla pools; inject the custom hero afterward.
        HeroUnitManager.Instance?.RegisterForPicker(__instance);
    }
}

[HarmonyPatch(typeof(CharacterSelectionManager), nameof(CharacterSelectionManager.ConstructHeroSelection))]
static class AttunmentHeroConstructSelectionPatch
{
    static void Prefix()
    {
        // Portraits are built from unitPool[hero] in this method.
        HeroUnitManager.Instance?.RegisterForPicker();
    }
}

[HarmonyPatch(typeof(CharacterSelectionManager), "ResolveSkinList")]
static class AttunmentHeroResolveSkinListPatch
{
    static void Postfix(Unit heroUnit, List<Unit> __result)
    {
        HeroUnitManager.Instance?.AppendClassSkins(heroUnit, __result);
    }
}

[HarmonyPatch(typeof(HeroPortraitBehaviour), nameof(HeroPortraitBehaviour.Select))]
static class AttunmentHeroPortraitSelectPatch
{
    static void Postfix(HeroPortraitBehaviour __instance)
    {
        // Vanilla Select() hides skinsToggles[1+] when isCustom is true (custom crypt heroes).
        // Our class identities use those extra toggles on the Attunment portrait.
        HeroUnitManager? manager = HeroUnitManager.Instance;
        if (manager == null || __instance.originalHeroId != manager.PortraitHero.Id)
            return;
        if (__instance.locked)
            return;

        List<Unit> skins = __instance.skins;
        Toggle[] toggles = __instance.skinsToggles;
        if (skins == null || toggles == null)
            return;

        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i] == null)
                continue;
            bool show = i < skins.Count;
            toggles[i].gameObject.SetActive(show);
            if (show)
                toggles[i].interactable = !skins[i].lockedInCharacterSelection;
        }
    }
}

[HarmonyPatch(typeof(MetaManager), nameof(MetaManager.DownloadData))]
static class AttunmentHeroMetaDownloadPatch
{
    static void Postfix(MetaManager __instance)
    {
        UnitManager? units = __instance._manager?._unitManager
            ?? UnityEngine.Object.FindObjectOfType<UnitManager>();
        HeroUnitManager.Instance?.RegisterForPicker(units);
    }
}

[HarmonyPatch(typeof(MetaManager), nameof(MetaManager.GetAllHeroDataList))]
static class AttunementsafeHeroDataListPatch
{
    static bool Prefix(MetaManager __instance, ref List<HeroData> __result)
    {
        var list = new List<HeroData>();
        List<HeroData>? saved = __instance._progressionData?.heroesData;
        if (saved != null)
        {
            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i] != null)
                    list.Add(saved[i]);
            }
        }

        CryptManager? crypt = __instance._manager?._cryptManager;
        if (crypt != null)
        {
            try
            {
                List<CustomUnitData> custom = crypt.GetCustomHeroDataList();
                if (custom != null)
                {
                    for (int i = 0; i < custom.Count; i++)
                    {
                        if (custom[i]?.heroData != null)
                            list.Add(custom[i].heroData);
                    }
                }

                List<CustomUnitData> random = crypt.randomUnitsData;
                if (random != null)
                {
                    for (int i = 0; i < random.Count; i++)
                    {
                        if (random[i]?.heroData != null)
                            list.Add(random[i].heroData);
                    }
                }
            }
            catch (Exception)
            {
                // Crypt lists are optional; saved campaign heroes are enough for the picker.
            }
        }

        __result = list;
        return false;
    }
}
