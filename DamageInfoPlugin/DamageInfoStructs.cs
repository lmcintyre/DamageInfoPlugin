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
    
    public struct HijackStruct
    {
        public uint kind;
        public uint val1;
        public uint val2;
        public uint icon;
        public uint color;
        public IntPtr text1;
        public IntPtr text2;
        public float unk3;
    }
    
    public struct ActionEffectInfo
    {
        public uint actionId;
        public FlyTextKind kind;
        public uint sourceId;
        public uint targetId;
        public int value;

        public override bool Equals(object o)
        {
            return
                o is ActionEffectInfo other
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

        public override string ToString()
        {
            return
                $"actionId: {actionId} kind: {kind} ({(int)kind}) sourceId: {sourceId} (0x{sourceId:X}) targetId: {targetId} (0x{targetId:X}) value: {value}";
        }
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
}