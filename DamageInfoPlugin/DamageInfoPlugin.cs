using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using DamageInfoPlugin.Positionals;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using static DamageInfoPlugin.LogType;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using DObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace DamageInfoPlugin;

// ReSharper disable once ClassNeverInstantiated.Global
public unsafe class DamageInfoPlugin : IDalamudPlugin
{
	private const int TargetInfoGaugeBgNodeIndex = 41;
	private const int TargetInfoGaugeNodeIndex = 43;

	private const int TargetInfoSplitGaugeBgNodeIndex = 2;
	private const int TargetInfoSplitGaugeNodeIndex = 4;

	private const int FocusTargetInfoGaugeBgNodeIndex = 13;
	private const int FocusTargetInfoGaugeNodeIndex = 15;

	public string Name => "Damage Info";

	private const string CommandName = "/dmginfo";

	private readonly Configuration _configuration;
	private readonly PluginUI _ui;

	private delegate void AddScreenLogDelegate(
		Character* target,
		Character* source,
		FlyTextKind logKind,
		int option,
		int actionKind,
		int actionId,
		int val1,
		int val2,
		int val3,
		int val4);

	private delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);
	private delegate void SetCastBarDelegate(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5);

	private readonly Hook<AddScreenLogDelegate> _addScreenLogHook;
	private readonly Hook<ReceiveActionEffectDelegate> _receiveActionEffectHook;
	private readonly Hook<SetCastBarDelegate> _setCastBarHook;
	private readonly Hook<SetCastBarDelegate> _setFocusTargetCastBarHook;

	private readonly CastbarInfo _nullCastbarInfo;
	private Dictionary<uint, DamageType> _actionToDamageTypeDict;
	private Dictionary<uint, string> _actionToNameDict;
	private readonly HashSet<uint> _ignoredCastActions;
	private ActionEffectStore _actionStore;
    private readonly Dictionary<uint, string> _petNicknamesDictionary;

    private PositionalManager _posManager;

	// These are the skills' percentage potency increases sent by the server
	// check research.csv for more info
	private readonly Dictionary<int, HashSet<int>> _positionalData = new()
	{
		{   56, [13] },					// Snap Punch
		{   66, [16] },					// Demolish
		{   88, [28, 61] },             // Chaos Thrust
		{ 2255, [30, 63, 70] },			// Aeolian Edge
		{ 2258, [25] },                 // Trick Attack
		{ 3554, [28, 66] },             // Fang and Claw
		{ 3556, [28, 66] },             // Wheeling Thrust
		{ 3563, [30, 65] },				// Armor Crush
		{ 7481, [29, 33, 68, 72] },     // Gekko (rear)
		{ 7482, [29, 33, 68, 72] },     // Kasha (flank)
		{24382, [11, 13] },             // Gibbet (flank)
		{24383, [11, 13] },             // Gallows (rear)
		{25772, [28, 66] },             // Chaotic Spring
		
		{34610, [52, 54, 66, 70] },				// Flanksting Strike 
		{34611, [52, 54, 66, 70] },				// Flanksbane Fang 
		{34612, [52, 54, 66, 70] },				// Hindsting Strike 
		{34613, [52, 54, 66, 70] },				// Hindsbane Fang 
		
		{34621, [9] },					// Hunter's Coil
		{34622, [9] },					// Swiftskin's Coil
	};
	
	private readonly HashSet<int> _positionalSkills = [
		56, 66, 79, 88, 2255, 2258, 3554, 3556, 3563, 7481, 7482, 24382, 24383,
		25772, 34610, 34611, 34612, 34613, 34621, 34622, 36947, 36970, 36971,
	];

	public DamageInfoPlugin(IDalamudPluginInterface pi)
	{
		DalamudApi.Initialize(pi);
		
		_configuration = LoadConfig();
		_ui = new PluginUI(_configuration, this);
		_actionToDamageTypeDict = new Dictionary<uint, DamageType>();
        _petNicknamesDictionary = DalamudApi.PluginInterface.GetOrCreateData("PetRenamer.GameObjectRenameDict", () => new Dictionary<uint, string>());
		_actionToNameDict = new Dictionary<uint, string>();
		_ignoredCastActions = new HashSet<uint>();
        _actionStore = new ActionEffectStore(_configuration);
		_nullCastbarInfo = new CastbarInfo
		{
			unitBase = null,
			gauge = null,
			bg = null,
		};
		_posManager = new PositionalManager();

		DalamudApi.CommandManager.AddHandler("/dmginfo", new CommandInfo(OnCommand)
		{
			HelpMessage = "Display the Damage Info configuration interface.",
		});

		try
		{
			var actionSheet = DalamudApi.DataManager.GetExcelSheet<Action>();

			if (actionSheet == null)
				throw new NullReferenceException();

			foreach (var row in actionSheet)
			{
				var dmgType = ((AttackType)row.AttackType.Row).ToDamageType();
				var name = row.Name;
				
				_actionToDamageTypeDict.Add(row.RowId, dmgType);
				_actionToNameDict.Add(row.RowId, name.ToString());

				if (row.ActionCategory.Row is > 4 and < 11)
					_ignoredCastActions.Add(row.ActionCategory.Row);
			}

			var receiveActionEffectFuncPtr = DalamudApi.SigScanner.ScanText("40 55 56 57 41 54 41 55 41 56 48 8D AC 24");
			_receiveActionEffectHook = DalamudApi.Hooks.HookFromAddress<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr, ReceiveActionEffect);

			var addScreenLogPtr = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? BF ?? ?? ?? ?? EB 39");
			_addScreenLogHook = DalamudApi.Hooks.HookFromAddress<AddScreenLogDelegate>(addScreenLogPtr, AddScreenLogDetour);

			var setCastBarFuncPtr = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 4C 8D 8F ?? ?? ?? ?? 4D 8B C6");
			_setCastBarHook = DalamudApi.Hooks.HookFromAddress<SetCastBarDelegate>(setCastBarFuncPtr, SetCastBarDetour);

			var setFocusTargetCastBarFuncPtr = DalamudApi.SigScanner.ScanText("40 56 41 54 41 55 41 57 48 83 EC 78");
			_setFocusTargetCastBarHook = DalamudApi.Hooks.HookFromAddress<SetCastBarDelegate>(setFocusTargetCastBarFuncPtr, SetFocusTargetCastBarDetour);

			DalamudApi.FlyTextGui.FlyTextCreated += OnFlyTextCreated;
		}
		catch (Exception ex)
		{
			DalamudApi.PluginLog.Error(ex, $"An error occurred loading DamageInfoPlugin.");
			DalamudApi.PluginLog.Error("Plugin will not be loaded.");

			_addScreenLogHook?.Disable();
			_addScreenLogHook?.Dispose();
			_receiveActionEffectHook?.Disable();
			_receiveActionEffectHook?.Dispose();
			_setCastBarHook?.Disable();
			_setCastBarHook?.Dispose();
			_setFocusTargetCastBarHook?.Disable();
			_setFocusTargetCastBarHook?.Dispose();
			DalamudApi.CommandManager.RemoveHandler(CommandName);

			throw;
		}

		_receiveActionEffectHook.Enable();
		_addScreenLogHook.Enable();
		_setCastBarHook.Enable();
		_setFocusTargetCastBarHook.Enable();

		DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
		DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

		Fools2023.Initialize(_configuration);
	}

	private Configuration LoadConfig()
	{
		var config = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
		if (config.Version < 2)
		{
			config = new Configuration();
		}
		else if (config.Version == 2)
		{
			config.Version = 3;

			config.PositionalMissColor = config.PositionalColor;
			config.PositionalHitColor = config.PositionalColor;
			
			if (config.PositionalColorInvert)
			{
				config.PositionalMissColorEnabled = true;
				config.PositionalHitColorEnabled = false;
			}
			else
			{
				config.PositionalMissColorEnabled = false;
				config.PositionalHitColorEnabled = true;
			}
		}

		config.Initialize(this);
		config.Save();
		return config;
	}

	public void Dispose()
	{
		_actionStore.Dispose();
		ResetMainTargetCastBar();
		ResetFocusTargetCastBar();
		_receiveActionEffectHook?.Disable();
		_receiveActionEffectHook?.Dispose();
		_addScreenLogHook?.Disable();
		_addScreenLogHook?.Dispose();
		_setCastBarHook?.Disable();
		_setCastBarHook?.Dispose();
		_setFocusTargetCastBarHook?.Disable();
		_setFocusTargetCastBarHook?.Dispose();

		DalamudApi.FlyTextGui.FlyTextCreated -= OnFlyTextCreated;

		_actionStore = null;
		_actionToDamageTypeDict = null;

		_ui.Dispose();
		Fools2023.Dispose();
		DalamudApi.CommandManager.RemoveHandler(CommandName);
		DalamudApi.PluginInterface.RelinquishData("PetRenamer.GameObjectRenameDict");
	}
		
	private void OnCommand(string command, string args)
	{
		_ui.SettingsVisible = true;

		if (args == "fools2023" && !_configuration.Fools2023Config.Unlocked)
		{
			Fools2023.Unlock();
			var seStr = new SeStringBuilder()
				.AddUiForeground("[DamageInfoPlugin]", 506)
				.Add(new TextPayload(" New rare damage types"))
				.AddUiForeground(" UNLOCKED! ", 504)
				.Add(new TextPayload("You can type /dmginfo to open the settings and disable them if you prefer. Note that damage icons must be enabled in Damage Info to see them."))
				.Build();
			DalamudApi.ChatGui.Print(new XivChatEntry() { Message = seStr });
		}

		if (args == "posload")
		{
			_posManager.Reset();
		}
	}

	private void DrawUI()
	{
		_ui.Draw();
	}

	private void DrawConfigUI()
	{
		_ui.SettingsVisible = true;
	}

#region castbar
	private CastbarInfo GetTargetInfoUiElements()
	{
		var unitBase = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_TargetInfo").ToPointer();

		if (unitBase == null) return _nullCastbarInfo;

		return new CastbarInfo
		{
			unitBase = unitBase,
			gauge = (AtkImageNode*)unitBase->UldManager.NodeList[TargetInfoGaugeNodeIndex],
			bg = (AtkImageNode*)unitBase->UldManager.NodeList[TargetInfoGaugeBgNodeIndex]
		};
	}

	private CastbarInfo GetTargetInfoSplitUiElements()
	{
		var unitBase = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_TargetInfoCastBar").ToPointer();

		if (unitBase == null) return _nullCastbarInfo;

		return new CastbarInfo
		{
			unitBase = unitBase,
			gauge = (AtkImageNode*)unitBase->UldManager.NodeList[TargetInfoSplitGaugeNodeIndex],
			bg = (AtkImageNode*)unitBase->UldManager.NodeList[TargetInfoSplitGaugeBgNodeIndex]
		};
	}

	private CastbarInfo GetFocusTargetUiElements()
	{
		var unitBase = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_FocusTargetInfo").ToPointer();

		if (unitBase == null) return _nullCastbarInfo;

		return new CastbarInfo
		{
			unitBase = unitBase,
			gauge = (AtkImageNode*)unitBase->UldManager.NodeList[FocusTargetInfoGaugeNodeIndex],
			bg = (AtkImageNode*)unitBase->UldManager.NodeList[FocusTargetInfoGaugeBgNodeIndex]
		};
	}

	public void ResetMainTargetCastBar()
	{
		GetTargetInfoUiElements().ResetIfValid();
		GetTargetInfoSplitUiElements().ResetIfValid();
	}

	public void ResetFocusTargetCastBar()
	{
		GetFocusTargetUiElements().ResetIfValid();
	}

	private void SetCastBarDetour(nint thisPtr, nint a2, nint a3, nint a4, char a5)
	{
		if (!_configuration.MainTargetCastBarColorEnabled)
		{
			_setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
			return;
		}

		var targetInfo = GetTargetInfoUiElements();
		var splitInfo = GetTargetInfoSplitUiElements();

		if (!targetInfo.Valid() && !splitInfo.Valid())
		{
			_setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
			return;
		}

		var toColor = _nullCastbarInfo;
		if (thisPtr.ToPointer() == targetInfo.unitBase)
			toColor = targetInfo;
		else if (thisPtr.ToPointer() == splitInfo.unitBase)
			toColor = splitInfo;

		if (toColor != _nullCastbarInfo)
			ColorCastBar(DalamudApi.TargetManager.Target, toColor, _setCastBarHook, thisPtr, a2, a3, a4, a5);
	}

	private void SetFocusTargetCastBarDetour(nint thisPtr, nint a2, nint a3, nint a4, char a5)
	{
		if (!_configuration.FocusTargetCastBarColorEnabled)
		{
			_setFocusTargetCastBarHook.Original(thisPtr, a2, a3, a4, a5);
			return;
		}

		var ftInfo = GetFocusTargetUiElements();

		if (thisPtr.ToPointer() != ftInfo.unitBase || !ftInfo.Valid()) return;

		ColorCastBar(DalamudApi.TargetManager.FocusTarget, ftInfo, _setFocusTargetCastBarHook, thisPtr, a2, a3, a4, a5);
	}

	private void ColorCastBar(IGameObject target, CastbarInfo info, Hook<SetCastBarDelegate> hook, nint thisPtr, nint a2, nint a3, nint a4, char a5)
	{
		if (target == null || target is not IBattleChara battleTarget)
		{
			hook.Original(thisPtr, a2, a3, a4, a5);
			return;
		}

		var actionId = battleTarget.CastActionId;
		_actionToDamageTypeDict.TryGetValue(actionId, out var type);
		// DebugLog(Castbar, $"casting {actionId} {type}");
		if (_ignoredCastActions.Contains(actionId))
		{
			info.Reset();
			hook.Original(thisPtr, a2, a3, a4, a5);
			return;
		}

		var castColor = type switch
		{
			DamageType.Physical => _configuration.PhysicalCastColor,
			DamageType.Magical => _configuration.MagicCastColor,
			DamageType.Unique => _configuration.DarknessCastColor,
			_ => Vector4.One,
		};

		var bgColor = type switch
		{
			DamageType.Physical => _configuration.PhysicalBgColor,
			DamageType.Magical => _configuration.MagicBgColor,
			DamageType.Unique => _configuration.DarknessBgColor,
			_ => Vector4.One,
		};

		info.Color(castColor, bgColor);
		hook.Original(thisPtr, a2, a3, a4, a5);
	}
#endregion

	private List<uint> FindCharaPets()
	{
		var results = new List<uint>();
		var charaId = GetCharacterActorId();
		foreach (var obj in DalamudApi.ObjectTable)
		{
			if (obj is not IBattleNpc npc) continue;

			var actPtr = npc.Address;
			if (actPtr == IntPtr.Zero) continue;

			if (npc.OwnerId == charaId)
				results.Add(npc.EntityId);
		}

		return results;
	}

	private uint GetCharacterActorId()
	{
		return DalamudApi.ClientState.LocalPlayer?.EntityId ?? 0;
	}

	private SeString GetActorName(uint id)
	{
		var dGameObject = DalamudApi.ObjectTable.SearchById(id);
		if (dGameObject == null) return SeString.Empty;
		if (dGameObject.ObjectKind == DObjectKind.BattleNpc && _petNicknamesDictionary.TryGetValue(id, out var name)) return name;
		return dGameObject.Name;
    }

	private void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
	{
		try
		{
			_actionStore.Cleanup();

			DebugLog(Effect, $"--- source actor: {sourceCharacter->GameObject.EntityId}, action id {effectHeader->ActionId}, anim id {effectHeader->AnimationId} numTargets: {effectHeader->TargetCount} ---");

			// TODO: Reimplement opcode logging, if it's even useful. Original code follows
			// ushort op = *((ushort*) effectHeader.ToPointer() - 0x7);
			// DebugLog(Effect, $"--- source actor: {sourceId}, action id {id}, anim id {animId}, opcode: {op:X} numTargets: {targetCount} ---");

			var entryCount = effectHeader->TargetCount switch
			{
				0 => 0,
				1 => 8,
				<= 8 => 64,
				<= 16 => 128,
				<= 24 => 192,
				<= 32 => 256,
				_ => 0
			};

			// Check if we have data for this action ID.
			// Then we can check if the p2 is in the expected value set for positional success.
			var positionalState = PositionalState.Ignore;
			var isPositional = _positionalSkills.Contains(effectHeader->AnimationId);
			if (isPositional)
			{
				positionalState = PositionalState.Failure;
				for (int i = 0; i < entryCount; i++)
					if (effectArray[i].type == ActionEffectType.Damage)
						if (_posManager.IsPositionalHit(effectHeader->AnimationId, effectArray[i].param2))
							positionalState = PositionalState.Success;
			}
			
			if (isPositional)
			{
				for (int i = 0; i < entryCount; i++)
				{
					if (effectArray[i].type == ActionEffectType.Damage && sourceId == GetCharacterActorId())
					{
						var id = effectHeader->AnimationId;
						var name = id.ToString();
						if (_actionToNameDict.TryGetValue(id, out var sheetName))
							name = $"{sheetName} [{name}]";
						PositionalLog($"Action: {name} jobLevel: {GetCurrentLevel()} boostPercent: {effectArray[i].param2} positionalState: {positionalState}");
					}
				}
			}
			
			for (int i = 0; i < entryCount; i++)
			{
				if (effectArray[i].type == ActionEffectType.Nothing) continue;

				var target = effectTail[i / 8];
				uint dmg = effectArray[i].value;
				if (effectArray[i].mult != 0)
					dmg += ((uint)ushort.MaxValue + 1) * effectArray[i].mult;

				var dmgType = ((AttackType)effectArray[i].AttackType).ToDamageType();
				if (effectArray[i].type == ActionEffectType.Heal) dmgType = DamageType.None;
				DebugLog(Effect, $"{effectArray[i]}, s: {sourceId} t: {target} dmgType {dmgType}");

				var newEffect = new ActionEffectInfo
				{
					step = ActionStep.Effect,
					actionId = effectHeader->ActionId,
					type = effectArray[i].type,
					damageType = dmgType,
					// we fill in LogKind later 
					sourceId = sourceId,
					targetId = target,
					value = dmg,
					positionalState = positionalState
				};

				_actionStore.AddEffect(newEffect);
			}
		}
		catch (Exception e)
		{
			DalamudApi.PluginLog.Error(e, "An error has occurred in Damage Info.");
		}

		_receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
	}

	private int GetCurrentLevel()
	{
		return DalamudApi.ClientState.LocalPlayer?.Level ?? -1;
	}

	private void AddScreenLogDetour(
		Character* target,
		Character* source,
		FlyTextKind logKind,
		int option,
		int actionKind,
		int actionId,
		int val1,
		int val2,
		int serverAttackType,
		int val4)
	{
		try
		{
			var targetId = target->GameObject.EntityId;
			var sourceId = source->GameObject.EntityId;

			if (_configuration.DebugLogEnabled)
			{
				DebugLog(ScreenLog, $"{option} {actionKind} {actionId}");
				DebugLog(ScreenLog, $"{val1} {val2} {serverAttackType} {val4}");
				var targetName = GetActorName(targetId);
				var sourceName = GetActorName(sourceId);
				DebugLog(ScreenLog, $"src {sourceId} {sourceName}");
				DebugLog(ScreenLog, $"tgt {targetId} {targetName}");
			}

			_actionStore.UpdateEffect((uint)actionId, sourceId, targetId, (uint)val1, (uint)serverAttackType, logKind);
		}
		catch (Exception e)
		{
			DalamudApi.PluginLog.Error(e, "An error occurred in Damage Info.");
		}

		_addScreenLogHook.Original(target, source, logKind, option, actionKind, actionId, val1, val2, serverAttackType, val4);
	}

	private void OnFlyTextCreated(
		ref FlyTextKind kind,
		ref int val1,
		ref int val2,
		ref SeString text1,
		ref SeString text2,
		ref uint color,
		ref uint icon,
		ref uint damageTypeIcon,
		ref float yOffset,
		ref bool handled)
	{
		try
		{
			var ftKind = kind;

			if (_configuration.DebugLogEnabled)
			{
				var str1 = text1?.TextValue.Replace("%", "%%");
				var str2 = text2?.TextValue.Replace("%", "%%");

				DebugLog(FlyText, $"flytext created: kind: {ftKind} ({(int)kind}), val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}");
				DebugLog(FlyText, $"text1: {str1} | text2: {str2}");
				DalamudApi.ChatGui.Print(new XivChatEntry { Message = $""});
			}

			var charaId = GetCharacterActorId();
			var petIds = FindCharaPets();

			var damageType = ((SeDamageType)damageTypeIcon).ToDamageType();
			if (!_actionStore.TryGetEffect((uint)val1, damageType, ftKind, charaId, petIds, out var info))
			{
				DebugLog(FlyText, $"Failed to obtain info... {val1} {damageType} {ftKind} {charaId}");
				return;
			}

			DebugLog(FlyText, $"Obtained info: {info}");
			
			SeCheck(info, (SeDamageType)damageTypeIcon, damageType);
			
			// I'd like to color dodges, so let's fallback in the case that we have a dodge - SE doesn't send info on these
			if (info is { value: 0, kind: FlyTextKind.Miss or FlyTextKind.NamedMiss } && _actionToDamageTypeDict.TryGetValue(info.actionId, out damageType))
			{
				DebugLog(FlyText, $"Processed fallback actionId {info.actionId} to {damageType} added icon {damageTypeIcon}");
			}

			var isHealingAction = info.type == ActionEffectType.Heal;
			var isPetAction = petIds.Contains(info.sourceId);
			var isCharaAction = info.sourceId == charaId;
			var isCharaTarget = info.targetId == charaId;

			if ((_configuration.IncomingColorEnabled || _configuration.OutgoingColorEnabled || _configuration.PositionalHitColorEnabled || _configuration.PositionalMissColorEnabled))
			{
				var incomingCheck = !isCharaAction && isCharaTarget && !isHealingAction && _configuration.IncomingColorEnabled;
				var outgoingCheck = isCharaAction && !isCharaTarget && !isHealingAction && _configuration.OutgoingColorEnabled;
				var petCheck = !isCharaAction && !isCharaTarget && petIds.Contains(info.sourceId) && !isHealingAction && _configuration.PetColorEnabled;

				// Large check - check that it's a character action, we shouldn't ignore the state, and that positionals are enabled
				// then, check to see if we should color the success or the failure
				var posCheck = isCharaAction && info.positionalState != PositionalState.Ignore;

				if (incomingCheck || outgoingCheck || petCheck)
					color = GetDamageColor(damageType);

				if (posCheck)
				{
					color = info.positionalState switch
					{
						PositionalState.Success when _configuration.PositionalHitColorEnabled => ImGui.GetColorU32(_configuration.PositionalHitColor),
						PositionalState.Failure when _configuration.PositionalMissColorEnabled => ImGui.GetColorU32(_configuration.PositionalMissColor),
						_ => color,
					};
				}
			}

			if (_configuration.SourceTextEnabled || _configuration.PetSourceTextEnabled || _configuration.HealSourceTextEnabled)
			{
				var tgtCheck = !isCharaAction && !isHealingAction && !isPetAction && _configuration.SourceTextEnabled;
				var petCheck = isPetAction && _configuration.PetSourceTextEnabled;
				var healCheck = isHealingAction && _configuration.HealSourceTextEnabled;

				if (tgtCheck || petCheck || healCheck)
				{
					text2 = GetNewText(info.sourceId, text2);
				}
			}

			if (_configuration.SeDamageIconDisable)
				damageTypeIcon = 0;

			if (info.type == ActionEffectType.Damage)
				Fools2023.SetRareDamageType(ref damageTypeIcon, ref text1);
			
			// Attack text checks
			if (!_configuration.IncomingAttackTextEnabled
			    || !_configuration.OutgoingAttackTextEnabled
			    || !_configuration.PetAttackTextEnabled
			    || !_configuration.HealAttackTextEnabled
			    || _configuration.AnyPositionalTextEnabled())
			{
				var incomingCheck = !isCharaAction && isCharaTarget && !isHealingAction && !isPetAction && !_configuration.IncomingAttackTextEnabled;
				var outgoingCheck = isCharaAction && !isCharaTarget && !isHealingAction && !isPetAction && !_configuration.OutgoingAttackTextEnabled;
				var petCheck = !isCharaAction && !isCharaTarget && !isHealingAction && isPetAction && !_configuration.PetAttackTextEnabled;
				var healCheck = isHealingAction && !isPetAction && !_configuration.HealAttackTextEnabled;

				var hitCheck = _configuration.PositionalHitTextSettings.AnyEnabled() && info.positionalState == PositionalState.Success;
				var missCheck = _configuration.PositionalMissTextSettings.AnyEnabled() && info.positionalState == PositionalState.Failure;
				var posAnyCheck = hitCheck || missCheck;

				var posOverride = (_configuration.PositionalAttackTextOverrideEnabled && !_configuration.OutgoingAttackTextEnabled)
				                  || _configuration.OutgoingAttackTextEnabled;
				var posCheck = posAnyCheck && posOverride && info.positionalState != PositionalState.Ignore;

				if (incomingCheck || petCheck || healCheck || (outgoingCheck && !posCheck))
					text1 = "";

				if (posCheck)
				{
					var payloads = new List<Payload>();
					if (hitCheck && _configuration.PositionalHitTextSettings.IsPrefixEnabled())
						payloads.Add(_configuration.PositionalHitTextSettings.PrefixPayload());
					if (missCheck && _configuration.PositionalMissTextSettings.IsPrefixEnabled())
						payloads.Add(_configuration.PositionalMissTextSettings.PrefixPayload());
					payloads.AddRange(text1.Payloads);
					if (hitCheck && _configuration.PositionalHitTextSettings.IsSuffixEnabled())
						payloads.Add(_configuration.PositionalHitTextSettings.SuffixPayload());
					if (missCheck && _configuration.PositionalMissTextSettings.IsSuffixEnabled())
						payloads.Add(_configuration.PositionalMissTextSettings.SuffixPayload());
					text1.Payloads.Clear();
					text1.Payloads.AddRange(payloads);
				}
			}

			if (_configuration.AnyPositionalSoundEnabled())
			{
				var hitSettings = _configuration.PositionalHitSoundSettings;
				var missSettings = _configuration.PositionalMissSoundSettings;
				if (info.positionalState == PositionalState.Success && hitSettings.Enabled)
					PlaySE(hitSettings.SoundId);
				if (info.positionalState == PositionalState.Failure && missSettings.Enabled)
					PlaySE(missSettings.SoundId);
			}
		}
		catch (Exception e)
		{
			DalamudApi.PluginLog.Error(e, "An error has occurred in Damage Info");
		}
	}

	private void SeCheck(ActionEffectInfo info, SeDamageType seDamageType, DamageType dmgType)
	{
		if ((seDamageType == SeDamageType.Physical && dmgType != DamageType.Physical) ||
		    (seDamageType == SeDamageType.Magical && dmgType != DamageType.Magical) ||
		    (seDamageType == SeDamageType.Unique && dmgType != DamageType.Unique))
		{
			var warning = $"Encountered a damage type mismatch on {info.actionId}: SE says {seDamageType}, damage info says {dmgType}";
			DalamudApi.PluginLog.Information(warning);
				
#if DEBUG
			var seStr = new SeStringBuilder()
				.AddUiForeground("[DamageInfoPlugin]", 506)
				.Add(new TextPayload($" {warning}."))
				.AddUiForeground(" Please report this in the Damage Info thread in the Goat Place discord!", 60)
				.Build();
			DalamudApi.ChatGui.Print(new XivChatEntry() { Message = seStr });
#endif
		}
	}
	
	private void PositionalLog(string message)
	{
		if (!_configuration.PositionalLogEnabled) return;
		var seStr = new SeStringBuilder()
			.AddUiForeground("[DamageInfoPlugin]", 506)
			.Add(new TextPayload($" {message}."))
			.Build();
		DalamudApi.ChatGui.Print(new XivChatEntry { Message = seStr });
	}

	private void PlaySE(int soundId)
	{
		try
		{
			UIModule.PlayChatSoundEffect((uint)soundId);
		}
		catch (ArgumentException e)
		{
			DebugLog(Sound, $"Failed to play sound {soundId}: {e.Message}");
		}
	}

	private SeString GetNewText(uint sourceId, SeString originalText)
	{
		SeString name = GetActorName(sourceId);
		var newPayloads = new List<Payload>();

		if (name.Payloads.Count == 0) return originalText;

		switch (DalamudApi.ClientState.ClientLanguage)
		{
			case ClientLanguage.Japanese:
				newPayloads.AddRange(name.Payloads);
				newPayloads.Add(new TextPayload("から"));
				break;
			case ClientLanguage.English:
				newPayloads.Add(new TextPayload("from "));
				newPayloads.AddRange(name.Payloads);
				break;
			case ClientLanguage.German:
				newPayloads.Add(new TextPayload("von "));
				newPayloads.AddRange(name.Payloads);
				break;
			case ClientLanguage.French:
				newPayloads.Add(new TextPayload("de "));
				newPayloads.AddRange(name.Payloads);
				break;
			default:
				newPayloads.Add(new TextPayload(">"));
				newPayloads.AddRange(name.Payloads);
				break;
		}

		if (originalText.Payloads.Count > 0)
			newPayloads.AddRange(originalText.Payloads);

		return new SeString(newPayloads);
	}

	private void DebugLog(LogType type, string str)
	{
		if (_configuration.DebugLogEnabled)
			DalamudApi.PluginLog.Information($"[{type}] {str}");
	}

	private uint GetDamageColor(DamageType type, uint fallback = 0xFF00008A)
	{
		return type switch
		{
			DamageType.Physical => ImGui.GetColorU32(_configuration.PhysicalColor),
			DamageType.Magical => ImGui.GetColorU32(_configuration.MagicColor),
			DamageType.Unique => ImGui.GetColorU32(_configuration.DarknessColor),
			_ => fallback
		};
	}
}