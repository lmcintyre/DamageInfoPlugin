using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Actor = Dalamud.Game.ClientState.Structs.Actor;

namespace DamageInfoPlugin
{
	public enum ActionEffectType : byte
	{
		Nothing = 0,
		Miss = 1,
		FullResist = 2,
		Damage = 3,
		Heal = 4,
		BlockedDamage = 5,
		ParriedDamage = 6,
		Invulnerable = 7,
		NoEffectText = 8,
		Unknown_0 = 9,
		MpLoss = 10,
		MpGain = 11,
		TpLoss = 12,
		TpGain = 13,
		GpGain = 14,
		ApplyStatusEffectTarget = 15,
		ApplyStatusEffectSource = 16,
		StatusNoEffect = 20,
		StartActionCombo = 27,
		ComboSucceed = 28,
		Knockback = 33,
		Mount = 40,
		VFX = 59,
	};
 
    // members suffixed with a number seem to be a duplicate
    public enum FlyTextKind {
        // val1 in serif font, text1 sans-serif as subtitle
        AutoAttack, // used for autoas and incoming DoT damage
 
        // val1 in serif font, text1 sans-serif as subtitle
        // does a bounce effect on appearance
        DirectHit,
 
        // val1 in larger serif font with exclamation, text1 sans-serif as subtitle
        // does a bigger bounce effect on appearance
        CriticalHit,
 
        // val1 in even larger serif font with 2 exclamations, text1 sans-serif as subtitle
        // does a large bounce effect on appearance
        // does not scroll
        CriticalDirectHit,
 
        // AutoAttack with sans-serif text2 to the left of the val1
        NamedAttack,
 
        // DirectHit with sans-serif text2 to the left of the val1
        NamedDirectHit,
 
        // CriticalHit with sans-serif text2 to the left of the val1
        NamedCriticalHit,
 
        // CriticalDirectHit with sans-serif text2 to the left of the val1
        NamedCriticalDirectHit,
 
        // all caps serif MISS
        Miss,
 
        // sans-serif text2 next to all caps serif MISS
        NamedMiss,
 
        // all caps serif DODGE
        Dodge,
 
        // sans-serif text2 next to all caps serif DODGE
        NamedDodge,
 
        // icon next to sans-serif text2
        NamedIcon,
        NamedIcon2,
 
        // serif val1 with all caps condensed font EXP with text1 sans-serif as subtitle
        Exp,
 
        // sans-serif text2 next to serif val1 with all caps condensed font MP with text1 sans-serif as subtitle
        NamedMP,
 
        // sans-serif text2 next to serif val1 with all caps condensed font TP with text1 sans-serif as subtitle
        NamedTP,
 
        NamedAttack2,   // used on HoTs, heals
        NamedMP2,
        NamedTP2,
 
        // sans-serif text2 next to serif val1 with all caps condensed font EP with text1 sans-serif as subtitle
        NamedEP,
 
        // displays nothing
        None,
 
        // all caps serif INVULNERABLE
        Invulnerable,
 
        // all caps sans-serif condensed font INTERRUPTED!
        // does a bounce effect on appearance
        // does not scroll
        Interrupted,
 
        // AutoAttack with no text1
        AutoAttackNoText,
        AutoAttackNoText2,
        CriticalHit2,
        AutoAttackNoText3,
        NamedCriticalHit2,
 
        // same as NamedCriticalHit with a green (cannot change) MP in condensed font to the right of val1
        // does a jiggle effect to the right on appearance
        NamedCriticalHitWithMP,
 
        // same as NamedCriticalHit with a yellow (cannot change) TP in condensed font to the right of val1
        // does a jiggle effect to the right on appearance
        NamedCriticalHitWithTP,
 
        // same as NamedIcon with sans-serif "has no effect!" to the right
        NamedIconHasNoEffect,
 
        // same as NamedIcon but text2 is slightly faded
        // used for buff expiry
        NamedIconFaded,
        NamedIconFaded2,
 
        // sans-serif text2
        Named,
 
        // same as NamedIcon with sans-serif "(fully resisted)" to the right
        NamedIconFullyResisted,
 
        // all caps serif INCAPACITATED!
        Incapacitated,
 
        // text2 with sans-serif "(fully resisted)" to the right
        NamedFullyResisted,
 
        // text2 with sans-serif "has no effect!" to the right
        NamedHasNoEffect,
 
        NamedAttack3,
        NamedMP3,
        NamedTP3,
 
        // same as NamedIcon with serif "INVULNERABLE!" beneath the text2
        NamedIconInvulnerable,
 
        // all caps serif RESIST
        Resist,
 
        // same as NamedIcon but places the given icon in the item icon outline
        NamedIconWithItemOutline,
 
        AutoAttackNoText4,
        CriticalHit3,
 
        // all caps serif REFLECT
        Reflect,
 
        // all caps serif REFLECTED
        Reflected,
 
        DirectHit2,
        CriticalHit5,
        CriticalDirectHit2,
	}
 
    public enum DamageType {
		Unknown = 0,
		Slashing = 1,
		Piercing = 2,
		Blunt = 3,
		Magic = 5,
		Darkness = 6,
		Physical = 7,
		LimitBreak = 8,
    }
 
	public struct HijackStruct {
		public uint kind;
		public uint val1;
		public uint val2;
		public uint icon;
		public uint color;
		public IntPtr text1;
		public IntPtr text2;
		public float unk3;
	}
    
	public struct EffectEntry
	{
		public ActionEffectType type;
		public byte param0;
		public byte param1;
		public byte param2;
		public byte mult;
		public byte flags;
		public ushort value;
 
		public override string ToString() {
			return
				$"Type: {type}, p0: {param0}, p1: {param1}, p2: {param2}, mult: {mult}, flags: {flags} | {Convert.ToString(flags, 2)}, value: {value}";
		}
	}

    public class DamageInfoPlugin : IDalamudPlugin {

        // when a flytext 
	    private const int CleanupInterval = 10000;
	    public string Name => "Damage Info";

        private const string commandName = "/dmginfo";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;

        public bool Hijack { get; set; }
        public HijackStruct hijackStruct { get; set; }
        
        private Hook<CreateFlyTextDelegate> createFlyTextHook;
        private Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;
        
        private Dictionary<uint, DamageType> actionToDamageTypeDict;
        private ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>> futureFlyText;
        private ConcurrentQueue<Tuple<IntPtr, long>> text;
        private long lastCleanup;

        public void Initialize(DalamudPluginInterface pluginInterface) {
	        lastCleanup = Ms();
            actionToDamageTypeDict = new Dictionary<uint, DamageType>();
	        futureFlyText = new ConcurrentDictionary<uint, List<Tuple<long, DamageType, int>>>();
            text = new ConcurrentQueue<Tuple<IntPtr, long>>();

            pi = pluginInterface;

            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi, this);
            ui = new PluginUI(configuration, this);

            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
	            { HelpMessage = "Display the Damage Info configuration interface."});

            var actionSheet = pi.Data.GetExcelSheet<Action>();
            foreach (var row in actionSheet.ToList()) {
	            DamageType tmpType = (DamageType) row.AttackType.Row;
	            if (tmpType != DamageType.Magic && tmpType != DamageType.Darkness)
		            tmpType = DamageType.Physical;
            
                actionToDamageTypeDict.Add(row.RowId, tmpType);
            }
            
            IntPtr createFlyTextFuncPtr = pi.TargetModuleScanner.ScanText(
	            "48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 40 48 63 FA 45 8B F0 48 8B F1 83 FF 34 7C 13 33 C0 48 8B 74 24 ?? 48 8B 7C 24 ?? 48 83 C4 40 41 5E C3");
            createFlyTextHook = new Hook<CreateFlyTextDelegate>(createFlyTextFuncPtr, (CreateFlyTextDelegate) CreateFlyText);
            createFlyTextHook.Enable();
            
            IntPtr receiveActionEffectFuncPtr = pi.TargetModuleScanner.ScanText(
	            "4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9");
            receiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr, (ReceiveActionEffectDelegate) ReceiveActionEffect);
            receiveActionEffectHook.Enable();

            pi.UiBuilder.OnBuildUi += DrawUI;
            pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();
        }

        public void Dispose() {
            ClearFlyTextQueue();
            createFlyTextHook.Disable();
            createFlyTextHook.Dispose();
            receiveActionEffectHook.Disable();
            receiveActionEffectHook.Dispose();

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

        public ulong GetCharacterActorId() {
	        return (ulong) pi.ClientState.LocalPlayer.ActorId;
        }

        public string GetActorName(int id) {
	        foreach (Dalamud.Game.ClientState.Actors.Types.Actor t in pi.ClientState.Actors)
		        if (id == t.ActorId)
			        return t.Name;
	        return "";
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
        ) {
			uint tColor = color;
   
	        if (Hijack)
	        {
		        string hjText1 = Marshal.PtrToStringAnsi(hijackStruct.text1);
		        string hjText2 = Marshal.PtrToStringAnsi(hijackStruct.text2);
   
		        FlyTextLog($"flytext hijacked: kind: {hijackStruct.kind}, val1: {hijackStruct.val1}, val2: {hijackStruct.val2}, color: {hijackStruct.color:X}, icon: {hijackStruct.icon}");
		        FlyTextLog($"text1: {hjText1} | text2: {hjText2}");
   
		        return createFlyTextHook.Original(flyTextMgr, hijackStruct.kind, hijackStruct.val1, hijackStruct.val2, hijackStruct.text1, hijackStruct.color, hijackStruct.icon, hijackStruct.text2, unk3);
	        }
   
	        FlyTextKind ftKind = (FlyTextKind)kind;
   
	        // wrap this here to lower overhead when not logging
	        if (configuration.FlyTextLogEnabled)
	        {
		        string strText1 = Marshal.PtrToStringAnsi(text1);
		        string strText2 = Marshal.PtrToStringAnsi(text2);
   
		        strText1 = strText1?.Replace("%", "%%");
		        strText2 = strText2?.Replace("%", "%%");
                
                FlyTextLog($"flytext created: kind: {ftKind}, val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}");
		        FlyTextLog($"text1: {strText1} | text2: {strText2}");
	        }

	        if (TryGetFlyTextDamageType(val1, out DamageType dmgType, out int sourceId)) {
		        if (configuration.TextColoringEnabled)
		        {
			        if (ftKind == FlyTextKind.AutoAttack
			            || ftKind == FlyTextKind.CriticalHit
			            || ftKind == FlyTextKind.DirectHit
			            || ftKind == FlyTextKind.CriticalDirectHit
			            || ftKind == FlyTextKind.NamedAttack
			            || ftKind == FlyTextKind.NamedDirectHit
			            || ftKind == FlyTextKind.NamedCriticalHit
			            || ftKind == FlyTextKind.NamedCriticalDirectHit) {
				        switch (dmgType) {
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

		        if (configuration.SourceTextEnabled && sourceId != (int) GetCharacterActorId()) {
			        string name = GetActorName(sourceId);
			        if (!string.IsNullOrEmpty(name)) {
				        string existingText = "";
				        if (text1 != IntPtr.Zero)
					        existingText = Marshal.PtrToStringAnsi(text1);

				        string combined = $"from {name} {existingText}";
				        text1 = Marshal.StringToHGlobalAnsi(combined);
                        text.Enqueue(new Tuple<IntPtr, long>(text1, Ms()));
			        }
		        }
            }

	        return createFlyTextHook.Original(flyTextMgr, kind, val1, val2, text1, tColor, icon, text2, unk3);
        }
   
        public unsafe delegate void ReceiveActionEffectDelegate(ulong sourceId, IntPtr unk2, IntPtr unk3, IntPtr packet, IntPtr unk4, IntPtr unk5, IntPtr unk6);
   
        public unsafe void ReceiveActionEffect(ulong sourceId, IntPtr sourceCharacter, IntPtr unk3,
										        IntPtr packet,
										        IntPtr unk4, IntPtr unk5, IntPtr unk6) {
	        Cleanup();
            // no log, no processing... just get him outta here
            if ((!configuration.EffectLogEnabled && !configuration.TextColoringEnabled)){
		        receiveActionEffectHook.Original(sourceId, sourceCharacter, unk3, packet, unk4, unk5, unk6);
		        return;
	        }

#if DEBUG
            EffectLog($"p1: {sourceId}");
            EffectLog($"p2: {sourceCharacter.ToInt64():X}");
            Actor test = Marshal.PtrToStructure<Actor>(sourceCharacter);
            EffectLog($"source: {test.Name}");
            EffectLog($"p3: {unk3.ToInt64():X}");
            EffectLog($"p4: {unk4.ToInt64():X}");
            EffectLog($"p5: {unk5.ToInt64():X}");
            EffectLog($"p6: {unk6.ToInt64():X}");
            EffectLog($"packet at {packet.ToInt64():X}");
#endif

			uint id = *((uint*)packet.ToPointer() + 0x2);
			ushort op = *((ushort*) packet.ToPointer() - 0x7);
			byte targetCount = *(byte*) (packet + 0x21);
            EffectLog($"--- source actor: {sourceId}, action id {id}, opcode: {op:X} numTargets: {targetCount} ---");
   
            IntPtr effectsPtr = packet + 0x2A;

            int effectsEntries = 0;
            int targetEntries = 1;
            if (targetCount == 0) {
	            effectsEntries = 0;
	            targetEntries = 1;
            } else if (targetCount == 1) {
	            effectsEntries = 8;
	            targetEntries = 1;
            } else if (targetCount <= 8) {
	            effectsEntries = 64;
	            targetEntries = 8;
            } else if (targetCount <= 16) {
	            effectsEntries = 128;
	            targetEntries = 16;
            } else if (targetCount <= 24) {
	            effectsEntries = 192;
	            targetEntries = 24;
            } else if (targetCount <= 32) {
	            effectsEntries = 256;
	            targetEntries = 32;
            }

            List<EffectEntry> entries = new List<EffectEntry>(effectsEntries);
   
            for (int i = 0; i < effectsEntries; i++) {
                entries.Add(*(EffectEntry*)effectsPtr);
                effectsPtr += 8;
            }
            
            effectsPtr += 6;
            ulong[] targets = new ulong[targetEntries];
			
            for (int i = 0; i < targetCount; i++) {
	            targets[i] = *(ulong*) effectsPtr;
                effectsPtr += 8;
            }

            for (int i = 0; i < entries.Count; i++) {
		        ulong tTarget = targets[i / 8];
		        uint tDmg = entries[i].value;
	            if (entries[i].mult != 0)
		            tDmg += (uint) ushort.MaxValue * entries[i].mult;

	            if (entries[i].type == ActionEffectType.Damage
	                || entries[i].type == ActionEffectType.BlockedDamage
	                || entries[i].type == ActionEffectType.ParriedDamage
                    // || entries[i].type == ActionEffectType.Miss
				) {
		            EffectLog($"{entries[i]}, s: {sourceId} t: {tTarget}");

		            if (configuration.TextColoringEnabled
		                && (sourceId == GetCharacterActorId() || tTarget == GetCharacterActorId())
		                && tDmg != 0)
			            AddToFutureFlyText(tDmg, actionToDamageTypeDict[id], (int) sourceId);
                }
            }
            receiveActionEffectHook.Original(sourceId, sourceCharacter, unk3, packet, unk4, unk5, unk6);
        }

        private unsafe void WriteNextEight(IntPtr position) {
	        string write = "";
	        for (int i = 0; i < 8; i++)
		        write += $"{*((byte*)position + i):X2} ";
	        EffectLog(write);
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
   
        private bool TryGetFlyTextDamageType(uint dmg, out DamageType type, out int sourceId) {
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

        private long Ms() {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        }
   
        private void AddToFutureFlyText(uint dmg, DamageType type, int sourceId) {
	        long ms = Ms();
	        var toInsert = new Tuple<long, DamageType, int>(ms, type, sourceId);
   
	        if (futureFlyText.TryGetValue(dmg, out var list)) {
		        if (list != null) {
			        list.Add(toInsert);
			        return;
		        }
	        }
   
	        var tmpList = new List<Tuple<long, DamageType, int>> {toInsert};
	        futureFlyText[dmg] = tmpList;
        }
   
		// Not all effect packets end up being flytext
		// so we have to clean up the orphaned entries here
        private void Cleanup() {
	        if (futureFlyText == null) return;

	        long ms = Ms();
	        if (ms - lastCleanup < CleanupInterval) return;

            FlyTextLog($"pre-cleanup flytext: {futureFlyText.Values.Count}");
            FlyTextLog($"pre-cleanup text: {text.Count}");
            lastCleanup = ms;

            var toRemove = new List<uint>();

            foreach (uint key in futureFlyText.Keys) {
	            if (!futureFlyText.TryGetValue(key, out var list)) continue;
	            if (list == null) {
		            toRemove.Add(key);
		            continue;
                }

	            for (int i = 0; i < list.Count; i++) {
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

            while (text.TryPeek(out var tup) && ms - tup?.Item2 >= 5000) {
	            text.TryDequeue(out var newTup);
	            Marshal.FreeHGlobal(newTup.Item1);
            }

	        FlyTextLog($"post-cleanup flytext: {futureFlyText.Values.Count}");
	        FlyTextLog($"post-cleanup text: {text.Count}");
        }

		public void ClearFlyTextQueue() {
            if (futureFlyText == null) return;

			FlyTextLog($"clearing flytext queue of {futureFlyText.Values.Count} items...");
			futureFlyText.Clear();
		}
    }
}