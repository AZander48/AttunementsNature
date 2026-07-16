using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HadeanTactics;
using UnityEngine;

namespace AttunmentsNature;

public class HeroUnitManager
{
    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _debug = null!;
    private ConfigEntry<string> _visualDonorId = null!;
    private ConfigEntry<string> _skillElement = null!;
    private ConfigEntry<int> _skillValue = null!;

    private UnitManager _unitManager = null!;
    private bool _heroRegistered = false;

    private const string HeroUnitId = "my_hero";
    private const string HeroSkillId = "skill_my_hero";

    public HeroUnitManager(ManualLogSource log, ConfigFile config)
    {
        _log = log;
        InitConfigEntries(config);
        // Don't spawn here — only try to register data if managers already exist.
        EnsureHeroUnitRegistered();
    }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("Hero Unit", "Debug", false, "Enable or disable debug logging");
        _visualDonorId = config.Bind(
            "Hero Unit",
            "Visual Donor Id",
            "moonhunter",
            "Unit id for model. Use InfoBox ID.");
        _skillElement = config.Bind(
            "Hero Unit",
            "Skill Element",
            AttunmentsEffects.Burn,
            "Attunment element applied to self on skill: burn, decay, freeze, or shock.");
        _skillValue = config.Bind(
            "Hero Unit",
            "Skill Value",
            10,
            "Attunment buff value (stacks / freeze duration).");

        config.Bind(
            "Hero Unit",
            "Add to bench",
            false,
            new ConfigDescription(
                "Register the hero unit (if needed) and spawn it as a hero on the bench/party.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = _ =>
                    {
                        if (GUILayout.Button("Add to bench", GUILayout.ExpandWidth(false)))
                            AddHeroUnitToBench();
                    },
                    HideDefaultButton = true,
                }));
    }

    private EffectContainer BuildHeroSkill()
    {
        string element = (_skillElement.Value ?? AttunmentsEffects.Burn).Trim().ToLowerInvariant();
        if (!AttunmentsEffects.TryGetElement(AttunmentsEffects.ArgsFor(element), out _))
            element = AttunmentsEffects.Burn;

        // Source = buff the casting hero. AllyOnly is for player-targeted cards.
        return new EffectContainer
        {
            id = HeroSkillId,
            containerType = EffectContainerType.skill,
            targetType = TargetType.Source,
            effects = new List<Effect>
            {
                AttunmentsEffects.CreateBuff(element, _skillValue.Value),
            },
        };
    }

    private Unit BuildCustomUnit(EffectContainer skill, string visualDonorId)
    {
        return new Unit
        {
            id = HeroUnitId,
            appearId = visualDonorId,
            assetRef = visualDonorId,
            title = "My Hero",
            pool = UnitPool.hero,
            team = TeamType.Team1,
            MaxHP = 150,
            currentHp = 150,
            BaseDamage = 25,
            BaseAttackRange = 1,
            baseAttackSpeed = 1f,
            movementSpeed = 3f,
            MaxMana = 100f,
            ManaRegen = 40f,
            currentMana = 0f,
            skillId = skill.id,
            skillLevel = 1,
            skills = new List<EffectContainer> { skill },
        };
    }

    /// <summary>Spawn path — AddUnitToTeam + isHero. Call from the config button, not during register.</summary>
    private void AddHeroUnitToBench()
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

        Unit unit = unitManager.GetUnitById(HeroUnitId);
        if (unit == null)
        {
            _log.LogError($"'{HeroUnitId}' not found after registration.");
            return;
        }

        if (_debug.Value)
            _log.LogInfo($"Spawning hero (donor={_visualDonorId.Value})");

        UnitBehaviour behaviour = unitManager.AddUnitToTeam(unit, wanderer: false);
        if (behaviour == null)
        {
            _log.LogError($"AddUnitToTeam failed for '{HeroUnitId}'.");
            return;
        }

        behaviour.isHero = true;

        if (_debug.Value)
            _log.LogInfo($"Spawned '{HeroUnitId}' as hero.");
    }

    private UnitManager GetUnitManager()
    {
        if (_unitManager != null) return _unitManager;
        _unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        return _unitManager;
    }

    /// <summary>Data-only: build Unit, register skill + allUnits. Do not CreateUnit / AddUnitToTeam here.</summary>
    private bool RegisterHeroUnit()
    {
        var unitManager = UnityEngine.Object.FindObjectOfType<UnitManager>();
        var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
        if (unitManager == null) return false;

        string donorId = _visualDonorId.Value?.Trim() ?? "";
        if (string.IsNullOrEmpty(donorId))
        {
            _log.LogError("Visual Donor Id is empty.");
            return false;
        }

        EffectContainer skill = BuildHeroSkill();
        if (relicManager != null)
            relicManager.AddOrReplaceEffectContainer(skill);

        Unit? unit = BuildCustomUnit(skill, donorId);

        if (unit == null)
        {
            _log.LogError($"Clone failed: GetUnitById('{donorId}') was null.");
            return false;
        }

        GameObject model = PoolManager.GetUnitPrefab(donorId);
        if (model != null)
            PoolManager.AddOrReplaceUnitPrefab($"{HeroUnitId}_prefab", model);

        unitManager.AddUnitToAllUnits(unit);

        if (unitManager.GetUnitById(HeroUnitId) == null)
        {
            _log.LogError($"'{HeroUnitId}' missing after AddUnitToAllUnits.");
            return false;
        }

        if (_debug.Value)
            _log.LogInfo($"Registered '{HeroUnitId}' donor={donorId} skill={skill.id}.");

        return true;
    }

    private void EnsureHeroUnitRegistered()
    {
        if (_heroRegistered) return;
        if (RegisterHeroUnit()) _heroRegistered = true;
    }
}
