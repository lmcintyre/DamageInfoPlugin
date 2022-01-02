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
}