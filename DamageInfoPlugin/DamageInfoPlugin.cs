using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DamageInfoPlugin
{
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

        private readonly GameGui _gameGui;
        private readonly DalamudPluginInterface _pi;
        private readonly CommandManager _cmdMgr;
        private readonly FlyTextGui _ftGui;
        private readonly ObjectTable _objectTable;
        private readonly ClientState _clientState;
        private readonly TargetManager _targetManager;
        
        private delegate void AddScreenLogDelegate(
            FFXIVClientStructs.FFXIV.Client.Game.Character.Character* target,
            FFXIVClientStructs.FFXIV.Client.Game.Character.Character* source,
            FlyTextKind logKind,
            int option,
            int actionKind,
            int actionId,
            int val1,
            int val2,
            int val3,
            int val4);
        private readonly Hook<AddScreenLogDelegate> _addScreenLogHook;

        private delegate void SetCastBarDelegate(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5);
        private readonly Hook<SetCastBarDelegate> _setCastBarHook;
        private readonly Hook<SetCastBarDelegate> _setFocusTargetCastBarHook;

        private delegate IntPtr WriteFlyTextDataDelegate(IntPtr a1, NumberArrayData* numberArray, uint numberArrayIndex, IntPtr a4, int a5, int* ftData, uint a7, uint a8);
        private readonly Hook<WriteFlyTextDataDelegate> _writeFlyTextHook;

        private Dictionary<uint, DamageType> _actionToDamageTypeDict;
        private readonly CastbarInfo _nullCastbarInfo;
        private readonly HashSet<uint> _ignoredCastActions;
        private readonly List<ScreenLogInfo> _actions;

        public DamageInfoPlugin(
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] FlyTextGui ftGui,
            [RequiredVersion("1.0")] DalamudPluginInterface pi,
            [RequiredVersion("1.0")] CommandManager cmdMgr,
            [RequiredVersion("1.0")] DataManager dataMgr,
            [RequiredVersion("1.0")] ObjectTable objectTable,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] TargetManager targetManager,
            [RequiredVersion("1.0")] SigScanner scanner)
        {
            _gameGui = gameGui;
            _ftGui = ftGui;
            _pi = pi;
            _cmdMgr = cmdMgr;
            _objectTable = objectTable;
            _clientState = clientState;
            _targetManager = targetManager;

            _actionToDamageTypeDict = new Dictionary<uint, DamageType>();
            _actions = new List<ScreenLogInfo>();
            _ignoredCastActions = new HashSet<uint>();

            _configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _configuration.Initialize(pi, this);
            _ui = new PluginUI(_configuration, this);

            cmdMgr.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Display the Damage Info configuration interface."
            });

            _nullCastbarInfo = new CastbarInfo { unitBase = null, gauge = null, bg = null };

            try
            {
                var actionSheet = dataMgr.GetExcelSheet<Action>();
                foreach (var row in actionSheet)
                {
                    var tmpType = (DamageType) row.AttackType.Row;
                    if (tmpType != DamageType.Magic
                        && tmpType != DamageType.Darkness 
                        && tmpType != DamageType.Unknown)
                        tmpType = DamageType.Physical;

                    _actionToDamageTypeDict.Add(row.RowId, tmpType);

                    if (row.ActionCategory.Row is > 4 and < 11)
                        _ignoredCastActions.Add(row.ActionCategory.Row);
                }
                
                var writeFtPtr = scanner.ScanText("E8 ?? ?? ?? ?? 83 F8 01 75 45");
                _writeFlyTextHook = new Hook<WriteFlyTextDataDelegate>(writeFtPtr, (WriteFlyTextDataDelegate) WriteFlyTextDataDetour);

                var addScreenLogPtr = scanner.ScanText("E8 ?? ?? ?? ?? BB ?? ?? ?? ?? EB 37");
                _addScreenLogHook = new Hook<AddScreenLogDelegate>(addScreenLogPtr, (AddScreenLogDelegate) AddScreenLogDetour);

                var setCastBarFuncPtr = scanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 80 7C 24");
                _setCastBarHook = new Hook<SetCastBarDelegate>(setCastBarFuncPtr, (SetCastBarDelegate) SetCastBarDetour);

                var setFocusTargetCastBarFuncPtr = scanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 41 0F B6 F9 49 8B E8 48 8B F2 48 8B D9");
                _setFocusTargetCastBarHook = new Hook<SetCastBarDelegate>(setFocusTargetCastBarFuncPtr, (SetCastBarDelegate) SetFocusTargetCastBarDetour);
                
                ftGui.FlyTextCreated += OnFlyTextCreated;
            }
            catch (Exception ex)
            {
                PluginLog.Information($"Encountered an error loading DamageInfoPlugin: {ex.Message}");
                PluginLog.Information("Plugin will not be loaded.");
                
                _writeFlyTextHook?.Disable();
                _writeFlyTextHook?.Dispose();
                _addScreenLogHook?.Disable();
                _addScreenLogHook?.Dispose();
                _setCastBarHook?.Disable();
                _setCastBarHook?.Dispose();
                _setFocusTargetCastBarHook?.Disable();
                _setFocusTargetCastBarHook?.Dispose();
                cmdMgr.RemoveHandler(CommandName);

                throw;
            }

            _writeFlyTextHook.Enable();
            _addScreenLogHook.Enable();
            _setCastBarHook.Enable();
            _setFocusTargetCastBarHook.Enable();

            pi.UiBuilder.Draw += DrawUI;
            pi.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            ResetMainTargetCastBar();
            ResetFocusTargetCastBar();
            _writeFlyTextHook?.Disable();
            _writeFlyTextHook?.Dispose();
            _addScreenLogHook?.Disable();
            _addScreenLogHook?.Dispose();
            _setCastBarHook?.Disable();
            _setCastBarHook?.Dispose();
            _setFocusTargetCastBarHook?.Disable();
            _setFocusTargetCastBarHook?.Dispose();

            _ftGui.FlyTextCreated -= OnFlyTextCreated;

            _actionToDamageTypeDict.Clear();
            _actionToDamageTypeDict = null;

            _ui.Dispose();
            _cmdMgr.RemoveHandler(CommandName);
            _pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            _ui.SettingsVisible = true;
        }

        private void DrawUI()
        {
            _ui.Draw();
        }

        private void DrawConfigUI()
        {
            _ui.SettingsVisible = true;
        }

        private uint FindCharaPet()
        {
            var charaId = GetCharacterActorId();
            foreach (var obj in _objectTable)
            {
                if (obj is not BattleNpc npc) continue;

                IntPtr actPtr = npc.Address;
                if (actPtr == IntPtr.Zero) continue;

                if (npc.OwnerId == charaId)
                    return npc.ObjectId;
            }

            return uint.MaxValue;
        }

        private uint GetCharacterActorId()
        {
            return _clientState?.LocalPlayer?.ObjectId ?? 0;
        }

        private SeString GetActorName(uint id)
        {
            foreach (var obj in _objectTable)
                if (obj != null)
                    if (id == obj.ObjectId)
                        return obj.Name;
            return "";
        }

        private CastbarInfo GetTargetInfoUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*)_gameGui.GetAddonByName("_TargetInfo", 1).ToPointer();

            if (unitbase == null) return _nullCastbarInfo;

            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*)unitbase->UldManager.NodeList[TargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*)unitbase->UldManager.NodeList[TargetInfoGaugeBgNodeIndex]
            };
        }

        private CastbarInfo GetTargetInfoSplitUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*)_gameGui.GetAddonByName("_TargetInfoCastBar", 1).ToPointer();

            if (unitbase == null) return _nullCastbarInfo;

            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*)unitbase->UldManager.NodeList[TargetInfoSplitGaugeNodeIndex],
                bg = (AtkImageNode*)unitbase->UldManager.NodeList[TargetInfoSplitGaugeBgNodeIndex]
            };
        }

        private CastbarInfo GetFocusTargetUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*)_gameGui.GetAddonByName("_FocusTargetInfo", 1).ToPointer();

            if (unitbase == null) return _nullCastbarInfo;

            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*)unitbase->UldManager.NodeList[FocusTargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*)unitbase->UldManager.NodeList[FocusTargetInfoGaugeBgNodeIndex]
            };
        }

        public void ResetMainTargetCastBar()
        {
            var targetInfo = GetTargetInfoUiElements();
            var splitInfo = GetTargetInfoSplitUiElements();

            if (targetInfo.unitBase != null && targetInfo.gauge != null && targetInfo.bg != null)
            {
                targetInfo.gauge->AtkResNode.Color.R = 0xFF;
                targetInfo.gauge->AtkResNode.Color.G = 0xFF;
                targetInfo.gauge->AtkResNode.Color.B = 0xFF;
                targetInfo.gauge->AtkResNode.Color.A = 0xFF;

                targetInfo.bg->AtkResNode.Color.R = 0xFF;
                targetInfo.bg->AtkResNode.Color.G = 0xFF;
                targetInfo.bg->AtkResNode.Color.B = 0xFF;
                targetInfo.bg->AtkResNode.Color.A = 0xFF;
            }

            if (splitInfo.unitBase != null && splitInfo.gauge != null && splitInfo.bg != null)
            {
                splitInfo.gauge->AtkResNode.Color.R = 0xFF;
                splitInfo.gauge->AtkResNode.Color.G = 0xFF;
                splitInfo.gauge->AtkResNode.Color.B = 0xFF;
                splitInfo.gauge->AtkResNode.Color.A = 0xFF;

                splitInfo.bg->AtkResNode.Color.R = 0xFF;
                splitInfo.bg->AtkResNode.Color.G = 0xFF;
                splitInfo.bg->AtkResNode.Color.B = 0xFF;
                splitInfo.bg->AtkResNode.Color.A = 0xFF;
            }
        }

        public void ResetFocusTargetCastBar()
        {
            var ftInfo = GetFocusTargetUiElements();

            if (ftInfo.unitBase != null && ftInfo.gauge != null && ftInfo.bg != null)
            {
                ftInfo.gauge->AtkResNode.Color.R = 0xFF;
                ftInfo.gauge->AtkResNode.Color.G = 0xFF;
                ftInfo.gauge->AtkResNode.Color.B = 0xFF;
                ftInfo.gauge->AtkResNode.Color.A = 0xFF;

                ftInfo.bg->AtkResNode.Color.R = 0xFF;
                ftInfo.bg->AtkResNode.Color.G = 0xFF;
                ftInfo.bg->AtkResNode.Color.B = 0xFF;
                ftInfo.bg->AtkResNode.Color.A = 0xFF;
            }
        }

        private void SetCastBarDetour(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (!_configuration.MainTargetCastBarColorEnabled)
            {
                _setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            var targetInfo = GetTargetInfoUiElements();
            var splitInfo = GetTargetInfoSplitUiElements();

            bool combinedInvalid = targetInfo.unitBase == null || targetInfo.gauge == null || targetInfo.bg == null;
            bool splitInvalid = splitInfo.unitBase == null || splitInfo.gauge == null || splitInfo.bg == null;

            if (combinedInvalid && splitInvalid)
            {
                _setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            if (thisPtr.ToPointer() == targetInfo.unitBase && !combinedInvalid)
            {
                var mainTarget = _targetManager.Target;
                ColorCastBar(mainTarget, targetInfo, _setCastBarHook, thisPtr, a2, a3, a4, a5);
            }
            else if (thisPtr.ToPointer() == splitInfo.unitBase && !splitInvalid)
            {
                var mainTarget = _targetManager.Target;
                ColorCastBar(mainTarget, splitInfo, _setCastBarHook, thisPtr, a2, a3, a4, a5);
            }
        }

        private void SetFocusTargetCastBarDetour(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (!_configuration.FocusTargetCastBarColorEnabled)
            {
                _setFocusTargetCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            var ftInfo = GetFocusTargetUiElements();

            bool focusTargetInvalid = ftInfo.unitBase == null || ftInfo.gauge == null || ftInfo.bg == null;

            if (thisPtr.ToPointer() == ftInfo.unitBase && !focusTargetInvalid)
            {
                GameObject focusTarget = _targetManager.FocusTarget;
                ColorCastBar(focusTarget, ftInfo, _setFocusTargetCastBarHook, thisPtr, a2, a3, a4, a5);
            }
        }

        private void ColorCastBar(GameObject target, CastbarInfo info, Hook<SetCastBarDelegate> hook,
            IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (target == null || target is not BattleNpc battleTarget)
            {
                hook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            var actionId = battleTarget.CastActionId;

            _actionToDamageTypeDict.TryGetValue(actionId, out DamageType type);
            if (_ignoredCastActions.Contains(actionId))
            {
                info.gauge->AtkResNode.Color.R = 0xFF;
                info.gauge->AtkResNode.Color.G = 0xFF;
                info.gauge->AtkResNode.Color.B = 0xFF;
                info.gauge->AtkResNode.Color.A = 0xFF;

                info.bg->AtkResNode.Color.R = 0xFF;
                info.bg->AtkResNode.Color.G = 0xFF;
                info.bg->AtkResNode.Color.B = 0xFF;
                info.bg->AtkResNode.Color.A = 0xFF;

                hook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            var castColor = type switch
            {
                DamageType.Physical => _configuration.PhysicalCastColor,
                DamageType.Magic => _configuration.MagicCastColor,
                DamageType.Darkness => _configuration.DarknessCastColor,
                _ => Vector4.One
            };

            var bgColor = type switch
            {
                DamageType.Physical => _configuration.PhysicalBgColor,
                DamageType.Magic => _configuration.MagicBgColor,
                DamageType.Darkness => _configuration.DarknessBgColor,
                _ => Vector4.One
            };

            info.gauge->AtkResNode.Color.R = (byte)(castColor.X * 255);
            info.gauge->AtkResNode.Color.G = (byte)(castColor.Y * 255);
            info.gauge->AtkResNode.Color.B = (byte)(castColor.Z * 255);
            info.gauge->AtkResNode.Color.A = (byte)(castColor.W * 255);

            info.bg->AtkResNode.Color.R = (byte)(bgColor.X * 255);
            info.bg->AtkResNode.Color.G = (byte)(bgColor.Y * 255);
            info.bg->AtkResNode.Color.B = (byte)(bgColor.Z * 255);
            info.bg->AtkResNode.Color.A = (byte)(bgColor.W * 255);

            hook.Original(thisPtr, a2, a3, a4, a5);
        }

        private void AddScreenLogDetour(
            FFXIVClientStructs.FFXIV.Client.Game.Character.Character* target,
            FFXIVClientStructs.FFXIV.Client.Game.Character.Character* source,
            FlyTextKind logKind,
            int option,
            int actionKind,
            int actionId,
            int val1,
            int val2,
            int val3,
            int val4)
        {
            var targetId = target->GameObject.ObjectID;
            var sourceId = source->GameObject.ObjectID;
            
            if (_configuration.DebugLogEnabled)
            {
                DebugLog(LogType.ScreenLog, $"[ScreenLog] {option} {actionKind} {actionId}");
                DebugLog(LogType.ScreenLog, $"[ScreenLog] {val1} {val2} {val3} {val4}");
                var targetName = _objectTable.SearchById(targetId)?.Name;
                var sourceName  = _objectTable.SearchById(sourceId)?.Name;
                DebugLog(LogType.ScreenLog, $"[ScreenLog] src {sourceId} {sourceName}");
                DebugLog(LogType.ScreenLog, $"[ScreenLog] tgt {targetId} {targetName}");    
            }
            
            _actions.Add(new ScreenLogInfo
            {
                actionId = (uint) actionId,
                kind = logKind,
                sourceId = sourceId,
                targetId = targetId,
                value = val1,
            });
            
            _addScreenLogHook.Original(target, source, logKind, option, actionKind, actionId, val1, val2, val3, val4);
        }

        private IntPtr WriteFlyTextDataDetour(IntPtr a1, NumberArrayData* numberArray, uint numberArrayIndex, IntPtr a4, int a5, int* ftData, uint a7, uint a8)
        {
            var result = _writeFlyTextHook.Original(a1, numberArray, numberArrayIndex, a4, a5, ftData, a7, a8);

            if (numberArray == null || ftData == null || !_configuration.ColorEnabled) return result;
            
            var ftKind = (FlyTextKind) ftData[0];
            var actionId = (uint) ftData[2];
            if (actionId == 0) return result;
            
            var dmgType = _actionToDamageTypeDict[actionId];
            var color = 0;

            if (!IsColorableFlyText(ftKind)) return result;

            color = dmgType switch
            {
                DamageType.Physical => (int)ImGui.GetColorU32(_configuration.PhysicalColor),
                DamageType.Magic => (int)ImGui.GetColorU32(_configuration.MagicColor),
                DamageType.Darkness => (int)ImGui.GetColorU32(_configuration.DarknessColor),
                _ => color
            };
            numberArray->IntArray[numberArrayIndex + 5] = color;
            return result;
        }

        private void OnFlyTextCreated(
            ref FlyTextKind kind,
            ref int val1,
            ref int val2,
            ref SeString text1,
            ref SeString text2,
            ref uint color,
            ref uint icon,
            ref float yOffset,
            ref bool handled)
        {
            try
            {
                if (_configuration.DebugLogEnabled)
                {
                    var str1 = text1?.TextValue.Replace("%", "%%");
                    var str2 = text2?.TextValue.Replace("%", "%%");

                    DebugLog(LogType.FlyText, $"kind: {kind} ({(int)kind}), val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}");
                    DebugLog(LogType.FlyText, $"text1: {str1} | text2: {str2}");
                }

                var ftKind = kind;
                var ftVal1 = val1;
                var charaActorId = GetCharacterActorId();
                var charaPetId = FindCharaPet();
                var action = 
                    _actions.FirstOrDefault(
                        x => x.kind == ftKind 
                             && x.value == ftVal1 
                             && (x.sourceId == charaActorId || x.sourceId == charaPetId || x.targetId == charaActorId));

                if (!_actions.Remove(action)) return;
                
                var charaId = GetCharacterActorId();
                var petId = FindCharaPet();
                
                if (_configuration.SourceTextEnabled)
                {
                    bool tgtCheck = action.sourceId != charaId && action.sourceId != petId;
                    bool petCheck = action.sourceId == petId && _configuration.PetSourceTextEnabled;
                
                    if (tgtCheck || petCheck)
                    {
                        text2 = GetNewText(action.sourceId, text2);
                    }
                }
                
                // Attack text checks
                if ((action.sourceId != charaId && action.sourceId != petId && !_configuration.IncomingAttackTextEnabled)
                    || (action.sourceId == charaId && !_configuration.OutgoingAttackTextEnabled)
                    || (action.sourceId == petId && !_configuration.PetAttackTextEnabled))
                {
                    text1 = "";
                }
            }
            catch (Exception e)
            {
                PluginLog.Information($"{e.Message} {e.StackTrace}");
            }
        }

        private SeString GetNewText(uint sourceId, SeString originalText)
        {
            SeString name = GetActorName(sourceId);
            var newPayloads = new List<Payload>();

            if (name.Payloads.Count == 0) return originalText;

            switch (_clientState.ClientLanguage)
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
                PluginLog.Information($"[{type}] {str}");
        }

        private bool IsColorableFlyText(FlyTextKind kind)
        {
            return kind is FlyTextKind.AutoAttack
                or FlyTextKind.DirectHit
                or FlyTextKind.CriticalHit
                or FlyTextKind.CriticalDirectHit
                or FlyTextKind.NamedAttack
                or FlyTextKind.NamedDirectHit
                or FlyTextKind.NamedCriticalHit
                or FlyTextKind.NamedCriticalDirectHit
                or FlyTextKind.Miss
                or FlyTextKind.NamedMiss
                or FlyTextKind.Dodge
                or FlyTextKind.NamedDodge
                or FlyTextKind.NamedAttack2
                or FlyTextKind.Invulnerable
                or FlyTextKind.AutoAttackNoText
                or FlyTextKind.AutoAttackNoText2
                or FlyTextKind.CriticalHit2
                or FlyTextKind.AutoAttackNoText3
                or FlyTextKind.NamedCriticalHit2
                or FlyTextKind.Named
                or FlyTextKind.Incapacitated
                or FlyTextKind.NamedFullyResisted
                or FlyTextKind.NamedHasNoEffect
                or FlyTextKind.NamedAttack3
                or FlyTextKind.Resist
                or FlyTextKind.AutoAttackNoText4
                or FlyTextKind.CriticalHit3
                or FlyTextKind.Reflect
                or FlyTextKind.Reflected
                or FlyTextKind.DirectHit2
                or FlyTextKind.CriticalHit4
                or FlyTextKind.CriticalDirectHit2;
        }
    }
}