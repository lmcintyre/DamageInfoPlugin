using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.ClientState.Actors.Types.NonPlayer;
using Dalamud.Hooking;
using ImGuiNET;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DamageInfoPlugin
{
    public class DamageInfoPlugin : IDalamudPlugin
    {
        // when a flytext 
        private const int CleanupInterval = 10000;

        private const int ActorCastOffset = 6884;
        private const int AtkResNodeGaugeColorOffset = 112;
        
        private const int TargetInfoGaugeOffset = 688;
        private const int TargetInfoGaugeBgOffset = 696;
        
        private const int TargetInfoSplitGaugeOffset = 568;
        private const int TargetInfoSplitGaugeBgOffset = 576;
        
        private const int FocusTargetInfoGaugeParentOffset = 568;
        private const int FocusTargetInfoGaugeOffsetFromParent = 56;
        private const int FocusTargetInfoGaugeShadowOffset = 584;
        
        public string Name => "Damage Info";

        private const string commandName = "/dmginfo";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;

        public bool Hijack { get; set; }
        public bool Randomize { get; set; }
        public HijackStruct hijackStruct { get; set; }

        private Hook<CreateFlyTextDelegate> createFlyTextHook;
        private Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;
        
        private Hook<SetCastBarDelegate> setCastBarHook;
        private Hook<SetCastBarDelegate> setFocusTargetCastBarHook;

        private Dictionary<uint, DamageType> actionToDamageTypeDict;
        private HashSet<uint> ignoredCastActions;
        private ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>> futureFlyText;
        private ConcurrentQueue<Tuple<IntPtr, long>> text;
        private long lastCleanup;

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

            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
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

                throw;
            }

            createFlyTextHook.Enable();
            receiveActionEffectHook.Enable();
            setCastBarHook.Enable();
            setFocusTargetCastBarHook.Enable();

            pi.UiBuilder.OnBuildUi += DrawUI;
            pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();
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
            pi.CommandManager.RemoveHandler(commandName);
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

        public ushort GetCurrentCast(IntPtr actor)
        {
            return (ushort) Marshal.ReadInt16(actor, ActorCastOffset);
        }
        
        public (IntPtr, IntPtr, IntPtr) GetTargetInfoUiElementAddresses()
        {
            IntPtr targetInfoCastBar = pi.Framework.Gui.GetUiObjectByName("_TargetInfo", 1);
            if (targetInfoCastBar == IntPtr.Zero) return (IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IntPtr targetInfoCastBarGauge = Marshal.ReadIntPtr(targetInfoCastBar, TargetInfoGaugeOffset);
            IntPtr targetInfoCastBarGaugeBg = Marshal.ReadIntPtr(targetInfoCastBar, TargetInfoGaugeBgOffset);

            return (targetInfoCastBar, targetInfoCastBarGauge, targetInfoCastBarGaugeBg);
        }

        public (IntPtr, IntPtr, IntPtr) GetTargetInfoSplitUiElementAddresses()
        {
            IntPtr targetInfoSplitCastBar = pi.Framework.Gui.GetUiObjectByName("_TargetInfoCastBar", 1);
            if (targetInfoSplitCastBar == IntPtr.Zero) return (IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IntPtr targetInfoSplitCastBarGauge = Marshal.ReadIntPtr(targetInfoSplitCastBar, TargetInfoSplitGaugeOffset);
            IntPtr targetInfoSplitCastBarGaugeBg = Marshal.ReadIntPtr(targetInfoSplitCastBar, TargetInfoSplitGaugeBgOffset);

            return (targetInfoSplitCastBar, targetInfoSplitCastBarGauge, targetInfoSplitCastBarGaugeBg);
        }
        
        public (IntPtr, IntPtr, IntPtr) GetFocusTargetUiElementAddresses()
        {
            IntPtr focusTargetInfo = pi.Framework.Gui.GetUiObjectByName("_FocusTargetInfo", 1);
            if (focusTargetInfo == IntPtr.Zero) return (IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IntPtr focusTargetInfoCastBarGaugeBgParentPtr = Marshal.ReadIntPtr(focusTargetInfo, FocusTargetInfoGaugeParentOffset);
            IntPtr focusTargetInfoCastBarGaugeBg = Marshal.ReadIntPtr(focusTargetInfoCastBarGaugeBgParentPtr, FocusTargetInfoGaugeOffsetFromParent);
            IntPtr focusTargetInfoCastBarGauge = Marshal.ReadIntPtr(focusTargetInfo, FocusTargetInfoGaugeShadowOffset);

            return (focusTargetInfo, focusTargetInfoCastBarGauge, focusTargetInfoCastBarGaugeBg);
        }

        public bool IsCharacterPet(int suspectedPet)
        {
            int charaId = GetCharacterActorId();
            foreach (Actor a in pi.ClientState.Actors)
            {
                if (!(a is BattleNpc npc)) continue;

                IntPtr actPtr = npc.Address;
                if (actPtr == IntPtr.Zero) continue;

                if (npc.ActorId != suspectedPet)
                    continue;

                if (npc.OwnerId == charaId)
                    return true;
            }

            return false;
        }

        public int FindCharaPet()
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

        public int GetCharacterActorId()
        {
            if (pi.ClientState.LocalPlayer != null)
                return pi.ClientState.LocalPlayer.ActorId;
            return 0;
        }

        public string GetActorName(int id)
        {
            foreach (Actor t in pi.ClientState.Actors)
                if (t != null)
                    if (id == t.ActorId)
                        return t.Name;
            return "";
        }

        public void ResetMainTargetCastBar()
        {
            IntPtr targetInfoCastBar, targetInfoCastBarGauge, targetInfoCastBarGaugeBg;
            (targetInfoCastBar, targetInfoCastBarGauge, targetInfoCastBarGaugeBg) = GetTargetInfoUiElementAddresses();
            IntPtr targetInfoSplitCastBar, targetInfoSplitCastBarGauge, targetInfoSplitCastBarGaugeBg;
            (targetInfoSplitCastBar, targetInfoSplitCastBarGauge, targetInfoSplitCastBarGaugeBg) = GetTargetInfoSplitUiElementAddresses();
            
            int white = -1;

            if (targetInfoCastBar != IntPtr.Zero && targetInfoCastBarGauge != IntPtr.Zero && targetInfoCastBarGaugeBg != IntPtr.Zero)
            {
                IntPtr gaugeColorPtr = IntPtr.Add(targetInfoCastBarGauge, AtkResNodeGaugeColorOffset);
                IntPtr gaugeBgColorPtr = IntPtr.Add(targetInfoCastBarGaugeBg, AtkResNodeGaugeColorOffset);
                Marshal.WriteInt32(gaugeColorPtr, white);
                Marshal.WriteInt32(gaugeBgColorPtr, white);
            }

            if (targetInfoSplitCastBar != IntPtr.Zero && targetInfoSplitCastBarGauge != IntPtr.Zero && targetInfoSplitCastBarGaugeBg != IntPtr.Zero)
            {
                IntPtr splitGaugeColorPtr = IntPtr.Add(targetInfoSplitCastBarGauge, AtkResNodeGaugeColorOffset);
                IntPtr splitGaugeBgColorPtr = IntPtr.Add(targetInfoSplitCastBarGaugeBg, AtkResNodeGaugeColorOffset);
                Marshal.WriteInt32(splitGaugeColorPtr, white);
                Marshal.WriteInt32(splitGaugeBgColorPtr, white);
            }
        }

        public void ResetFocusTargetCastBar()
        {
            IntPtr ftInfoCastBar, ftInfoCastBarGauge, ftInfoCastBarGaugeBg;
            (ftInfoCastBar, ftInfoCastBarGauge, ftInfoCastBarGaugeBg) = GetFocusTargetUiElementAddresses();

            int white = -1;

            if (ftInfoCastBar != IntPtr.Zero && ftInfoCastBarGauge != IntPtr.Zero && ftInfoCastBarGaugeBg != IntPtr.Zero)
            {
                IntPtr gaugeColorPtr = IntPtr.Add(ftInfoCastBarGauge, AtkResNodeGaugeColorOffset);
                IntPtr gaugeBgColorPtr = IntPtr.Add(ftInfoCastBarGaugeBg, AtkResNodeGaugeColorOffset);
                Marshal.WriteInt32(gaugeColorPtr, white);
                Marshal.WriteInt32(gaugeBgColorPtr, white);    
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
            
            IntPtr targetInfoCastBar, targetInfoCastBarGauge, targetInfoCastBarGaugeBg;
            (targetInfoCastBar, targetInfoCastBarGauge, targetInfoCastBarGaugeBg) = GetTargetInfoUiElementAddresses();
            
            IntPtr targetInfoSplitCastBar, targetInfoSplitCastBarGauge, targetInfoSplitCastBarGaugeBg;
            (targetInfoSplitCastBar, targetInfoSplitCastBarGauge, targetInfoSplitCastBarGaugeBg) = GetTargetInfoSplitUiElementAddresses();

            bool combinedInvalid = targetInfoCastBar == IntPtr.Zero || targetInfoCastBarGauge == IntPtr.Zero || targetInfoCastBarGaugeBg == IntPtr.Zero;
            bool splitInvalid = targetInfoSplitCastBar == IntPtr.Zero || targetInfoSplitCastBarGauge == IntPtr.Zero || targetInfoSplitCastBarGaugeBg == IntPtr.Zero;
            
            if (combinedInvalid && splitInvalid)
            {
                setCastBarHook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }

            if (thisPtr == targetInfoCastBar && !combinedInvalid)
            {
                IntPtr? mainTarget = pi.ClientState?.Targets?.CurrentTarget?.Address;
                ColorCastBar(mainTarget, targetInfoCastBarGauge, targetInfoCastBarGaugeBg, setCastBarHook,
                    thisPtr, a2, a3, a4, a5);
            }
            else if (thisPtr == targetInfoSplitCastBar && !splitInvalid)
            {
                IntPtr? mainTarget = pi.ClientState?.Targets?.CurrentTarget?.Address;
                ColorCastBar(mainTarget, targetInfoSplitCastBarGauge, targetInfoSplitCastBarGaugeBg, setCastBarHook,
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
            
            IntPtr ftInfoCastBar, ftInfoCastBarGauge, ftInfoCastBarGaugeBg;
            (ftInfoCastBar, ftInfoCastBarGauge, ftInfoCastBarGaugeBg) = GetFocusTargetUiElementAddresses();
            
            bool focusTargetInvalid = ftInfoCastBar == IntPtr.Zero || ftInfoCastBarGauge == IntPtr.Zero || ftInfoCastBarGaugeBg == IntPtr.Zero; 
            
            if (thisPtr == ftInfoCastBar && !focusTargetInvalid)
            {
                IntPtr? focusTarget = pi.ClientState?.Targets?.FocusTarget?.Address;
                ColorCastBar(focusTarget, ftInfoCastBarGauge, ftInfoCastBarGaugeBg, setFocusTargetCastBarHook,
                                thisPtr, a2, a3, a4, a5);
            }
        }
        
        private void ColorCastBar(IntPtr? target, IntPtr gauge, IntPtr bg, Hook<SetCastBarDelegate> hook,
            IntPtr thisPtr, IntPtr a2, IntPtr a3, IntPtr a4, char a5)
        {
            if (!target.HasValue || target.Value == IntPtr.Zero)
            {
                hook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }
            
            ushort actionId = GetCurrentCast(target.Value);
                    
            IntPtr gaugeColorPtr = IntPtr.Add(gauge, AtkResNodeGaugeColorOffset);
            IntPtr gaugeBgColorPtr = IntPtr.Add(bg, AtkResNodeGaugeColorOffset);

            actionToDamageTypeDict.TryGetValue(actionId, out DamageType type);
            if (ignoredCastActions.Contains(actionId))
            {
                Marshal.WriteInt32(gaugeColorPtr, -1);
                Marshal.WriteInt32(gaugeBgColorPtr, -1);
                hook.Original(thisPtr, a2, a3, a4, a5);
                return;
            }
                    
            uint newCastColor = type switch
            {
                DamageType.Physical => ImGui.GetColorU32(configuration.PhysicalCastColor),
                DamageType.Magic => ImGui.GetColorU32(configuration.MagicCastColor),
                DamageType.Darkness => ImGui.GetColorU32(configuration.DarknessCastColor),
                _ => uint.MaxValue
            };
                    
            uint newBgColor = type switch
            {
                DamageType.Physical => ImGui.GetColorU32(configuration.PhysicalBgColor),
                DamageType.Magic => ImGui.GetColorU32(configuration.MagicBgColor),
                DamageType.Darkness => ImGui.GetColorU32(configuration.DarknessBgColor),
                _ => uint.MaxValue
            };
                    
            Marshal.WriteInt32(gaugeColorPtr, (int) newCastColor);
            Marshal.WriteInt32(gaugeBgColorPtr, (int) newBgColor);
            hook.Original(thisPtr, a2, a3, a4, a5);
        }
        
        public unsafe delegate IntPtr CreateFlyTextDelegate(IntPtr flyTextMgr,
            UInt32 kind, UInt32 val1, UInt32 val2,
            IntPtr text1, UInt32 color, UInt32 icon, IntPtr text2, float unk3);

        public unsafe IntPtr CreateFlyText(
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

            if (Randomize)
            {
                int ttVal1 = ModifyDamageALittle((int) val1);
                tVal1 = (uint) ttVal1;
            }

            try
            {
                if (Hijack)
                {
                    string hjText1 = Marshal.PtrToStringAnsi(hijackStruct.text1);
                    string hjText2 = Marshal.PtrToStringAnsi(hijackStruct.text2);

                    FlyTextLog(
                        $"flytext hijacked: kind: {hijackStruct.kind}, val1: {hijackStruct.val1}, val2: {hijackStruct.val2}, color: {hijackStruct.color:X}, icon: {hijackStruct.icon}");
                    FlyTextLog($"text1: {hjText1} | text2: {hjText2}");

                    return createFlyTextHook.Original(flyTextMgr, hijackStruct.kind, hijackStruct.val1,
                        hijackStruct.val2, hijackStruct.text1, hijackStruct.color, hijackStruct.icon,
                        hijackStruct.text2, unk3);
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
                            string name = GetActorName(sourceId);

                            if (!string.IsNullOrEmpty(name))
                            {
                                string existingText = "";
                                if (text1 != IntPtr.Zero)
                                    existingText = Marshal.PtrToStringAnsi(text1);

                                string combined = $"from {name} {existingText}";
                                text1 = Marshal.StringToHGlobalAnsi(combined);
                                text.Enqueue(new Tuple<IntPtr, long>(text1, Ms()));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                PluginLog.Log($"{e.Message} {e.StackTrace}");
            }

            return createFlyTextHook.Original(flyTextMgr, kind, tVal1, val2, text1, tColor, icon, text2, unk3);
        }

        public delegate void ReceiveActionEffectDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos,
            IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);

        public unsafe void ReceiveActionEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos,
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

        // private unsafe void LogFromPtr(IntPtr ptr, int count)
        // {
        //     if (ptr == IntPtr.Zero)
        //     {
        //         EffectLog("dump{0}: null");
        //         return;
        //     }
        //
        //     StringBuilder sb = new StringBuilder();
        //     for (int i = 0; i < count / 512 + 1; i++)
        //     {
        //         var bytesLeft = count - i * 512;
        //         var theseBytes = bytesLeft > 512 ? 512 : bytesLeft;
        //         for (int j = 0; j < theseBytes; j++)
        //             sb.Append($"{*((byte*) ptr.ToPointer() + (i * 512) + j):X2} ");
        //         EffectLog($"dump{i}: {sb}");
        //         sb.Clear();
        //     }
        // }

        // private unsafe void WriteNextEight(IntPtr position) {
        //  string write = "";
        //  for (int i = 0; i < 8; i++)
        //   write += $"{*((byte*)position + i):X2} ";
        //  EffectLog(write);
        // }

        //      private void CreateMessageLog(string str) {
        // PluginLog.Log($"[createmessage] {str}");
        //      }

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