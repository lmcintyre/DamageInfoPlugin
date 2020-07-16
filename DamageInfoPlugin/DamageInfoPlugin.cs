using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Hooking;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DamageInfoPlugin
{
    // members suffixed with a number seem to be a duplicate
	enum FlyTextKind {
        // val1 in serif font, text1 sans-serif as subtitle
        AutoAttack,

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

        NamedAttack2,
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



    enum DamageType {
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

    public class DamageInfoPlugin : IDalamudPlugin
    {
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
        // private Dictionary<uint, DamageType> weaponToDamageTypeDict;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;

            actionToDamageTypeDict = new Dictionary<uint, DamageType>();
            // weaponToDamageTypeDict = new Dictionary<uint, DamageType>();

            var actionSheet = pi.Data.GetExcelSheet<Action>();
            var itemSheet = pi.Data.GetExcelSheet<Item>();
            foreach (var row in actionSheet.ToList()) {
	            DamageType tmpType = (DamageType) row.AttackType.Row;
	            if (tmpType != DamageType.Magic && tmpType != DamageType.Darkness)
		            tmpType = DamageType.Physical;
            
                actionToDamageTypeDict.Add(row.RowId, tmpType);
            }
				

            // foreach (var row in itemSheet.ToList()) {
	           //  if (row.EquipSlotCategory.Value.RowId == 1 || row.EquipSlotCategory.Value.RowId == 13) {
		          //   weaponToDamageTypeDict.Add(row.RowId, (DamageType)row.);
            //     }
            // }

            // PluginLog.Log($"{pi.ClientState.LocalPlayer.Address}");

	        configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi);

            ui = new PluginUI(configuration, this);

            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Display the Damage Info configuration interface."
            });

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

        public void Dispose()
        {
            ui.Dispose();

            createFlyTextHook.Disable();
            createFlyTextHook.Dispose();

            receiveActionEffectHook.Disable();
            receiveActionEffectHook.Dispose();

            pi.CommandManager.RemoveHandler(commandName);
            pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            ui.Visible = true;
        }

        private void DrawUI()
        {
            this.ui.Draw();
        }

        private void DrawConfigUI()
        {
            ui.SettingsVisible = true;
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
	        string strText1, strText2;
	        if (Hijack) {
		        strText1 = Marshal.PtrToStringAnsi(hijackStruct.text1);
		        strText2 = Marshal.PtrToStringAnsi(hijackStruct.text2);

		        PluginLog.Log($"flytext created: kind: {hijackStruct.kind}, val1: {hijackStruct.val1}, val2: {hijackStruct.val2}, color: {hijackStruct.color:X}, icon: {hijackStruct.icon}");
		        PluginLog.Log($"text1: {strText1} | text2: {strText2}");

		        return createFlyTextHook.Original(flyTextMgr, hijackStruct.kind, hijackStruct.val1, hijackStruct.val2, hijackStruct.text1, hijackStruct.color, hijackStruct.icon, hijackStruct.text2, unk3);
            }

	        strText1 = Marshal.PtrToStringAnsi(text1);
	        strText2 = Marshal.PtrToStringAnsi(text2);
            //Marshal.StringToHGlobalAnsi()
			PluginLog.Log($"flytext created: kind: {kind}, val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}");
            PluginLog.Log($"text1: {strText1} | text2: {strText2}");

			return createFlyTextHook.Original(flyTextMgr, kind, val1, val2, text1, color, icon, text2, unk3);
        }

        public unsafe delegate void ReceiveActionEffectDelegate(IntPtr unk1, IntPtr unk2, IntPtr unk3, IntPtr packet, IntPtr unk4, IntPtr unk5, IntPtr unk6);

        public unsafe void ReceiveActionEffect(IntPtr unk1, IntPtr unk2, IntPtr unk3,
										        IntPtr packet,
										        IntPtr unk4, IntPtr unk5, IntPtr unk6) {
			PluginLog.Log($"packet at {packet.ToInt64():X}");
			uint id = *((uint*)packet.ToPointer() + 0x2);
            PluginLog.Log($"action id {id}");


			receiveActionEffectHook.Original(unk1, unk2, unk3, packet, unk4, unk5, unk6);
        }

        public static UInt32 Color3ToUint(Vector3 color)
        {
	        byte[] tmp = new byte[4];
	        tmp[0] = (byte)Math.Truncate(color.X * 255); //r
	        tmp[1] = (byte)Math.Truncate(color.Y * 255); //g
	        tmp[2] = (byte)Math.Truncate(color.Z * 255); //b
	        tmp[3] = 0xFF;

	        return BitConverter.ToUInt32(tmp, 0);
        }

        public static UInt32 Color4ToUint(Vector4 color)
        {
	        byte[] tmp = new byte[4];
	        tmp[0] = (byte)Math.Truncate(color.X * 255); //r
	        tmp[1] = (byte)Math.Truncate(color.Y * 255); //g
	        tmp[2] = (byte)Math.Truncate(color.Z * 255); //b
	        tmp[3] = (byte)Math.Truncate(color.W * 255); //a

	        return BitConverter.ToUInt32(tmp, 0);
        }
    }
}
