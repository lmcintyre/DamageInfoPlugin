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

        public bool EffectLogEnabled { get; set; } = true;
        public bool FlyTextLogEnabled { get; set; } = true;
        private bool _TextColoringEnabled = true;

        public bool TextColoringEnabled {
	        get => _TextColoringEnabled;
	        set {
                if (value == false)
                    plugin?.ClearFlyTextQueue();
                _TextColoringEnabled = value;
	        }
        }

        public Vector3 PhysicalColor { get; set; } = new Vector3(1, 0, 0);
        public Vector3 MagicColor { get; set; } = new Vector3(0, 0, 1);
        public Vector3 DarknessColor { get; set; } = new Vector3(1, 0, 1);
        
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized]
        private DamageInfoPlugin plugin;

        public void Initialize(DalamudPluginInterface pluginInterface, DamageInfoPlugin dmgPlugin)
        {
            this.pluginInterface = pluginInterface;
            this.plugin = dmgPlugin;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
