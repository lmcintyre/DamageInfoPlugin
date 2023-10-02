using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace DamageInfoPlugin;

public class PositionalTextSettings
{
	public string Prefix { get; set; } = "";
	public string Suffix { get; set; } = "";
	public bool PrefixEnabled { get; set; } = false;
	public bool SuffixEnabled { get; set; } = false;

	// yeah i know
	public bool IsPrefixEnabled()
	{
		return !string.IsNullOrEmpty(Prefix) && PrefixEnabled;
	}
	
	public bool IsSuffixEnabled()
	{
		return !string.IsNullOrEmpty(Suffix) && SuffixEnabled;
	}
	
	public bool AnyEnabled()
	{
		return PrefixEnabled || SuffixEnabled;
	}

	public Payload PrefixPayload() => string.IsNullOrEmpty(Prefix) ? null : new TextPayload(Prefix);
	public Payload SuffixPayload() => string.IsNullOrEmpty(Suffix) ? null : new TextPayload(Suffix);
}

public class PositionalSoundSettings
{
	public bool Enabled { get; set; } = false;
	public int SoundId { get; set; } = 1;
}
	
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
        
	public int Version { get; set; } = 3;

	public Vector4 PhysicalColor { get; set; } = new(1, 0, 0, 1);
	public Vector4 MagicColor { get; set; } = new(0, 0, 1, 1);
	public Vector4 DarknessColor { get; set; } = new(1, 0, 1, 1);
        
	public Vector4 PhysicalCastColor { get; set; } = new(1, 0, 0, 1);
	public Vector4 MagicCastColor { get; set; } = new(0, 0, 1, 1);
	public Vector4 DarknessCastColor { get; set; } = new(1, 0, 1, 1);
        
	public Vector4 PhysicalBgColor { get; set; } = new(1, 1, 1, 1);
	public Vector4 MagicBgColor { get; set; } = new(1, 1, 1, 1);
	public Vector4 DarknessBgColor { get; set; } = new(1, 1, 1, 1);
	    
	public bool IncomingColorEnabled { get; set; } = true;
	public bool OutgoingColorEnabled { get; set; } = true;
	public bool PetColorEnabled { get; set; } = true;
	public bool SeDamageIconDisable { get; set; } = false;
        
	public bool SourceTextEnabled { get; set; }
	public bool PetSourceTextEnabled { get; set; }
	public bool HealSourceTextEnabled { get; set; }

	public bool IncomingAttackTextEnabled { get; set; } = true;
	public bool OutgoingAttackTextEnabled { get; set; } = true;
	public bool PetAttackTextEnabled { get; set; } = true;
	public bool HealAttackTextEnabled { get; set; } = true;

	// Positionals
	[Obsolete("PositionalColorEnabled is retired in favor of PositionalHitColorEnabled and PositionalMissColorEnabled")]
	public bool PositionalColorEnabled { get; set; } = true;
	    
	[Obsolete("PositionalColorInvert is retired in favor of using the appropriate settings for hit and miss colors.")]
	public bool PositionalColorInvert { get; set; } = false;
	    
	[Obsolete("PositionalColor is retired in favor of PositionalHitColor and PositionalMissColor")]
	public Vector4 PositionalColor { get; set; } = new(0, 1, 0, 1);
	    
	public Vector4 PositionalHitColor { get; set; } = new(0, 1, 0, 1);
	public Vector4 PositionalMissColor { get; set; } = new(1, 0, 0, 1);
	public bool PositionalHitColorEnabled { get; set; } = true;
	public bool PositionalMissColorEnabled { get; set; } = true;

	public PositionalTextSettings PositionalHitTextSettings { get; set; } = new();
	public PositionalTextSettings PositionalMissTextSettings { get; set; } = new();
	public bool PositionalAttackTextOverrideEnabled { get; set; } = false;
	
	public PositionalSoundSettings PositionalHitSoundSettings { get; set; } = new();
	public PositionalSoundSettings PositionalMissSoundSettings { get; set; } = new();

	public bool AnyPositionalTextEnabled()
	{
		return PositionalHitTextSettings.AnyEnabled() || PositionalMissTextSettings.AnyEnabled();
	}

	public bool AnyPositionalSoundEnabled()
	{
		return PositionalHitSoundSettings.Enabled || PositionalMissSoundSettings.Enabled;
	}
	
	public bool DebugLogEnabled { get; set; }
	    
	public Fools2023Config Fools2023Config { get; set; } = new();

	[NonSerialized]
	private DamageInfoPlugin _dmgPlugin;

	public void Initialize(DamageInfoPlugin dmgPlugin)
	{
		_dmgPlugin = dmgPlugin;
	}

	public void Save()
	{
		DalamudApi.PluginInterface.SavePluginConfig(this);
	}
}