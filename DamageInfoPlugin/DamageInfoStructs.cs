using System;
using System.Runtime.InteropServices;
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

    [StructLayout(LayoutKind.Explicit)]
    public struct EffectHeader
    {
        [FieldOffset(8)] public uint ActionId;
        [FieldOffset(28)] public ushort AnimationId;
        [FieldOffset(33)] public byte TargetCount;
    }
    
    public struct EffectEntry
    {
        public ActionEffectType type;
        public byte param0;
        public byte param1;
        public byte param2;
        public byte mult;
        public byte flags;
        public ushort value;

        public override string ToString()
        {
            return
                $"Type: {type}, p0: {param0}, p1: {param1}, p2: {param2}, mult: {mult}, flags: {flags} | {Convert.ToString(flags, 2)}, value: {value}";
        }
    }

    public struct EffectTail
    {
        
    }
    
    public struct ActionEffectInfo
    {
        public ActionStep step;
        public ulong tick;
        
        public uint actionId;
        public ActionEffectType type;
        public FlyTextKind kind;
        public uint sourceId;
        public ulong targetId;
        public uint value;
        public bool positionalSucceed;

        public override bool Equals(object o)
        {
            return
                o is ActionEffectInfo other
                && other.actionId == actionId
                && other.kind == kind
                && other.sourceId == sourceId
                && other.targetId == targetId
                && other.value == value
                && other.positionalSucceed == positionalSucceed;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(actionId, (int)kind, sourceId, targetId, value, positionalSucceed);
        }

        public override string ToString()
        {
            return
                $"actionId: {actionId} kind: {kind} ({(int)kind}) sourceId: {sourceId} (0x{sourceId:X}) targetId: {targetId} (0x{targetId:X}) value: {value} positionalSucceed: {positionalSucceed}";
        }
    }
}