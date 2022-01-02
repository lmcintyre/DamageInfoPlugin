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

        public bool DebugLogEnabled { get; set; } = false;

        private bool _colorEnabled = true;
        private bool _sourceTextEnabled = true;
        private bool _petSourceTextEnabled = false;
        private bool _mainTargetCastBarColorEnabled = false;
        private bool _focusTargetCastBarColorEnabled = false;
        private bool _incomingAttackTextEnabled = true;
        private bool _outgoingAttackTextEnabled = true;
        private bool _petAttackTextEnabled = true;

        public bool ColorEnabled
        {
	        get => _colorEnabled;
	        set => _colorEnabled = value;
        }

        public bool SourceTextEnabled {
	        get => _sourceTextEnabled;
	        set {
		        if (!value) {
				    _petSourceTextEnabled = false;
                }
		        _sourceTextEnabled = value;
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
        
        public bool IncomingAttackTextEnabled
        {
	        get => _incomingAttackTextEnabled;
	        set => _incomingAttackTextEnabled = value;
        }

        public bool OutgoingAttackTextEnabled
        {
	        get => _outgoingAttackTextEnabled;
	        set => _outgoingAttackTextEnabled = value;
        }

        public bool PetAttackTextEnabled
        {
	        get => _petAttackTextEnabled;
	        set => _petAttackTextEnabled = value;
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
