using System;

namespace DamageInfoPlugin
{
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

    public unsafe struct AtkResNodeColors
    {
        /* 00 */ public uint color;
        /* 04 */ public float local_depth;
        /* 08 */ public float global_depth;
        /* 12 */ public fixed short local_add_color[3];
        /* 18 */ public fixed short global_add_color[3];
        /* 24 */ public fixed byte local_mul_color[3];
        /* 27 */ public fixed byte global_mul_color[3];

        public override string ToString()
        {
            return
                $"color: {color} " +
                $"local add: r {local_add_color[0]} g {local_add_color[1]} b {local_add_color[2]} " +
                $"global add: r {global_add_color[0]} g {global_add_color[1]} b {global_add_color[2]} " +
                $"local mul: r {local_mul_color[0]} g {local_mul_color[1]} b {local_mul_color[2]} " +
                $"global mul: r {global_mul_color[0]} g {global_mul_color[1]} b {global_mul_color[2]}";
        }
    }
}