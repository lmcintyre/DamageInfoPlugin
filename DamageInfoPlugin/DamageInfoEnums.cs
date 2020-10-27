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
        AutoAttack = 0, // used for autoas and incoming DoT damage

        // val1 in serif font, text1 sans-serif as subtitle
        // does a bounce effect on appearance
        DirectHit = 1,

        // val1 in larger serif font with exclamation, text1 sans-serif as subtitle
        // does a bigger bounce effect on appearance
        CriticalHit = 2,

        // val1 in even larger serif font with 2 exclamations, text1 sans-serif as subtitle
        // does a large bounce effect on appearance
        // does not scroll
        CriticalDirectHit = 3,

        // AutoAttack with sans-serif text2 to the left of the val1
        NamedAttack = 4,

        // DirectHit with sans-serif text2 to the left of the val1
        NamedDirectHit = 5,

        // CriticalHit with sans-serif text2 to the left of the val1
        NamedCriticalHit = 6,

        // CriticalDirectHit with sans-serif text2 to the left of the val1
        NamedCriticalDirectHit = 7,

        // all caps serif MISS
        Miss = 8,

        // sans-serif text2 next to all caps serif MISS
        NamedMiss = 9,

        // all caps serif DODGE
        Dodge = 10,

        // sans-serif text2 next to all caps serif DODGE
        NamedDodge = 11,

        // icon next to sans-serif text2
        NamedIcon = 12,
        NamedIcon2 = 13,

        // serif val1 with all caps condensed font EXP with text1 sans-serif as subtitle
        Exp = 14,

        // sans-serif text2 next to serif val1 with all caps condensed font MP with text1 sans-serif as subtitle
        NamedMP = 15,

        // sans-serif text2 next to serif val1 with all caps condensed font TP with text1 sans-serif as subtitle
        NamedTP = 16,

        NamedAttack2 = 17, // used on HoTs, heals
        NamedMP2 = 18,
        NamedTP2 = 19,

        // sans-serif text2 next to serif val1 with all caps condensed font EP with text1 sans-serif as subtitle
        NamedEP = 20,

        // displays nothing
        None = 21,

        // all caps serif INVULNERABLE
        Invulnerable = 22,

        // all caps sans-serif condensed font INTERRUPTED!
        // does a bounce effect on appearance
        // does not scroll
        Interrupted = 23,

        // AutoAttack with no text1
        AutoAttackNoText = 24,
        AutoAttackNoText2 = 25,
        CriticalHit2 = 26,
        AutoAttackNoText3 = 27,
        NamedCriticalHit2 = 28,

        // same as NamedCriticalHit with a green (cannot change) MP in condensed font to the right of val1
        // does a jiggle effect to the right on appearance
        NamedCriticalHitWithMP = 29,

        // same as NamedCriticalHit with a yellow (cannot change) TP in condensed font to the right of val1
        // does a jiggle effect to the right on appearance
        NamedCriticalHitWithTP = 30,

        // same as NamedIcon with sans-serif "has no effect!" to the right
        NamedIconHasNoEffect = 31,

        // same as NamedIcon but text2 is slightly faded
        // used for buff expiry
        NamedIconFaded = 32,
        NamedIconFaded2 = 33,

        // sans-serif text2
        Named = 34,

        // same as NamedIcon with sans-serif "(fully resisted)" to the right
        NamedIconFullyResisted = 35,

        // all caps serif INCAPACITATED!
        Incapacitated = 36,

        // text2 with sans-serif "(fully resisted)" to the right
        NamedFullyResisted = 37,

        // text2 with sans-serif "has no effect!" to the right
        NamedHasNoEffect = 38,

        NamedAttack3 = 39,
        NamedMP3 = 40,
        NamedTP3 = 41,

        // same as NamedIcon with serif "INVULNERABLE!" beneath the text2
        NamedIconInvulnerable = 42,

        // all caps serif RESIST
        Resist = 43,

        // same as NamedIcon but places the given icon in the item icon outline
        NamedIconWithItemOutline = 44,

        AutoAttackNoText4 = 45,
        CriticalHit3 = 46,

        // all caps serif REFLECT
        Reflect = 47,

        // all caps serif REFLECTED
        Reflected = 48,

        DirectHit2 = 49,
        CriticalHit5 = 50,
        CriticalDirectHit2 = 51,
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