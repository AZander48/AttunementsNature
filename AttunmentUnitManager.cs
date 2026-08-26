using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HadeanTactics;
using UnityEngine;
using UnityEngine.UI;

namespace AttunmentsNature;

public class AttunmentUnitManager
{
    internal static AttunmentUnitManager? Instance { get; private set; }

    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _debug = null!;

    private UnitManager _unitManager = null!;

    // Levels per style the game can upgrade into (vanilla uses {id}_{style}_{level}).
    private const int MaxStyleLevel = 3;

    public AttunmentUnitManager(ManualLogSource log, ConfigFile config)
    {
        Instance = this;
        _log = log;
        InitConfigEntries(config);
        // Don't spawn here — only try to register data if managers already exist.
        //EnsureHeroUnitRegistered();
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("Hero Unit", "Debug", false, "Enable or disable debug logging");
    }

    /// <summary>
    /// Upgrade unit for a class/level: {heroId}_{classIndex}_{level}. The vanilla upgrade
    /// system (GetUnitUpgradeEntry + MergeAndUpgrade) merges these in on level-up,
    /// adding the class's skill to the hero's skills list (→ multiple skills).
    /// </summary>
    /*private Unit BuildUpgradeUnit(HeroDefinition hero, IHeroClass cls, int level, string donorId)
    {
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
    }*/

    /// <summary>Spawn path — AddUnitToTeam + is. Call from the config button, not during register.</summary>
    /*private void AddAttunmentUnitToBench()
    {
        var unitManager = GetUnitManager();
        if (unitManager == null)
        {
            _log.LogError("Unit manager not found");
            return;
        }

        Unit unit = unitManager.GetUnitById(AttunmentUnit.Instance.Id);
        if (unit == null)
        {
            _log.LogError($"'{AttunmentUnit.Instance.Id}' not found after registration.");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Spawning attunment unit (donor={AttunmentUnit.Instance.AppearId})");

        UnitBehaviour behaviour = unitManager.AddUnitToTeam(unit, wanderer: false);
        if (behaviour == null)
        {
            _log.LogError($"AddUnitToTeam failed for '{AttunmentUnit.Instance.Id}'.");
            return;
        }

        behaviour.isHero = true;

        if (_debug.Value)
            _log.LogInfo($"Spawned '{AttunmentUnit.Instance.Id}' as attunment unit.");
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
    /*internal void RegisterForPicker(UnitManager? unitManager = null)
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
            if (RegisterHeroUnits(unitManager))
            {
                _heroRegistered = true;
                EnsureProgressionEntry(unitManager, PortraitHero);
            }
        }
        catch (Exception e)
        {
            _log.LogError($"RegisterForPicker failed: {e}");
        }
    }*/

    /// <summary>Re-add class skills to RelicManager (FeedData wipes the container dictionary).</summary>
    /*private void RegisterAllSkills(UnitManager unitManager)
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
                    relicManager.AddOrReplaceEffectContainer(cls.BuildSkill(hero.Id, level, _skillValue.Value));
            }
        }
    }*/

    /*private bool IsOnPicker(UnitManager unitManager, string heroId)
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
    }*/

    /// <summary>Data-only: register class skills, base heroes, and per-class upgrade units.</summary>
    /*private bool RegisterHeroUnits(UnitManager? unitManager = null)
    {
        unitManager ??= UnityEngine.Object.FindObjectOfType<UnitManager>();
        RelicManager relicManager = unitManager?._manager?._relicManager
            ?? UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (unitManager == null) return false;

        string configDonor = _visualDonorId.Value?.Trim() ?? "";
        bool allOk = true;
        foreach (HeroDefinition hero in _heroes)
        {
            bool onPortrait = hero.Id == PortraitHero.Id;
            if (!onPortrait)
                RemoveFromHeroPicker(unitManager, hero.Id);
            if (!RegisterOneHero(unitManager, relicManager, hero, configDonor, onPortrait))
                allOk = false;
        }

        return allOk;
    }*/

    /*private bool RegisterOneHero(UnitManager unitManager, RelicManager? relicManager, HeroDefinition hero, string configDonor, bool onPortrait)
    {
        string donorId = !string.IsNullOrEmpty(configDonor) ? configDonor : hero.AppearId;
        if (string.IsNullOrEmpty(donorId))
        {
            _log.LogError($"Visual donor is empty for '{hero.Id}'.");
            return false;
        }

        if (relicManager != null)
        {
            foreach (IHeroClass cls in hero.Classes)
            {
                for (int level = 1; level <= MaxStyleLevel; level++)
                    relicManager.AddOrReplaceEffectContainer(cls.BuildSkill(hero.Id, level, _skillValue.Value));
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
                Unit upgrade = BuildUpgradeUnit(hero, cls, level, donorId);
                unitManager.AddUnitToAllUnits(upgrade);
                Unit stored = unitManager.GetUnitById(upgrade.id);
                if (stored != null)
                    stored.skillLevel = level;
            }
        }

        if (_debug.Value)
            _log.LogInfo($"Registered '{hero.Id}' donor={donorId} classes={hero.Classes.Count}.");

        return true;
    }*/

    /*private static void RemoveFromHeroPicker(UnitManager unitManager, string heroId)
    {
        if (unitManager.unitPool == null)
            return;
        if (!unitManager.unitPool.TryGetValue(UnitPool.hero, out List<Unit> heroes) || heroes == null)
            return;
        heroes.RemoveAll(u => u != null && u.id == heroId);
    }*/

    /// <summary>
    /// Extra class identities shown as skin toggles on the single portrait.
    /// A run starts with currentHeroSkin, so these are full units (just not in UnitPool.hero).
    /// </summary>
    /*internal void AppendClassSkins(Unit heroUnit, List<Unit> skins)
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
    }*/

    /// <summary>
    /// Vanilla EnsureHeroDataExists calls GetHeroData first, which enumerates crypt lists
    /// and NREs if progression/crypt is not ready. Write into heroesData only.
    /// </summary>
    /*private void EnsureProgressionEntry(UnitManager unitManager, HeroDefinition hero)
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
    }*/

    /*private void EnsureHeroUnitRegistered()
    {
        RegisterForPicker();
    }*/

    /// <summary>
    /// Rebuild skills from unlocked styles only (styleLevel[i] &gt; 0).
    /// The free starter Attunment skill (styleLevel[0] == 0) is dropped once another class
    /// is unlocked, so it is not carried onto Element Cloak / Roots / etc.
    /// </summary>
    /*internal void SyncSkillsFromStyles(Unit unit)
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
            EffectContainer probe = cls.BuildSkill(hero.Id, 1, _skillValue.Value);
            string stablePureId = CustomHeroSkillDisplay.PureIdForStyle(hero.Id, i);
            bool stacksAcrossLevels = probe.pureId == stablePureId;

            if (stacksAcrossLevels)
            {
                next.Add(cls.BuildSkill(hero.Id, level, _skillValue.Value));
            }
            else
            {
                for (int lv = 1; lv <= level; lv++)
                    next.Add(cls.BuildSkill(hero.Id, lv, _skillValue.Value));
            }
        }

        if (next.Count > 0)
            unit.skills = next;
    }*/
}
