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
        
        public Vector4 PhysicalColor { get; set; } = new Vector4(1, 0, 0, 1);
        public Vector4 MagicColor { get; set; } = new Vector4(0, 0, 1, 1);
        public Vector4 DarknessColor { get; set; } = new Vector4(1, 0, 1, 1);
        
        public Vector4 PhysicalCastColor { get; set; } = new Vector4(1, 0, 0, 1);
        public Vector4 MagicCastColor { get; set; } = new Vector4(0, 0, 1, 1);
        public Vector4 DarknessCastColor { get; set; } = new Vector4(1, 0, 1, 1);
        
        public Vector4 PhysicalBgColor { get; set; } = new Vector4(1, 1, 1, 1);
        public Vector4 MagicBgColor { get; set; } = new Vector4(1, 1, 1, 1);
        public Vector4 DarknessBgColor { get; set; } = new Vector4(1, 1, 1, 1);

        public bool EffectLogEnabled { get; set; } = false;
        public bool FlyTextLogEnabled { get; set; } = false;

        private bool _incomingColorEnabled = true;
        private bool _outgoingColorEnabled = true;
        private bool _petDamageColorEnabled = true;
        private bool _sourceTextEnabled = true;
        private bool _petSourceTextEnabled = false;
        private bool _mainTargetCastBarColorEnabled = false;
        private bool _focusTargetCastBarColorEnabled = false;

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

        public bool MainTargetCastBarColorEnabled
        {
	        get => _mainTargetCastBarColorEnabled;
	        set
	        {
		        dmgPlugin?.ResetMainTargetCastBar();
		        _mainTargetCastBarColorEnabled = value;
	        }
        }
        public bool FocusTargetCastBarColorEnabled
        {
	        get => _focusTargetCastBarColorEnabled;
	        set
	        {
		        dmgPlugin?.ResetFocusTargetCastBar();
		        _focusTargetCastBarColorEnabled = value;
	        }
        }
        
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
