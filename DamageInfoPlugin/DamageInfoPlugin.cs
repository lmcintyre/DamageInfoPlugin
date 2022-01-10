using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using static DamageInfoPlugin.LogType;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

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
        private readonly Hook<AddScreenLogDelegate> _addScreenLogHook;

        private delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);
        private readonly Hook<ReceiveActionEffectDelegate> _receiveActionEffectHook;
        
        private delegate void SetCastBarDelegate(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5);
        private readonly Hook<SetCastBarDelegate> _setCastBarHook;
        private readonly Hook<SetCastBarDelegate> _setFocusTargetCastBarHook;
        
        private readonly CastbarInfo _nullCastbarInfo;
        
        private Dictionary<uint, DamageType> _actionToDamageTypeDict;
        private readonly HashSet<uint> _ignoredCastActions;

        private ActionEffectStore _actionStore;

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
            _actionStore = new ActionEffectStore();
            _ignoredCastActions = new HashSet<uint>();

            _configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _configuration.Initialize(pi, this);
            _ui = new PluginUI(_configuration, this);

            cmdMgr.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Display the Damage Info configuration interface."
            });

            _nullCastbarInfo = new CastbarInfo {unitBase = null, gauge = null, bg = null};

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
                
                    if (row.ActionCategory.Row > 4 && row.ActionCategory.Row < 11)
                        _ignoredCastActions.Add(row.ActionCategory.Row);
                }
                
                var receiveActionEffectFuncPtr = scanner.ScanText("4C 89 44 24 ?? 53 56 57 41 54 41 57");
                _receiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr, (ReceiveActionEffectDelegate) ReceiveActionEffect);
                
                var addScreenLogPtr = scanner.ScanText("E8 ?? ?? ?? ?? BB ?? ?? ?? ?? EB 37");
                _addScreenLogHook = new Hook<AddScreenLogDelegate>(addScreenLogPtr, (AddScreenLogDelegate) AddScreenLogDetour);

                var setCastBarFuncPtr = scanner.ScanText("E8 ?? ?? ?? ?? 4C 8D 8F ?? ?? ?? ?? 4D 8B C6");
                _setCastBarHook = new Hook<SetCastBarDelegate>(setCastBarFuncPtr, (SetCastBarDelegate) SetCastBarDetour);
                
                var setFocusTargetCastBarFuncPtr = scanner.ScanText("E8 ?? ?? ?? ?? 49 8B 47 20 4C 8B 6C 24");
                _setFocusTargetCastBarHook = new Hook<SetCastBarDelegate>(setFocusTargetCastBarFuncPtr, (SetCastBarDelegate) SetFocusTargetCastBarDetour);

                ftGui.FlyTextCreated += OnFlyTextCreated;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"An error occurred loading DamageInfoPlugin.");
                PluginLog.Error("Plugin will not be loaded.");

                _addScreenLogHook?.Disable();
                _addScreenLogHook?.Dispose();
                _receiveActionEffectHook?.Disable();
                _receiveActionEffectHook?.Dispose();
                _setCastBarHook?.Disable();
                _setCastBarHook?.Dispose();
                _setFocusTargetCastBarHook?.Disable();
                _setFocusTargetCastBarHook?.Dispose();
                cmdMgr.RemoveHandler(CommandName);

                throw;
            }

            _receiveActionEffectHook.Enable();
            _addScreenLogHook.Enable();
            _setCastBarHook.Enable();
            _setFocusTargetCastBarHook.Enable();

            pi.UiBuilder.Draw += DrawUI;
            pi.UiBuilder.OpenConfigUi += DrawConfigUI;
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
            
            _ftGui.FlyTextCreated -= OnFlyTextCreated;

            _actionStore = null;
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
        
        private CastbarInfo GetTargetInfoUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*) _gameGui.GetAddonByName("_TargetInfo", 1).ToPointer();

            if (unitbase == null) return _nullCastbarInfo;
            
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->UldManager.NodeList[TargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->UldManager.NodeList[TargetInfoGaugeBgNodeIndex]
            };
        }

        private CastbarInfo GetTargetInfoSplitUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*) _gameGui.GetAddonByName("_TargetInfoCastBar", 1).ToPointer();
            
            if (unitbase == null) return _nullCastbarInfo;
            
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->UldManager.NodeList[TargetInfoSplitGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->UldManager.NodeList[TargetInfoSplitGaugeBgNodeIndex]
            };
        }
        
        private CastbarInfo GetFocusTargetUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*) _gameGui.GetAddonByName("_FocusTargetInfo", 1).ToPointer();
            
            if (unitbase == null) return _nullCastbarInfo;
            
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->UldManager.NodeList[FocusTargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->UldManager.NodeList[FocusTargetInfoGaugeBgNodeIndex]
            };
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
            return _clientState.LocalPlayer.ObjectId;
        }

        private SeString GetActorName(uint id)
        {
            return _objectTable.SearchById(id)?.Name ?? SeString.Empty;
        }

        #region castbar
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
            
            info.gauge->AtkResNode.Color.R = (byte) (castColor.X * 255);
            info.gauge->AtkResNode.Color.G = (byte) (castColor.Y * 255);
            info.gauge->AtkResNode.Color.B = (byte) (castColor.Z * 255);
            info.gauge->AtkResNode.Color.A = (byte) (castColor.W * 255);

            info.bg->AtkResNode.Color.R = (byte) (bgColor.X * 255);
            info.bg->AtkResNode.Color.G = (byte) (bgColor.Y * 255);
            info.bg->AtkResNode.Color.B = (byte) (bgColor.Z * 255);
            info.bg->AtkResNode.Color.A = (byte) (bgColor.W * 255);
            
            hook.Original(thisPtr, a2, a3, a4, a5);
        }
        #endregion

        private void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
        {
            try
            {
                _actionStore.Cleanup();
                // no log, no processing... just get him outta here
                // if ((!_configuration.DebugLogEnabled &&
                //      !(_configuration.IncomingColorEnabled || _configuration.OutgoingColorEnabled) &&
                //      !_configuration.SourceTextEnabled))
                // {
                //     _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
                //     return;
                // }
                
                DebugLog(Effect, $"--- source actor: {sourceCharacter->GameObject.ObjectID}, action id {effectHeader->ActionId}, anim id {effectHeader->AnimationId} numTargets: {effectHeader->TargetCount} ---");
                
                // TODO: Reimplement opcode logging, if it's even useful. Original code follows
                // ushort op = *((ushort*) effectHeader.ToPointer() - 0x7);
                // DebugLog(Effect, $"--- source actor: {sourceId}, action id {id}, anim id {animId}, opcode: {op:X} numTargets: {targetCount} ---");
                
// #if DEBUG
                if (_configuration.DebugLogEnabled)
                {
                    // EffectLog($"packet (effectHeader): {effectHeader.ToInt64():X}");
                    // LogFromPtr(effectHeader, 1024);
                    //
                    // EffectLog($"effectArray: {effectArray.ToInt64():X}");
                    // LogFromPtr(effectArray, 64);
                    //
                    // EffectLog($"effectTrail: {effectTrail.ToInt64():X}");
                    // LogFromPtr(effectTrail, 64);

                    // LogFromPtr(unk6, 64);
                }
// #endif

                GetEntryCount(effectHeader->TargetCount, out var effectsEntries, out var targetEntries);

                for (int i = 0; i < effectsEntries; i++)
                {
                    if (effectArray[i].type == ActionEffectType.Nothing) continue;
                    
                    var tTarget = effectTail[i / 8];
                    uint tDmg = effectArray[i].value;
                    if (effectArray[i].mult != 0)
                        tDmg += ((uint) ushort.MaxValue + 1) * effectArray[i].mult;
                    
                    DebugLog(Effect, $"{effectArray[i]}, s: {sourceId} t: {tTarget}");
                    
                    var newEffect = new ActionEffectInfo
                    {
                        step = ActionStep.Effect,
                        actionId = effectHeader->ActionId,
                        type = effectArray[i].type,
                        // kind =
                        sourceId = sourceId,
                        targetId = tTarget,
                        value = tDmg,
                    };
                    
                    _actionStore.AddEffect(newEffect);
                }

                _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error has occurred in Damage Info");
            }
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
            int val3,
            int val4)
        {
            try
            {
                var targetId = target->GameObject.ObjectID;
                var sourceId = source->GameObject.ObjectID;
            
                if (_configuration.DebugLogEnabled)
                {
                    DebugLog(ScreenLog, $"{option} {actionKind} {actionId}");
                    DebugLog(ScreenLog, $"{val1} {val2} {val3} {val4}");
                    var targetName = GetActorName(targetId);
                    var sourceName  = GetActorName(sourceId);
                    DebugLog(ScreenLog, $"src {sourceId} {sourceName}");
                    DebugLog(ScreenLog, $"tgt {targetId} {targetName}");    
                }

                _actionStore.UpdateEffect((uint)actionId, sourceId, targetId, (uint)val1, logKind);

                // var action = new ActionEffectInfo
                // {
                //     actionId = (uint) actionId,
                //     kind = logKind,
                //     sourceId = sourceId,
                //     targetId = targetId,
                //     value = val1,
                // };

                // _actions.Add(action);
                // DebugLog(LogType.ScreenLog, $"added action: {action}");
                // DebugLog(LogType.ScreenLog, $"_actions size: {_actions.Count}");
                //z
                _addScreenLogHook.Original(target, source, logKind, option, actionKind, actionId, val1, val2, val3, val4);
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error occurred in Damage Info.");
            }
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
                var ftKind = kind;

                // wrap this here to lower overhead when not logging
                if (_configuration.DebugLogEnabled)
                {
                    var str1 = text1?.TextValue?.Replace("%", "%%");
                    var str2 = text2?.TextValue?.Replace("%", "%%");

                    DebugLog(FlyText, $"flytext created: kind: {ftKind} ({(int)kind}), val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}");
                    DebugLog(FlyText, $"text1: {str1} | text2: {str2}");
                }

                if (_actionStore.TryGetEffect((uint) val1, ftKind, out var info))
                {
                    DebugLog(FlyText, $"Obtained info: {info}");
                    var charaId = GetCharacterActorId();
                    var petId = FindCharaPet();

                    if (_configuration.OutgoingColorEnabled || _configuration.IncomingColorEnabled)
                    {
                        bool outPlayer = info.sourceId == charaId && _configuration.OutgoingColorEnabled;
                        bool outPet = info.sourceId == petId && _configuration.PetDamageColorEnabled;
                        bool outCheck = outPlayer || outPet;

                        bool incCheck = info.sourceId != charaId && info.sourceId != petId && _configuration.IncomingColorEnabled;

                        // match up the condition with what to check
                        // because right now with this OR, it doesn't care if the source is incoming and outgoing is enabled
                        // so make sure that it oes it right
                        if (outCheck && !incCheck || !outCheck && incCheck)
                        {
                            // if (ftKind == FlyTextKind.AutoAttack
                            //     || ftKind == FlyTextKind.CriticalHit
                            //     || ftKind == FlyTextKind.DirectHit
                            //     || ftKind == FlyTextKind.CriticalDirectHit
                            //     || ftKind == FlyTextKind.NamedAttack
                            //     || ftKind == FlyTextKind.NamedDirectHit
                            //     || ftKind == FlyTextKind.NamedCriticalHit
                            //     || ftKind == FlyTextKind.NamedCriticalDirectHit)
                            {
                                if (_actionToDamageTypeDict.TryGetValue(info.actionId, out var dmgType))
                                    color = GetDamageColor(dmgType);
                            }
                        }
                    }

                    if (_configuration.SourceTextEnabled)
                    {
                        bool tgtCheck = info.sourceId != charaId && info.sourceId != petId;
                        bool petCheck = info.sourceId == petId && _configuration.PetSourceTextEnabled;

                        if (tgtCheck || petCheck)
                        {
                            text2 = GetNewText(info.sourceId, text2);
                        }
                            
                    }

                    // Attack text checks
                    if ((info.sourceId != charaId && info.sourceId != petId && !_configuration.IncomingAttackTextEnabled) ||
                        (info.sourceId == charaId && !_configuration.OutgoingAttackTextEnabled) ||
                        (info.sourceId == petId && !_configuration.PetAttackTextEnabled))
                    {
                        text1 = "";
                    }
                }
                else
                {
                    DebugLog(FlyText, $"Failed to obtain info... {val1} {ftKind}");
                }
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error has occurred in Damage Info");
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
        
        private uint GetDamageColor(DamageType type, uint fallback = 0xFF00008A)
        {
            return type switch
            {
                DamageType.Physical => ImGui.GetColorU32(_configuration.PhysicalColor),
                DamageType.Magic => ImGui.GetColorU32(_configuration.MagicColor),
                DamageType.Darkness => ImGui.GetColorU32(_configuration.DarknessColor),
                _ => fallback
            };
        }

        private void GetEntryCount(int targetCount, out int effectsEntries, out int targetEntries)
        {
            effectsEntries = 0;
            targetEntries = 1;
            switch (targetCount)
            {
                case 0:
                    effectsEntries = 0;
                    targetEntries = 1;
                    break;
                case 1:
                    effectsEntries = 8;
                    targetEntries = 1;
                    break;
                case <= 8:
                    effectsEntries = 64;
                    targetEntries = 8;
                    break;
                case <= 16:
                    effectsEntries = 128;
                    targetEntries = 16;
                    break;
                case <= 24:
                    effectsEntries = 192;
                    targetEntries = 24;
                    break;
                case <= 32:
                    effectsEntries = 256;
                    targetEntries = 32;
                    break;
            }
        }
    }
}