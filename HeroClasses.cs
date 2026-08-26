using System.Collections.Generic;
using HadeanTactics;

namespace AttunementsNature;

/// <summary>
/// A hero class / affinity path. Each concrete class lives in its own type.
/// Skills are produced by a factory (not a shared static instance) because they scale
/// with level, depend on runtime config, and the game deep-copies/mutates EffectContainers.
/// </summary>
public interface IHeroClass
{
    int Index { get; }
    string Name { get; }
    AllianceType Alliance { get; }

    /// <summary>Fresh skill container for this class at the given style level.</summary>
    EffectContainer BuildSkill(string heroId, int level, int baseValue);
}

/// <summary>Shared helper so each class only supplies its effects.</summary>
public abstract class HeroClassBase : IHeroClass
{
    public int Index { get; }
    public abstract string Name { get; }
    public AllianceType Alliance { get; }
    protected readonly string _element;

    protected HeroClassBase(int index, AllianceType alliance, string element = "Burn")
    {
        Index = index;
        Alliance = alliance;
        _element = element;
    }

    protected abstract TargetType Target { get; }
    protected abstract List<Effect> BuildEffects(int level, int baseValue);

    /// <summary>Tooltip body. Use {value} placeholders if you want vanilla effect substitution later.</summary>
    protected virtual string BuildDescription(int level, int baseValue) =>
        $"{Name} (level {level}).";

    public virtual EffectContainer BuildSkill(string heroId, int level, int baseValue)
    {
        // pureId is stable across levels so MergeAndUpgrade stacks into one skill slot.
        // id includes level (vanilla pattern) so relic entries and loca keys stay distinct.
        string pureId = $"skill_{heroId}_{Index}";
        string title = $"{Name} {level}";
        string description = BuildDescription(level, baseValue);
        var skill = new EffectContainer
        {
            id = $"{pureId}_{level}",
            pureId = pureId,
            title = title,
            description = description,
            containerType = EffectContainerType.skill,
            targetType = Target,
            level = level,
            effects = BuildEffects(level, baseValue),
        };
        CustomHeroSkillDisplay.Register(skill);
        return skill;
    }
}

/// <summary>Attunment: self-buff so this unit's attacks apply an element (scales with level).</summary>
public sealed class AttunmentClass : HeroClassBase
{
    public override string Name => $"{char.ToUpper(_element[0])}{_element.Substring(1)} Attunment";

    public AttunmentClass(int index, string element, AllianceType alliance)
        : base(index, alliance, element) { }

    protected override TargetType Target => TargetType.Source;

    protected override List<Effect> BuildEffects(int level, int baseValue) => new()
    {
        AttunementsEffects.CreateBuff(_element, baseValue * level),
    };

    protected override string BuildDescription(int level, int baseValue) =>
    $"Attacks apply {baseValue * level} {_element}.";
}

/// <summary>
/// Element cloak: encases the caster in ice (self-Freeze = invulnerable but inert, pierced only
/// by iceShatter) while a burning aura pulses adjacent enemies. Freeze duration, aura burn amount,
/// and aura duration all scale with level.
/// </summary>
public sealed class ElementCloakClass : HeroClassBase
{
    public override string Name => "Element Cloak";

    public ElementCloakClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;

    protected override List<Effect> BuildEffects(int level, int baseValue)
    {
        // Freeze uses the default Duration type: value is ignored, the float sets how many
        // seconds the caster stays frozen (invulnerable + unable to act) before auto-unfreezing.
        float freezeSeconds = level;

        // AuraBurn ticks once per second while active: each pulse applies Burn = value to adjacent
        // enemies, and the aura lives for `duration` seconds. Per-second ticks keep firing even
        // while the caster is frozen, so the aura keeps burning during the invuln window.
        int burnPerPulse = baseValue * level;
        float auraSeconds = level;

        return new List<Effect>
        {
            new Effect(EffectType.Freeze, freezeSeconds),
            new Effect(EffectType.AuraBurn, burnPerPulse, auraSeconds),
        };
    }

    protected override string BuildDescription(int level, int baseValue) =>
        $"Freezes the caster for {level} seconds, and applies {baseValue * level} burn to adjacent enemies for {level} seconds.";
}

/// <summary>
/// Elemental traps: STUB — currently burns all enemies. Replace with a real trap summon
/// (EffectType.Trap + a custom trap unit) once trap units are authored.
/// </summary>
public sealed class ElementalTrapsClass : HeroClassBase
{
    public override string Name => "Elemental Traps";

    public ElementalTrapsClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.AllEnemies;

    protected override List<Effect> BuildEffects(int level, int baseValue) => new()
    {
        // TODO: swap for EffectType.Trap + custom elemental trap unit.
        new Effect(EffectType.Burn, baseValue * level) { dMod = new DamageMod() },
    };

    protected override string BuildDescription(int level, int baseValue) =>
    $"Burns all enemies for {baseValue * level}.";
}

/// <summary>Roots: strangle all enemies — silence + damage + enroot (scales with level).</summary>
public sealed class RootsClass : HeroClassBase
{
    public override string Name => "Roots";

    public RootsClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.AllEnemies;

    protected override List<Effect> BuildEffects(int level, int baseValue)
    {
        var effects = new List<Effect>();

        if (level >= 1)
        {
            effects.Add(new Effect(EffectType.DealDamage, baseValue * level) { dMod = new DamageMod() });
            effects.Add(new Effect(EffectType.Enroot, (float)level));
        }

        if (level >= 3)
        {
            effects.Add(new Effect(EffectType.Silence, (float)level));
        }

        return effects;
    }

    protected override string BuildDescription(int level, int baseValue) {
        if (level >= 3)
        {
            return $"Deals {baseValue * level} damage, enroots all enemies for {level} seconds, and silences all enemies for {level} seconds.";
        }
        return $"Deals {baseValue * level} damage, and enroots all enemies for {level} seconds.";
    }
}


/// Beast Hero Classes

/// <summary>
/// Devourer: execute enemies below an HP% threshold (value scales with level).
/// Uses ExecuteEffects — EffectType.Execution + args "mod_execute", Harmony-patched.
/// </summary>
public sealed class DevourerClass : HeroClassBase
{
    public override string Name => "Devourer";

    public DevourerClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    // Single-target execute reads clearer than AllEnemies for a finisher.
    protected override TargetType Target => TargetType.EnemyOnly;

    protected override List<Effect> BuildEffects(int level, int baseValue)
    {
        // value = % of MaxHP the target must be under. e.g. baseValue=15, level=2 → below 30%.
        int thresholdPercent = baseValue * level;
        return new List<Effect>
        {
            ExecuteEffects.Create(thresholdPercent),
        };
    }

    protected override string BuildDescription(int level, int baseValue) =>
    $"Executes enemies below {baseValue * level}% of their MaxHP.";
}

/// <summary>
/// Ferocious: .
/// </summary>
public sealed class FerociousClass : HeroClassBase
{
    public override string Name => "Ferocious";

    public FerociousClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;

    protected override List<Effect> BuildEffects(int level, int baseValue)
    {
        var effects = new List<Effect>();

        if (level >= 1)
        {
            effects.Add(new Effect(EffectType.Haste, baseValue * level));
        }
        if (level >= 2)
        {
            effects.Add(new Effect(EffectType.LifeSteal, baseValue * level));
        }
        if (level >= 3)
        {
            effects.Add(new Effect(EffectType.GainDamage, baseValue * level));
        }

        return effects;
    }

    protected override string BuildDescription(int level, int baseValue) {
        if (level >= 3)
        {
            return $"Hastes the caster for {baseValue * level} seconds, gains life steal, and gains {baseValue * level} damage.";
        }
        if (level >= 2)
        {
            return $"Hastes the caster for {baseValue * level} seconds, and gains life steal.";
        }
        return $"Hastes the caster for {baseValue * level} seconds.";
    }
}

/// <summary>
/// Hunter: deal damage and leach HP (value scales with level).
/// </summary>
public sealed class HunterClass : HeroClassBase
{
    public override string Name => "Hunter";

    public HunterClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    // Single-target damage reads clearer than AllEnemies for a finisher.
    protected override TargetType Target => TargetType.EnemyOnly;

    protected override List<Effect> BuildEffects(int level, int baseValue)
    {
        return new List<Effect>
        {
            new Effect(EffectType.DealDamage, baseValue * level) { dMod = new DamageMod() },
            new Effect(EffectType.Leach, baseValue * level) { 
                dMod = new DamageMod(), 
                value = baseValue * level,
            },
        };
    }

    protected override string BuildDescription(int level, int baseValue) =>
    $"Deals {baseValue * level} damage and leaches {baseValue * level} HP.";
}

/// <summary>
/// Beast Call: summon a beast (value scales with level).
/// </summary>
public sealed class BeastSummonClass : HeroClassBase
{
    public override string Name => "Beast Call";

    public BeastSummonClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;

    // One skill slot: each level adds another summon (Stack/Sync keep a single pureId).
    protected override List<Effect> BuildEffects(int level, int baseValue)
    {
        var effects = new List<Effect>();
        if (level >= 1)
            effects.Add(new Effect(EffectType.CreateUnit, 1) { args = "wolf" });
        if (level >= 2)
            effects.Add(new Effect(EffectType.CreateUnit, 1) { args = "bear" });
        if (level >= 3)
            effects.Add(new Effect(EffectType.CreateUnit, 1) { args = "spider" });
        return effects;
    }

    protected override string BuildDescription(int level, int baseValue) {
        if (level >= 3)
        {
            return $"Summons a wolf, bear, and spider.";
        }
        if (level >= 2)
        {
            return $"Summons a wolf and a bear.";
        }
        return $"Summons a wolf.";
    }
}

/// Human Hero Classes

/// <summary>Sigils: create a sigil (value scales with level).</summary>
public sealed class SigilsClass : HeroClassBase
{
    public override string Name => "Sigils";

    public SigilsClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;
    protected override List<Effect> BuildEffects(int level, int baseValue) => new();

    protected override string BuildDescription(int level, int baseValue) =>
    $"Creates a sigil.";
}

/// <summary>
/// Potions: create a potion (value scales with level).
/// </summary>
public sealed class PotionsClass : HeroClassBase
{
    public override string Name => "Potions";

    public PotionsClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;
    protected override List<Effect> BuildEffects(int level, int baseValue) => new();

    protected override string BuildDescription(int level, int baseValue) =>
    $"Creates a potion.";
}

/// <summary>
/// Food: create a food (value scales with level).
/// </summary>
public sealed class FoodClass : HeroClassBase
{
    public override string Name => "Food";

    public FoodClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;
    protected override List<Effect> BuildEffects(int level, int baseValue) => new();

    protected override string BuildDescription(int level, int baseValue) =>
    $"Creates a food.";
}

/// <summary>
/// Perma: create a perma (value scales with level).
/// </summary>
public sealed class PermaClass : HeroClassBase
{
    public override string Name => "Perma";

    public PermaClass(int index, AllianceType alliance)
        : base(index, alliance) { }

    protected override TargetType Target => TargetType.Source;
    protected override List<Effect> BuildEffects(int level, int baseValue) => new();

    protected override string BuildDescription(int level, int baseValue) =>
    $"Creates a perma.";
}

/// <summary>
/// A hero definition: base stats + its class/affinity paths. Each hero gets its own type.
/// </summary>
public abstract class HeroDefinition
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract IReadOnlyList<IHeroClass> Classes { get; }

    public virtual int MaxHP => 150;
    public virtual int BaseDamage => 25;
    public virtual int BaseAttackRange => 1;
    public virtual float BaseAttackSpeed => 1f;
    public virtual float MovementSpeed => 3f;
    public virtual float MaxMana => 100f;
    public virtual float ManaRegen => 40f;

    // Per-level stat deltas merged in when the player upgrades a style.
    public virtual int UpgradeHp => 25;
    public virtual int UpgradeDamage => 5;

    /// <summary>Vanilla unit id used for portrait/model until this hero has its own art.</summary>
    public virtual string AppearId => "inquisitor";
}

/// <summary>The Attunment hero and its four classes.</summary>
public sealed class AttunmentHero : HeroDefinition
{
    public override string Id => "elemental";
    public override string Title => ", The Elemental";

    public override IReadOnlyList<IHeroClass> Classes { get; } = new IHeroClass[]
    {
        new AttunmentClass(0, AttunementsEffects.Burn, AllianceType.bright),
        new ElementCloakClass(1, AllianceType.mystic),
        new ElementalTrapsClass(2, AllianceType.hunter),
        new RootsClass(3, AllianceType.disruptor),
    };

    public override string AppearId => "inquisitor_white";
}

/// <summary>The Beast hero and its four classes.</summary>
public sealed class BeastHero : HeroDefinition
{
    public override string Id => "beast";
    public override string Title => ", The Beast";

    public override IReadOnlyList<IHeroClass> Classes { get; } = new IHeroClass[]
    {
        new BeastSummonClass(0, AllianceType.bright),
        new HunterClass(1, AllianceType.mystic),
        new FerociousClass(2, AllianceType.brute), // TODO: hunter-specific traps
        new DevourerClass(3, AllianceType.disruptor),
    };

    
    public override string AppearId => "nightshade_pink";
}


/// <summary>The Human hero and its four classes.</summary>
public sealed class HumanHero : HeroDefinition
{
    public override string Id => "scholar";
    public override string Title => ", The Scholar";

    public override IReadOnlyList<IHeroClass> Classes { get; } = new IHeroClass[]
    {
        new SigilsClass(0, AllianceType.bright),
        new PotionsClass(1, AllianceType.mystic),
        new FoodClass(2, AllianceType.hunter),
        new PermaClass(3, AllianceType.disruptor),
    };

    public override string AppearId => "warlock_necromancer";
}

