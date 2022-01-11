using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace DamageInfoPlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
	    private bool _mainTargetCastBarColorEnabled;
	    public bool MainTargetCastBarColorEnabled
	    {
		    get => _mainTargetCastBarColorEnabled;
		    set
		    {
			    _dmgPlugin?.ResetMainTargetCastBar();
			    _mainTargetCastBarColorEnabled = value;
		    }
	    }

	    private bool _focusTargetCastBarColorEnabled;
	    public bool FocusTargetCastBarColorEnabled
	    {
		    get => _focusTargetCastBarColorEnabled;
		    set
		    {
			    _dmgPlugin?.ResetFocusTargetCastBar();
			    _focusTargetCastBarColorEnabled = value;
		    }
	    }
        
	    public int Version { get; set; } = 1;

	    public Vector4 PhysicalColor { get; set; } = new(1, 0, 0, 1);
	    public Vector4 MagicColor { get; set; } = new(0, 0, 1, 1);
	    public Vector4 DarknessColor { get; set; } = new(1, 0, 1, 1);
        
	    public Vector4 PhysicalCastColor { get; set; } = new(1, 0, 0, 1);
	    public Vector4 MagicCastColor { get; set; } = new(0, 0, 1, 1);
	    public Vector4 DarknessCastColor { get; set; } = new(1, 0, 1, 1);
        
	    public Vector4 PhysicalBgColor { get; set; } = new(1, 1, 1, 1);
	    public Vector4 MagicBgColor { get; set; } = new(1, 1, 1, 1);
	    public Vector4 DarknessBgColor { get; set; } = new(1, 1, 1, 1);

	    public bool DebugLogEnabled { get; set; }
        
	    public bool IncomingColorEnabled { get; set; } = true;
	    public bool OutgoingColorEnabled { get; set; } = true;
	    public bool PetColorEnabled { get; set; } = true;
        
	    public bool SourceTextEnabled { get; set; }
	    public bool PetSourceTextEnabled { get; set; }
	    public bool HealSourceTextEnabled { get; set; }

	    public bool IncomingAttackTextEnabled { get; set; } = true;
	    public bool OutgoingAttackTextEnabled { get; set; } = true;
	    public bool PetAttackTextEnabled { get; set; } = true;
	    public bool HealAttackTextEnabled { get; set; } = true;
        
        [NonSerialized]
        private DalamudPluginInterface _pluginInterface;

        [NonSerialized]
        private DamageInfoPlugin _dmgPlugin;

        public void Initialize(DalamudPluginInterface pluginInterface, DamageInfoPlugin dmgPlugin)
        {
            _pluginInterface = pluginInterface;
            _dmgPlugin = dmgPlugin;
        }

        public void Save()
        {
            _pluginInterface.SavePluginConfig(this);
        }
    }
}
