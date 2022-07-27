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
                $"Type: {type}, p0: {param0:D3}, p1: {param1:D3}, p2: {param2:D3} 0x{param2:X2} '{Convert.ToString(param2, 2).PadLeft(8, '0')}', mult: {mult:D3}, flags: {flags:D3} | {Convert.ToString(flags, 2).PadLeft(8, '0')}, value: {value:D6}";
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
        public PositionalState positionalState;

        public override bool Equals(object o)
        {
            return
                o is ActionEffectInfo other
                && other.actionId == actionId
                && other.kind == kind
                && other.sourceId == sourceId
                && other.targetId == targetId
                && other.value == value
                && other.positionalState == positionalState;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(actionId, (int)kind, sourceId, targetId, value, positionalState);
        }

        public override string ToString()
        {
            return
                $"actionId: {actionId} kind: {kind} ({(int)kind}) sourceId: {sourceId} (0x{sourceId:X}) targetId: {targetId} (0x{targetId:X}) value: {value} positionalState: {positionalState}";
        }
    }
}