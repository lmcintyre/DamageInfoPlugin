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

        private bool _incomingColorEnabled = true;
        private bool _outgoingColorEnabled = true;
        private bool _petDamageColorEnabled = true;
        private bool _sourceTextEnabled = true;
        private bool _petSourceTextEnabled = false;

        public bool IncomingColorEnabled {
	        get => _incomingColorEnabled;
	        set {
		        if (!value) {
                    if (!_outgoingColorEnabled)
						dmgPlugin?.ClearFlyTextQueue();
                }
                    
                _incomingColorEnabled = value;
	        }
        }

        public bool OutgoingColorEnabled
        {
	        get => _outgoingColorEnabled;
	        set
	        {
		        if (!value) {
                    if (!_incomingColorEnabled)
						dmgPlugin?.ClearFlyTextQueue();
			        _petDamageColorEnabled = false;
		        }
			        
		        _outgoingColorEnabled = value;
	        }
        }

        public bool SourceTextEnabled {
	        get => _sourceTextEnabled;
	        set {
		        if (!value) {
			        dmgPlugin?.ClearTextPtrs();
				    _petSourceTextEnabled = false;
                }
		        _sourceTextEnabled = value;
	        }
        }

        public bool PetDamageColorEnabled {
	        get => _petDamageColorEnabled;
	        set {
		        if (value)
			        _outgoingColorEnabled = true;
		        _petDamageColorEnabled = value;
	        }
        }

        public bool PetSourceTextEnabled {
	        get => _petSourceTextEnabled;
	        set {
		        if (value)
			        _sourceTextEnabled = true;
		        _petSourceTextEnabled = value;
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
