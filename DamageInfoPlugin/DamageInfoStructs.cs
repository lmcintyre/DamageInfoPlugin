using System;
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