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
    
    public enum LogType
    {
        FlyText,
        ScreenLog,
        Effect,
        FlyTextWrite
    }
}