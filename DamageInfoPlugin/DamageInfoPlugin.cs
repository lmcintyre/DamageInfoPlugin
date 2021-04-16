using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud;
using Dalamud.Game.ClientState.Actors.Types.NonPlayer;
using Dalamud.Game.ClientState.Structs;
using Dalamud.Hooking;
using FFXIVClientStructs.Component.GUI;
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

        public bool Hijack { get; set; }
        public bool Randomize { get; set; }
        public HijackStruct HijackStruct { get; set; }

        private Hook<CreateFlyTextDelegate> createFlyTextHook;
        private Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;
        
        private Hook<SetCastBarDelegate> setCastBarHook;
        private Hook<SetCastBarDelegate> setFocusTargetCastBarHook;

        private Dictionary<uint, DamageType> actionToDamageTypeDict;
        private HashSet<uint> ignoredCastActions;
        private ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>> futureFlyText;
        private ConcurrentQueue<Tuple<IntPtr, long>> text;
        private long lastCleanup;
        private IntPtr blankText;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            lastCleanup = Ms();
            actionToDamageTypeDict = new Dictionary<uint, DamageType>();
            futureFlyText = new ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>>();
            text = new ConcurrentQueue<Tuple<IntPtr, long>>();
            ignoredCastActions = new HashSet<uint>();

            pi = pluginInterface;

            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi, this);
            ui = new PluginUI(configuration, this);

            pi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
                {HelpMessage = "Display the Damage Info configuration interface."});

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
                IntPtr createFlyTextFuncPtr = pi.TargetModuleScanner.ScanText(
                    "48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 40 48 63 FA 45 8B F0 48 8B F1 83 FF 34 7C 13 33 C0 48 8B 74 24 ?? 48 8B 7C 24 ?? 48 83 C4 40 41 5E C3");
                createFlyTextHook =
                    new Hook<CreateFlyTextDelegate>(createFlyTextFuncPtr, (CreateFlyTextDelegate) CreateFlyText);

                IntPtr receiveActionEffectFuncPtr =
                    pi.TargetModuleScanner.ScanText("4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9");
                receiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr,
                    (ReceiveActionEffectDelegate) ReceiveActionEffect);

                IntPtr setCastBarFuncPtr = pi.TargetModuleScanner.ScanText(
                    "48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 80 7C 24 ?? ?? 49 8B D9 49 8B E8 48 8B F2 74 22 49 8B 09 66 41 C7 41 ?? ?? ?? E8 ?? ?? ?? ?? 66 83 F8 69 75 0D 48 8B 0B BA ?? ?? ?? ?? E8 ?? ?? ?? ??");
                setCastBarHook = new Hook<SetCastBarDelegate>(setCastBarFuncPtr, (SetCastBarDelegate) SetCastBarDetour);
                
                IntPtr setFocusTargetCastBarFuncPtr = pi.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 41 0F B6 F9 49 8B E8 48 8B F2 48 8B D9");
                setFocusTargetCastBarHook = new Hook<SetCastBarDelegate>(setFocusTargetCastBarFuncPtr, (SetCastBarDelegate) SetFocusTargetCastBarDetour);
            }
            catch (Exception ex)
            {
                PluginLog.Log($"Encountered an error loading DamageInfoPlugin: {ex.Message}");
                PluginLog.Log("Plugin will not be loaded.");

                createFlyTextHook?.Disable();
                createFlyTextHook?.Dispose();
                receiveActionEffectHook?.Disable();
                receiveActionEffectHook?.Dispose();
                setCastBarHook?.Disable();
                setCastBarHook?.Dispose();
                setFocusTargetCastBarHook?.Disable();
                setFocusTargetCastBarHook?.Dispose();
                Marshal.FreeHGlobal(blankText);

                throw;
            }

            blankText = Marshal.AllocHGlobal(1);
            Marshal.WriteByte(blankText, 0);

            createFlyTextHook.Enable();
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
            createFlyTextHook.Disable();
            createFlyTextHook.Dispose();
            receiveActionEffectHook.Disable();
            receiveActionEffectHook.Dispose();
            setCastBarHook?.Disable();
            setCastBarHook?.Dispose();
            setFocusTargetCastBarHook?.Disable();
            setFocusTargetCastBarHook?.Dispose();

            futureFlyText = null;
            actionToDamageTypeDict = null;

            ui.Dispose();
            pi.CommandManager.RemoveHandler(CommandName);
            pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
#if DEBUG
            ui.Visible = true;
#else
            ui.SettingsVisible = true;
#endif
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
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->ULDData.NodeList[TargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->ULDData.NodeList[TargetInfoGaugeBgNodeIndex]
            };
        }

        private CastbarInfo GetTargetInfoSplitUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("_TargetInfoCastBar", 1).ToPointer();
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->ULDData.NodeList[TargetInfoSplitGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->ULDData.NodeList[TargetInfoSplitGaugeBgNodeIndex]
            };
        }
        
        private CastbarInfo GetFocusTargetUiElements()
        {
            AtkUnitBase* unitbase = (AtkUnitBase*) pi.Framework.Gui.GetUiObjectByName("_FocusTargetInfo", 1).ToPointer();
            return new CastbarInfo
            {
                unitBase = unitbase,
                gauge = (AtkImageNode*) unitbase->ULDData.NodeList[FocusTargetInfoGaugeNodeIndex],
                bg = (AtkImageNode*) unitbase->ULDData.NodeList[FocusTargetInfoGaugeBgNodeIndex]
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

        private string GetActorName(int id)
        {
            foreach (Actor t in pi.ClientState.Actors)
                if (t != null)
                    if (id == t.ActorId)
                        return t.Name;
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
        
        private delegate IntPtr CreateFlyTextDelegate(IntPtr flyTextMgr,
            UInt32 kind, UInt32 val1, UInt32 val2,
            IntPtr text1, UInt32 color, UInt32 icon, IntPtr text2, float unk3);

        private IntPtr CreateFlyText(
            IntPtr flyTextMgr, // or something
            UInt32 kind,
            UInt32 val1,
            UInt32 val2,
            IntPtr text1,
            UInt32 color,
            UInt32 icon,
            IntPtr text2,
            float unk3
        )
        {
            uint tColor = color;
            uint tVal1 = val1;
            IntPtr tText2 = text2;

            if (Randomize)
            {
                int ttVal1 = ModifyDamageALittle((int) val1);
                tVal1 = (uint) ttVal1;
            }

            try
            {
                if (Hijack)
                {
                    string hjText1 = Marshal.PtrToStringAnsi(HijackStruct.text1);
                    string hjText2 = Marshal.PtrToStringAnsi(HijackStruct.text2);

                    FlyTextLog(
                        $"flytext hijacked: kind: {HijackStruct.kind}, val1: {HijackStruct.val1}, val2: {HijackStruct.val2}, color: {HijackStruct.color:X}, icon: {HijackStruct.icon}");
                    FlyTextLog($"text1: {hjText1} | text2: {hjText2}");

                    return createFlyTextHook.Original(flyTextMgr, HijackStruct.kind, HijackStruct.val1,
                        HijackStruct.val2, HijackStruct.text1, HijackStruct.color, HijackStruct.icon,
                        HijackStruct.text2, unk3);
                }

                FlyTextKind ftKind = (FlyTextKind) kind;

                // wrap this here to lower overhead when not logging
                if (configuration.FlyTextLogEnabled)
                {
                    string strText1 = Marshal.PtrToStringAnsi(text1);
                    string strText2 = Marshal.PtrToStringAnsi(text2);

                    strText1 = strText1?.Replace("%", "%%");
                    strText2 = strText2?.Replace("%", "%%");

                    FlyTextLog(
                        $"flytext created: kind: {ftKind}, val1: {tVal1}, val2: {val2}, color: {color:X}, icon: {icon}");
                    FlyTextLog($"text1: {strText1} | text2: {strText2}");
                }

                if (TryGetFlyTextDamageType(val1, out DamageType dmgType, out int sourceId))
                {
                    int charaId = GetCharacterActorId();
                    int petId = FindCharaPet();

                    if (configuration.OutgoingColorEnabled || configuration.IncomingColorEnabled)
                    {
                        // sourceId == GetCharacterActorId() && configuration.OutgoingColorEnabled || (sourceId != GetCharacterActorId() && sourceId != FindCharaPet() && configuration.IncomingColorEnabled)
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
                                        tColor = ImGui.GetColorU32(configuration.PhysicalColor);
                                        break;
                                    case DamageType.Magic:
                                        tColor = ImGui.GetColorU32(configuration.MagicColor);
                                        break;
                                    case DamageType.Darkness:
                                        tColor = ImGui.GetColorU32(configuration.DarknessColor);
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
                        {
                            text1 = GetNewTextPtr(sourceId, text1);
                            if (text1 != IntPtr.Zero)
                                text.Enqueue(new Tuple<IntPtr, long>(text1, Ms()));
                        }
                    }

                    // Attack text checks
                    if ((sourceId != charaId && sourceId != petId && !configuration.IncomingAttackTextEnabled) ||
                        (sourceId == charaId && !configuration.OutgoingAttackTextEnabled) ||
                        (sourceId == petId && !configuration.PetAttackTextEnabled))
                        tText2 = blankText;
                }
            }
            catch (Exception e)
            {
                PluginLog.Log($"{e.Message} {e.StackTrace}");
            }

            return createFlyTextHook.Original(flyTextMgr, kind, tVal1, val2, text1, tColor, icon, tText2, unk3);
        }

        private IntPtr GetNewTextPtr(int sourceId, IntPtr originalText)
        {
            IntPtr ret = IntPtr.Zero;
            string name = GetActorName(sourceId);

            if (string.IsNullOrEmpty(name)) return ret;

            var newText = new List<byte>();
            
            switch (pi.ClientState.ClientLanguage)
            {
                case ClientLanguage.Japanese:
                    newText.AddRange(Encoding.Default.GetBytes(name));
                    newText.AddRange(Encoding.UTF8.GetBytes("から"));
                    break;
                case ClientLanguage.English:
                    newText.AddRange(Encoding.Default.GetBytes($"from {name}"));
                    break;
                case ClientLanguage.German:
                    newText.AddRange(Encoding.Default.GetBytes($"von {name}"));
                    break;
                case ClientLanguage.French:
                    newText.AddRange(Encoding.Default.GetBytes($"de {name}"));
                    break;
                default:
                    newText.AddRange(Encoding.Default.GetBytes($">{name}"));
                    break;
            }
            
            if (originalText != IntPtr.Zero)
                newText.AddRange(Encoding.Default.GetBytes($" {Marshal.PtrToStringAnsi(originalText)}"));

            newText.Add(0);
            ret = Marshal.AllocHGlobal(newText.Count);
            Marshal.Copy(newText.ToArray(), 0, ret, newText.Count);

            return ret;
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
            if (!futureFlyText.TryGetValue(dmg, out var list)) return false;

            if (list.Count == 0)
                return false;

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

            FlyTextLog($"pre-cleanup flytext: {futureFlyText.Values.Count}");
            FlyTextLog($"pre-cleanup text: {text.Count}");
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

            while (text.TryPeek(out var tup) && ms - tup?.Item2 >= 5000)
            {
                text.TryDequeue(out var newTup);
                Marshal.FreeHGlobal(newTup.Item1);
            }

            FlyTextLog($"post-cleanup flytext: {futureFlyText.Values.Count}");
            FlyTextLog($"post-cleanup text: {text.Count}");
        }

        public void ClearTextPtrs()
        {
            while (text.TryDequeue(out var tup))
                Marshal.FreeHGlobal(tup.Item1);
        }

        public void ClearFlyTextQueue()
        {
            if (futureFlyText == null) return;

            FlyTextLog($"clearing flytext queue of {futureFlyText.Values.Count} items...");
            futureFlyText.Clear();
        }

        private int ModifyDamageALittle(int originalDamage)
        {
            var margin = (int) Math.Truncate(originalDamage * 0.1);
            var rand = new Random();
            var newDamage = rand.Next(originalDamage - margin, originalDamage + margin);
            PluginLog.Log($"og dmg: {originalDamage}, new dmg: {newDamage}");
            return newDamage;
        }
    }
}