namespace DamageInfoPlugin
{
    public enum ActionEffectType : byte
    {
        Nothing = 0,
        Miss = 1,
        FullResist = 2,
        Damage = 3,
        Heal = 4,
        BlockedDamage = 5,
        ParriedDamage = 6,
        Invulnerable = 7,
        NoEffectText = 8,
        Unknown_0 = 9,
        MpLoss = 10,
        MpGain = 11,
        TpLoss = 12,
        TpGain = 13,
        GpGain = 14,
        ApplyStatusEffectTarget = 15,
        ApplyStatusEffectSource = 16,
        StatusNoEffect = 20,
        StartActionCombo = 27,
        ComboSucceed = 28,
        Knockback = 33,
        Mount = 40,
        VFX = 59,
    };

    // members suffixed with a number seem to be a duplicate
    public enum FlyTextKind
    {
        // val1 in serif font, text1 sans-serif as subtitle
        AutoAttack, // used for autoas and incoming DoT damage

        // val1 in serif font, text1 sans-serif as subtitle
        // does a bounce effect on appearance
        DirectHit,

        // val1 in larger serif font with exclamation, text1 sans-serif as subtitle
        // does a bigger bounce effect on appearance
        CriticalHit,

        // val1 in even larger serif font with 2 exclamations, text1 sans-serif as subtitle
        // does a large bounce effect on appearance
        // does not scroll
        CriticalDirectHit,

        // AutoAttack with sans-serif text2 to the left of the val1
        NamedAttack,

        // DirectHit with sans-serif text2 to the left of the val1
        NamedDirectHit,

        // CriticalHit with sans-serif text2 to the left of the val1
        NamedCriticalHit,

        // CriticalDirectHit with sans-serif text2 to the left of the val1
        NamedCriticalDirectHit,

        // all caps serif MISS
        Miss,

        // sans-serif text2 next to all caps serif MISS
        NamedMiss,

        // all caps serif DODGE
        Dodge,

        // sans-serif text2 next to all caps serif DODGE
        NamedDodge,

        // icon next to sans-serif text2
        NamedIcon,
        NamedIcon2,

        // serif val1 with all caps condensed font EXP with text1 sans-serif as subtitle
        Exp,

        // sans-serif text2 next to serif val1 with all caps condensed font MP with text1 sans-serif as subtitle
        NamedMP,

        // sans-serif text2 next to serif val1 with all caps condensed font TP with text1 sans-serif as subtitle
        NamedTP,

        NamedAttack2, // used on HoTs, heals
        NamedMP2,
        NamedTP2,

        // sans-serif text2 next to serif val1 with all caps condensed font EP with text1 sans-serif as subtitle
        NamedEP,

        // displays nothing
        None,

        // all caps serif INVULNERABLE
        Invulnerable,

        // all caps sans-serif condensed font INTERRUPTED!
        // does a bounce effect on appearance
        // does not scroll
        Interrupted,

        // AutoAttack with no text1
        AutoAttackNoText,
        AutoAttackNoText2,
        CriticalHit2,
        AutoAttackNoText3,
        NamedCriticalHit2,

        // same as NamedCriticalHit with a green (cannot change) MP in condensed font to the right of val1
        // does a jiggle effect to the right on appearance
        NamedCriticalHitWithMP,

        // same as NamedCriticalHit with a yellow (cannot change) TP in condensed font to the right of val1
        // does a jiggle effect to the right on appearance
        NamedCriticalHitWithTP,

        // same as NamedIcon with sans-serif "has no effect!" to the right
        NamedIconHasNoEffect,

        // same as NamedIcon but text2 is slightly faded
        // used for buff expiry
        NamedIconFaded,
        NamedIconFaded2,

        // sans-serif text2
        Named,

        // same as NamedIcon with sans-serif "(fully resisted)" to the right
        NamedIconFullyResisted,

        // all caps serif INCAPACITATED!
        Incapacitated,

        // text2 with sans-serif "(fully resisted)" to the right
        NamedFullyResisted,

        // text2 with sans-serif "has no effect!" to the right
        NamedHasNoEffect,

        NamedAttack3,
        NamedMP3,
        NamedTP3,

        // same as NamedIcon with serif "INVULNERABLE!" beneath the text2
        NamedIconInvulnerable,

        // all caps serif RESIST
        Resist,

        // same as NamedIcon but places the given icon in the item icon outline
        NamedIconWithItemOutline,

        AutoAttackNoText4,
        CriticalHit3,

        // all caps serif REFLECT
        Reflect,

        // all caps serif REFLECTED
        Reflected,

        DirectHit2,
        CriticalHit5,
        CriticalDirectHit2,
    }

    public enum DamageType
    {
        Unknown = 0,
        Slashing = 1,
        Piercing = 2,
        Blunt = 3,
        Magic = 5,
        Darkness = 6,
        Physical = 7,
        LimitBreak = 8,
    }
}