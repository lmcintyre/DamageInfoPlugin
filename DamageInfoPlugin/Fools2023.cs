using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Data;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;

namespace DamageInfoPlugin;

public class Fools2023Config
{
	public bool Unlocked { get; set; } = false;
	public bool Enabled { get; set; } = false;
	public int Frequency { get; set; } = 10;

	public Dictionary<int, string> DamageTypes { get; set; } = new();

	public void LoadDefaultRareDamageTypes()
	{
		DamageTypes.Add(60376, "Bunny");
		DamageTypes.Add(65100, "Carrot");
		DamageTypes.Add(93550, "Karakul");
		DamageTypes.Add(61396, "XIV");
		DamageTypes.Add(61432, "MSQ");
		DamageTypes.Add(76612, "Musical");
		DamageTypes.Add(76972, "Shiba");
		DamageTypes.Add(60752, "House");
		DamageTypes.Add(64501, "Indifferent");
		DamageTypes.Add(62576, "Omni-class");
		DamageTypes.Add(61582, "GM");
		DamageTypes.Add(61581, "Yoshi-P");
		DamageTypes.Add(61573, "Sprout");
		DamageTypes.Add(61511, "AFK");
		DamageTypes.Add(60346, "Miner");
		DamageTypes.Add(76979, "Penguin");
		DamageTypes.Add(4839, "Grebuloff");
		DamageTypes.Add(900, "BUDDY");
		DamageTypes.Add(61545, "RP");
		DamageTypes.Add(66400, "APP-EAL");
		DamageTypes.Add(26559, "Chest Coffer");
		DamageTypes.Add(80178, "Ragnarok");
		DamageTypes.Add(60436, "Bedge");
		DamageTypes.Add(61503, "90k'd");
		DamageTypes.Add(65918, "Crab");
		DamageTypes.Add(10402, "Cleric Stance");
		DamageTypes.Add(61832, "Ultimate");
		DamageTypes.Add(76725, "Namazu");
	}
}

public static class Fools2023
{
	private const string SettingsHeader = "April Fools 2023 (Rare Damage Types)";
	private static readonly Random _r = new(Environment.TickCount);
	private static readonly Dictionary<int, TextureWrap> _textures = new();
	private static Configuration _configuration;
	private static Fools2023Config _config;
	private static DataManager _dataManager;

	private static int _editIconId;
	private static string _editName = "";

	public static void Initialize(Configuration configuration, DataManager dataManager)
	{
		_configuration = configuration;
		_config = configuration.Fools2023Config;
		_dataManager = dataManager;
	}

	public static void Dispose()
	{
		foreach (var kv in _textures)
		{
			kv.Value.Dispose();
		}
	}
	
	public static void Unlock()
	{
		_config.LoadDefaultRareDamageTypes();
		_config.Unlocked = true;
		_configuration.Save();
	}

	private static bool DrawConfigDisabled(bool failCondition, string tooltip)
	{
		if (failCondition)
		{
			ImGui.BeginDisabled(true);
			ImGui.CollapsingHeader(SettingsHeader);
			var hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
			ImGui.EndDisabled();
			if (hover)
			{
				ImGui.SetTooltip(tooltip);
			}
		};
		return failCondition;
	}

	public static void DrawConfig()
	{
		if (DrawConfigDisabled(!_config.Unlocked, "Unlock this feature by using the command '/dmginfo fools2023' in chat.")) return;
		if (DrawConfigDisabled(_configuration.SeDamageIconDisable, "Enable vanilla damage icons under the Flytext category to use this feature.")) return;

		if (ImGui.CollapsingHeader("April Fools 2023 (Rare Damage Types)"))
		{
			var foolsConfigValue = _config.Enabled;
			var foolsFrequencyValue = _config.Frequency;

			if (ImGui.Checkbox("Enable new rare damage types", ref foolsConfigValue))
			{
				_config.Enabled = foolsConfigValue;
				_configuration.Save();
			}

			if (foolsConfigValue)
			{
				ImGui.TextUnformatted("Rare damage type occurrence chance:");
				if (ImGui.SliderInt("##foolsfreq", ref foolsFrequencyValue, 1, 10))
				{
					_config.Frequency = foolsFrequencyValue;
					_configuration.Save();
				}
				ImGui.TextUnformatted($"A rare damage type will occur for 1 in every {foolsFrequencyValue} named attacks.");
			}

			ImGui.BeginChild("##fools2023list", ImGuiHelpers.ScaledVector2(0, 300), true);
			ImGui.Columns(4);
			ImGui.TextUnformatted("Icon ID");
			ImGui.NextColumn();
			ImGui.TextUnformatted("Icon");
			ImGui.NextColumn();
			ImGui.TextUnformatted("Name");
			ImGui.NextColumn();
			ImGui.TextUnformatted("Delete");
			ImGui.NextColumn();
			ImGui.Separator();
			foreach (var damageType in _config.DamageTypes)
			{
				ImGui.TextUnformatted($"{damageType.Key}");
				ImGui.NextColumn();

				if (!_textures.TryGetValue(damageType.Key, out var img))
				{
					Task.Run(() =>
					{
						PluginLog.Debug($"Loading icon {damageType.Key}");
						_textures[damageType.Key] = _dataManager.GetImGuiTexture(GetIcon(damageType.Key));
					});
				}
				else if (img != null)
				{
					ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 8 * ImGuiHelpers.GlobalScale);
					ImGui.Image(img.ImGuiHandle, ImGuiHelpers.ScaledVector2(32, 32));
					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8 * ImGuiHelpers.GlobalScale);
				}
				ImGui.NextColumn();
				ImGui.TextUnformatted(damageType.Value);
				ImGui.NextColumn();
				if (ImGui.Button($"-##fools2023delete{damageType.Value}"))
				{
					_config.DamageTypes.Remove(damageType.Key);
					_configuration.Save();
				}

				ImGui.NextColumn();
			}
			ImGui.InputInt("##fools2023iconid", ref _editIconId, 0, 0);
			ImGui.NextColumn();
			ImGui.NextColumn();
			if (!IconExists(_editIconId) || _editIconId == 0)
			{
				ImGui.TextUnformatted("Invalid Icon ID");
			} else {
				if (ImGui.InputText("##fools2023name", ref _editName, 32, ImGuiInputTextFlags.EnterReturnsTrue))
				{
					_config.DamageTypes.Add(_editIconId, _editName);
					_editIconId = 0;
					_editName = "";	
				}
			}
			ImGui.EndDisabled();
			ImGui.NextColumn();
			if (ImGui.Button("+##fools2023add"))
			{
				_config.DamageTypes.Add(_editIconId, _editName);
				_editIconId = 0;
				_editName = "";
			}
			ImGui.Columns(1);
			ImGui.EndChild();
			ImGui.TextWrapped("QoLBar's Icon Browser (/qolicons) is recommended for finding icon IDs.");
			ImGui.TextWrapped("Please note this 'feature' is in maintenance mode and will not have anything new added, but will be fixed in case of bugs.");
		}
	}
	
	private static bool ShouldShowDamageType()
	{
		if (_configuration.SeDamageIconDisable
		    || !_configuration.OutgoingAttackTextEnabled
		    || !_config.Unlocked
		    || !_config.Enabled
		    || _config.DamageTypes.Count == 0)
			return false;
		var trigger = _r.Next(_config.Frequency);
		return trigger == 0;
	}

	public static void SetRareDamageType(ref uint icon, ref SeString text)
	{
		if (!ShouldShowDamageType()) return;
		if (text.Payloads.Count == 0) return;

		var index = _r.Next(_config.DamageTypes.Count);
		var result = _config.DamageTypes.ElementAt(index);
		icon = (uint)result.Key;
		var str = new SeString(new TextPayload($"{result.Value} "));
		str.Append(text.Payloads);
		text = str;
	}

	private static bool IconExists(int iconId)
	{
		var pathHr = $"ui/icon/{(object)(iconId / 1000U):D3}000/{iconId:D6}_hr1.tex";
		var path = $"ui/icon/{(object)(iconId / 1000U):D3}000/{iconId:D6}.tex";

		return _dataManager.FileExists(pathHr) || _dataManager.FileExists(path);
	}

	public static TexFile GetIcon(int iconId)
	{
		var pathHr = $"ui/icon/{(object)(iconId / 1000U):D3}000/{iconId:D6}_hr1.tex";
		var path = $"ui/icon/{(object)(iconId / 1000U):D3}000/{iconId:D6}.tex";

		return _dataManager.GetFile<TexFile>(_dataManager.FileExists(pathHr) ? pathHr : path);
	}
}