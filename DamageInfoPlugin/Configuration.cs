using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace DamageInfoPlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool EffectLogEnabled { get; set; } = false;
        public bool FlyTextLogEnabled { get; set; } = false;
        public bool SourceTextEnabled { get; set; } = false;
        private bool _TextColoringEnabled = true;

        public bool TextColoringEnabled {
	        get => _TextColoringEnabled;
	        set {
                if (value == false)
                    dmgPlugin?.ClearFlyTextQueue();
                _TextColoringEnabled = value;
	        }
        }

        public Vector4 PhysicalColor { get; set; } = new Vector4(1, 0, 0, 1);
        public Vector4 MagicColor { get; set; } = new Vector4(0, 0, 1, 1);
        public Vector4 DarknessColor { get; set; } = new Vector4(1, 0, 1, 1);
        
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized]
        private DamageInfoPlugin dmgPlugin;

        public void Initialize(DalamudPluginInterface pluginInterface, DamageInfoPlugin dmgPlugin)
        {
            this.pluginInterface = pluginInterface;
            this.dmgPlugin = dmgPlugin;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
