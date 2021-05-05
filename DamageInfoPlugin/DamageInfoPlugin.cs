using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game.ClientState.Actors.Types.NonPlayer;
using Dalamud.Game.ClientState.Structs;
using Dalamud.Game.Internal.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Actor = Dalamud.Game.ClientState.Actors.Types.Actor;

namespace DamageInfoPlugin
{
    public unsafe class DamageInfoPlugin : IDalamudPlugin
    {
        // when a flytext 
        private const int CleanupInterval = 10000;

        private const int TargetInfoGaugeBgNodeIndex = 41;
        private const int TargetInfoGaugeNodeIndex = 43;

        private const int TargetInfoSplitGaugeBgNodeIndex = 2;
        private const int TargetInfoSplitGaugeNodeIndex = 4;
        
        private const int FocusTargetInfoGaugeBgNodeIndex = 13;
        private const int FocusTargetInfoGaugeNodeIndex = 15;

        public string Name => "Damage Info";

        private const string CommandName = "/dmginfo";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;

        public bool Randomize { get; set; }

        private Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;
        
        private Hook<SetCastBarDelegate> setCastBarHook;
        private Hook<SetCastBarDelegate> setFocusTargetCastBarHook;

        private CastbarInfo _nullCastbarInfo;
        private Dictionary<uint, DamageType> actionToDamageTypeDict;
        private HashSet<uint> ignoredCastActions;
        private ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>> futureFlyText;
        private long lastCleanup;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            lastCleanup = Ms();
            actionToDamageTypeDict = new Dictionary<uint, DamageType>();
            futureFlyText = new ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>>();
            ignoredCastActions = new HashSet<uint>();

            pi = pluginInterface;

            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi, this);
            ui = new PluginUI(configuration, this);

            pi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
                {HelpMessage = "Display the Damage Info configuration interface."});

            _nullCastbarInfo = new CastbarInfo {unitBase = null, gauge = null, bg = null};
            
            var actionSheet = pi.Data.GetExcelSheet<Action>();
            foreach (var row in actionSheet)
            {
                DamageType tmpType = (DamageType) row.AttackType.Row;
                if (tmpType != DamageType.Magic && tmpType != DamageType.Darkness && tmpType != DamageType.Unknown)
                    tmpType = DamageType.Physical;

                actionToDamageTypeDict.Add(row.RowId, tmpType);
                
                if (row.ActionCategory.Row > 4 && row.ActionCategory.Row < 11)
                    ignoredCastActions.Add(row.ActionCategory.Row);
            }

            try
            {
                IntPtr receiveActionEffectFuncPtr =
                    pi.TargetModuleScanner.ScanText("4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9");
                receiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr,
                    (ReceiveActionEffectDelegate) ReceiveActionEffect);

                IntPtr setCastBarFuncPtr = pi.TargetModuleScanner.ScanText(
                    "48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 80 7C 24 ?? ?? 49 8B D9 49 8B E8 48 8B F2 74 22 49 8B 09 66 41 C7 41 ?? ?? ?? E8 ?? ?? ?? ?? 66 83 F8 69 75 0D 48 8B 0B BA ?? ?? ?? ?? E8 ?? ?? ?? ??");
                setCastBarHook = new Hook<SetCastBarDelegate>(setCastBarFuncPtr, (SetCastBarDelegate) SetCastBarDetour);
                
                IntPtr setFocusTargetCastBarFuncPtr = pi.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 41 0F B6 F9 49 8B E8 48 8B F2 48 8B D9");
                setFocusTargetCastBarHook = new Hook<SetCastBarDelegate>(setFocusTargetCastBarFuncPtr, (SetCastBarDelegate) SetFocusTargetCastBarDetour);

                pi.Framework.Gui.FlyText.OnFlyText += OnFlyText;
            }
            catch (Exception ex)
            {
                PluginLog.Log($"Encountered an error loading DamageInfoPlugin: {ex.Message}");
                PluginLog.Log("Plugin will not be loaded.");

                receiveActionEffectHook?.Disable();
                receiveActionEffectHook?.Dispose();
                setCastBarHook?.Disable();
                setCastBarHook?.Dispose();
                setFocusTargetCastBarHook?.Disable();
                setFocusTargetCastBarHook?.Dispose();
                pi.CommandManager.RemoveHandler(CommandName);

                throw;
            }

            receiveActionEffectHook.Enable();
            setCastBarHook.Enable();
            setFocusTargetCastBarHook.Enable();

            pi.UiBuilder.OnBuildUi += DrawUI;
            pi.UiBuilder.OnOpenConfigUi += (_, _) => DrawConfigUI();
        }

        public void Dispose()
        {
            ClearFlyTextQueue();
            ResetMainTargetCastBar();
            ResetFocusTargetCastBar();
            receiveActionEffectHook?.Disable();
            receiveActionEffectHook?.Dispose();
            setCastBarHook?.Disable();
            setCastBarHook?.Dispose();
            setFocusTargetCastBarHook?.Disable();
            setFocusTargetCastBarHook?.Dispose();
            
            pi.Framework.Gui.FlyText.OnFlyText -= OnFlyText;

            futureFlyText = null;
            actionToDamageTypeDict = null;

            ui.Dispose();
            pi.CommandManager.RemoveHandler(CommandName);
            pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            ui.SettingsVisible = true;
        }

        private void DrawUI()
        {
            ui.Draw();
        }

        private void DrawConfigUI()
        {
            ui.SettingsVisible = true;
        }

        private ushort GetCurrentCast(IntPtr actor)
        {
            return (ushort) Marshal.ReadInt16(actor, ActorOffsets.CurrentCastSpellActionId);
        }
        
        private CastbarInfo GetTargetInfoUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("_TargetInfo", 1).ToPointer();

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
            AtkUnitBase* unitbase = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("_TargetInfoCastBar", 1).ToPointer();
            
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
            AtkUnitBase* unitbase = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("_FocusTargetInfo", 1).ToPointer();
            
            if (unitbase == null) return _nullCastbarInfo;
            
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->UldManager.NodeList[FocusTargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->UldManager.NodeList[FocusTargetInfoGaugeBgNodeIndex]
            };
        }
        
        private int FindCharaPet()
        {
            int charaId = GetCharacterActorId();
            foreach (Actor a in pi.ClientState.Actors)
            {
                if (!(a is BattleNpc npc)) continue;

                IntPtr actPtr = npc.Address;
                if (actPtr == IntPtr.Zero) continue;

                if (npc.OwnerId == charaId)
                    return npc.ActorId;
            }

            return -1;
        }

        private int GetCharacterActorId()
        {
            if (pi.ClientState.LocalPlayer != null)
                return pi.ClientState.LocalPlayer.ActorId;
            return 0;
        }

        private SeString GetActorName(int id)
        {
            foreach (Actor t in pi.ClientState.Actors)
                if (t != null)
                    if (id == t.ActorId)
                        return pi.SeStringManager.Parse(t.Address + ActorOffsets.Name);
            return "";
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

        private delegate void SetCastBarDelegate(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5);

        private void SetCastBarDetour(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (!configuration.MainTargetCastBarColorEnabled)
            {
                setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            var targetInfo = GetTargetInfoUiElements();
            var splitInfo = GetTargetInfoSplitUiElements();

            bool combinedInvalid = targetInfo.unitBase == null || targetInfo.gauge == null || targetInfo.bg == null;
            bool splitInvalid = splitInfo.unitBase == null || splitInfo.gauge == null || splitInfo.bg == null;
            
            if (combinedInvalid && splitInvalid)
            {
                setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            if (thisPtr.ToPointer() == targetInfo.unitBase && !combinedInvalid)
            {
                IntPtr? mainTarget = pi.ClientState?.Targets?.CurrentTarget?.Address;
                ColorCastBar(mainTarget, targetInfo, setCastBarHook,
                    thisPtr, a2, a3, a4, a5);
            }
            else 
            if (thisPtr.ToPointer() == splitInfo.unitBase && !splitInvalid)
            {
                IntPtr? mainTarget = pi.ClientState?.Targets?.CurrentTarget?.Address;
                ColorCastBar(mainTarget, splitInfo, setCastBarHook,
                    thisPtr, a2, a3, a4, a5);
            }
        }

        private void SetFocusTargetCastBarDetour(IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (!configuration.FocusTargetCastBarColorEnabled)
            {
                setFocusTargetCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }
            
            var ftInfo = GetFocusTargetUiElements();
            
            bool focusTargetInvalid = ftInfo.unitBase == null || ftInfo.gauge == null || ftInfo.bg == null; 
            
            if (thisPtr.ToPointer() == ftInfo.unitBase && !focusTargetInvalid)
            {
                IntPtr? focusTarget = pi.ClientState?.Targets?.FocusTarget?.Address;
                ColorCastBar(focusTarget, ftInfo, setFocusTargetCastBarHook,
                                thisPtr, a2, a3, a4, a5);
            }
        }
        
        private void ColorCastBar(IntPtr? target, CastbarInfo info, Hook<SetCastBarDelegate> hook,
            IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (!target.HasValue || target.Value == IntPtr.Zero)
            {
                hook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }
            
            ushort actionId = GetCurrentCast(target.Value);
            
            actionToDamageTypeDict.TryGetValue(actionId, out DamageType type);
            if (ignoredCastActions.Contains(actionId))
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
                DamageType.Physical => configuration.PhysicalCastColor,
                DamageType.Magic => configuration.MagicCastColor,
                DamageType.Darkness => configuration.DarknessCastColor,
                _ => Vector4.One
            };
                    
            var bgColor = type switch
            {
                DamageType.Physical => configuration.PhysicalBgColor,
                DamageType.Magic => configuration.MagicBgColor,
                DamageType.Darkness => configuration.DarknessBgColor,
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

        private void OnFlyText(
            ref FlyTextKind kind,
            ref uint actorIndex,
            ref uint val1,
            ref uint val2,
            ref SeString text1,
            ref SeString text2,
            ref uint color,
            ref uint icon,
            ref bool dirty,
            ref bool handled)
        {
            if (Randomize)
                val1 = ModifyDamageALittle(val1);

            try
            {
                FlyTextKind ftKind = kind;

                // wrap this here to lower overhead when not logging
                if (configuration.FlyTextLogEnabled)
                {
                    var str1 = text1?.TextValue?.Replace("%", "%%");
                    var str2 = text2?.TextValue?.Replace("%", "%%");

                    FlyTextLog($"flytext created: kind: {ftKind} ({kind}), val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}");
                    FlyTextLog($"text1: {str1} | text2: {str2}");
                }

                if (TryGetFlyTextDamageType(val1, out DamageType dmgType, out int sourceId))
                {
                    int charaId = GetCharacterActorId();
                    int petId = FindCharaPet();

                    if (configuration.OutgoingColorEnabled || configuration.IncomingColorEnabled)
                    {
                        bool outPlayer = sourceId == charaId && configuration.OutgoingColorEnabled;
                        bool outPet = sourceId == petId && configuration.PetDamageColorEnabled;
                        bool outCheck = outPlayer || outPet;

                        bool incCheck = sourceId != charaId && sourceId != petId && configuration.IncomingColorEnabled;

                        // match up the condition with what to check
                        // because right now with this OR, it doesn't care if the source is incoming and outgoing is enabled
                        // so make sure that it oes it right
                        if (outCheck && !incCheck || !outCheck && incCheck)
                        {
                            if (ftKind == FlyTextKind.AutoAttack
                                || ftKind == FlyTextKind.CriticalHit
                                || ftKind == FlyTextKind.DirectHit
                                || ftKind == FlyTextKind.CriticalDirectHit
                                || ftKind == FlyTextKind.NamedAttack
                                || ftKind == FlyTextKind.NamedDirectHit
                                || ftKind == FlyTextKind.NamedCriticalHit
                                || ftKind == FlyTextKind.NamedCriticalDirectHit)
                            {
                                switch (dmgType)
                                {
                                    case DamageType.Physical:
                                        color = ImGui.GetColorU32(configuration.PhysicalColor);
                                        break;
                                    case DamageType.Magic:
                                        color = ImGui.GetColorU32(configuration.MagicColor);
                                        break;
                                    case DamageType.Darkness:
                                        color = ImGui.GetColorU32(configuration.DarknessColor);
                                        break;
                                }
                            }
                        }
                    }

                    if (configuration.SourceTextEnabled)
                    {
                        bool tgtCheck = sourceId != charaId && sourceId != petId;
                        bool petCheck = sourceId == petId && configuration.PetSourceTextEnabled;

                        if (tgtCheck || petCheck)
                            text2 = GetNewText(sourceId, text2);
                    }

                    // Attack text checks
                    if ((sourceId != charaId && sourceId != petId && !configuration.IncomingAttackTextEnabled) ||
                        (sourceId == charaId && !configuration.OutgoingAttackTextEnabled) ||
                        (sourceId == petId && !configuration.PetAttackTextEnabled))
                        text1 = "";
                }
            }
            catch (Exception e)
            {
                PluginLog.Log($"{e.Message} {e.StackTrace}");
            }

            dirty = true;
        }

        private SeString GetNewText(int sourceId, SeString originalText)
        {
            SeString name = GetActorName(sourceId);
            var newPayloads = new List<Payload>();

            if (name.Payloads.Count == 0) return originalText;
            
            switch (pi.ClientState.ClientLanguage)
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

        private delegate void ReceiveActionEffectDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos,
            IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);

        private void ReceiveActionEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos,
            IntPtr effectHeader,
            IntPtr effectArray, IntPtr effectTrail)
        {
            try
            {
                Cleanup();
                // no log, no processing... just get him outta here
                if ((!configuration.EffectLogEnabled &&
                     !(configuration.IncomingColorEnabled || configuration.OutgoingColorEnabled) &&
                     !configuration.SourceTextEnabled))
                {
                    receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray,
                        effectTrail);
                    return;
                }

                uint id = *((uint*) effectHeader.ToPointer() + 0x2);
                uint animId = *((ushort*) effectHeader.ToPointer() + 0xE);
                ushort op = *((ushort*) effectHeader.ToPointer() - 0x7);
                byte targetCount = *(byte*) (effectHeader + 0x21);
                EffectLog(
                    $"--- source actor: {sourceId}, action id {id}, anim id {animId}, opcode: {op:X} numTargets: {targetCount} ---");

// #if DEBUG
                if (configuration.EffectLogEnabled)
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

                int effectsEntries = 0;
                int targetEntries = 1;
                if (targetCount == 0)
                {
                    effectsEntries = 0;
                    targetEntries = 1;
                }
                else if (targetCount == 1)
                {
                    effectsEntries = 8;
                    targetEntries = 1;
                }
                else if (targetCount <= 8)
                {
                    effectsEntries = 64;
                    targetEntries = 8;
                }
                else if (targetCount <= 16)
                {
                    effectsEntries = 128;
                    targetEntries = 16;
                }
                else if (targetCount <= 24)
                {
                    effectsEntries = 192;
                    targetEntries = 24;
                }
                else if (targetCount <= 32)
                {
                    effectsEntries = 256;
                    targetEntries = 32;
                }

                List<EffectEntry> entries = new List<EffectEntry>(effectsEntries);

                for (int i = 0; i < effectsEntries; i++)
                {
                    entries.Add(*(EffectEntry*) (effectArray + i * 8));
                }

                ulong[] targets = new ulong[targetEntries];

                for (int i = 0; i < targetCount; i++)
                {
                    targets[i] = *(ulong*) (effectTrail + i * 8);
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    ulong tTarget = targets[i / 8];
                    uint tDmg = entries[i].value;
                    if (entries[i].mult != 0)
                        tDmg += ((uint) ushort.MaxValue + 1) * entries[i].mult;

                    if (entries[i].type == ActionEffectType.Damage
                        || entries[i].type == ActionEffectType.BlockedDamage
                        || entries[i].type == ActionEffectType.ParriedDamage
                        // || entries[i].type == ActionEffectType.Miss
                    )
                    {
                        EffectLog($"{entries[i]}, s: {sourceId} t: {tTarget}");
                        if (tDmg == 0) continue;

                        int actId = GetCharacterActorId();
                        int charaPet = FindCharaPet();

                        // if source text is enabled, we know exactly when to add it
                        if (configuration.SourceTextEnabled &&
                            ((int) tTarget == actId || configuration.PetSourceTextEnabled && sourceId == charaPet))
                        {
                            AddToFutureFlyText(tDmg, actionToDamageTypeDict[animId], sourceId);
                        }
                        else if (configuration.OutgoingColorEnabled &&
                                 (sourceId == actId || configuration.PetDamageColorEnabled && sourceId == charaPet))
                        {
                            AddToFutureFlyText(tDmg, actionToDamageTypeDict[animId], sourceId);
                        }
                        else if ((int) tTarget == actId && configuration.IncomingColorEnabled)
                        {
                            AddToFutureFlyText(tDmg, actionToDamageTypeDict[animId], sourceId);
                        }
                    }
                }

                receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray,
                    effectTrail);
            }
            catch (Exception e)
            {
                PluginLog.Log($"{e.Message} {e.StackTrace}");
            }
        }

        private void EffectLog(string str)
        {
            if (configuration.EffectLogEnabled)
                PluginLog.Log($"[effect] {str}");
        }

        private void FlyTextLog(string str)
        {
            if (configuration.FlyTextLogEnabled)
                PluginLog.Log($"[flytext] {str}");
        }

        private bool TryGetFlyTextDamageType(uint dmg, out DamageType type, out int sourceId)
        {
            type = DamageType.Unknown;
            sourceId = 0;
            if (!futureFlyText.TryGetValue(dmg, out var list) || list == null || list.Count == 0) return false;

            var item = list[0];
            foreach (var tuple in list)
                if (tuple.Item1 < item.Item1)
                    item = tuple;
            list.Remove(item);
            type = item.Item2;
            sourceId = item.Item3;

            return true;
        }

        private long Ms()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        }

        private void AddToFutureFlyText(uint dmg, DamageType type, int sourceId)
        {
            long ms = Ms();
            var toInsert = new Tuple<long, DamageType, int>(ms, type, sourceId);

            if (futureFlyText.TryGetValue(dmg, out var list))
            {
                if (list != null)
                {
                    list.Add(toInsert);
                    return;
                }
            }

            var tmpList = new List<Tuple<long, DamageType, int>> {toInsert};
            futureFlyText[dmg] = tmpList;
        }

        // Not all effect packets end up being flytext
        // so we have to clean up the orphaned entries here
        private void Cleanup()
        {
            if (futureFlyText == null) return;

            long ms = Ms();
            if (ms - lastCleanup < CleanupInterval) return;

            // FlyTextLog($"pre-cleanup flytext: {futureFlyText.Values.Count}");
            // FlyTextLog($"pre-cleanup text: {text.Count}");
            lastCleanup = ms;

            var toRemove = new List<uint>();

            foreach (uint key in futureFlyText.Keys)
            {
                if (!futureFlyText.TryGetValue(key, out var list)) continue;
                if (list == null)
                {
                    toRemove.Add(key);
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    long diff = ms - list[i].Item1;
                    if (diff <= 5000) continue;
                    list.Remove(list[i]);
                    i--;
                }

                if (list.Count == 0)
                    toRemove.Add(key);
            }

            foreach (uint key in toRemove)
                futureFlyText.TryRemove(key, out var unused);
            
            // FlyTextLog($"post-cleanup flytext: {futureFlyText.Values.Count}");
            // FlyTextLog($"post-cleanup text: {text.Count}");
        }
        
        public void ClearFlyTextQueue()
        {
            if (futureFlyText == null) return;

            FlyTextLog($"clearing flytext queue of {futureFlyText.Values.Count} items...");
            futureFlyText.Clear();
        }

        private uint ModifyDamageALittle(uint originalDamage)
        {
            var margin = (int) Math.Truncate(originalDamage * 0.1);
            var rand = new Random();
            var newDamage = rand.Next(unchecked((int) originalDamage) - margin, unchecked((int) originalDamage) + margin);
            PluginLog.Log($"og dmg: {originalDamage}, new dmg: {newDamage}");
            return unchecked((uint) newDamage);
        }
    }
}