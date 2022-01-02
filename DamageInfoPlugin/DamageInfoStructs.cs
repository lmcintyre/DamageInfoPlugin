using System;
using Dalamud.Game.Gui.FlyText;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DamageInfoPlugin
{
    public unsafe struct CastbarInfo
    {
        public AtkUnitBase* unitBase;
        public AtkImageNode* gauge;
        public AtkImageNode* bg;
    }

    public struct ScreenLogInfo
    {
        public uint actionId;
        public FlyTextKind kind;
        public uint sourceId;
        public uint targetId;
        public int value;

        public override bool Equals(object o)
        {
            return
                o is ScreenLogInfo other
                && other.actionId == actionId
                && other.kind == kind
                && other.sourceId == sourceId
                && other.targetId == targetId
                && other.value == value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(actionId, (int)kind, sourceId, targetId, value);
        }
    }

    // public struct EffectEntry
    // {
    //     public ActionEffectType type;
    //     public byte param0;
    //     public byte param1;
    //     public byte param2;
    //     public byte mult;
    //     public byte flags;
    //     public ushort value;
    //
    //     public override string ToString()
    //     {
    //         return
    //             $"Type: {type}, p0: {param0}, p1: {param1}, p2: {param2}, mult: {mult}, flags: {flags} | {Convert.ToString(flags, 2)}, value: {value}";
    //     }
    // }
}