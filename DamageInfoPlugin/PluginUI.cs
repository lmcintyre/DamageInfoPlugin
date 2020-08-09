using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Plugin;

namespace DamageInfoPlugin
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;
        private DamageInfoPlugin damageInfoPlugin;

        private int kind, val1, val2, icon;
        private Vector4 color;
        private byte[] text1 = new byte[32];
        private byte[] text2 = new byte[32];

		// this extra bool exists for ImGui, since you can't ref a property
		private bool visible = false;
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        public PluginUI(Configuration configuration, DamageInfoPlugin damageInfoPlugin)
        {
            this.configuration = configuration;
            this.damageInfoPlugin = damageInfoPlugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
            DrawSettingsWindow();
        }

        public void DrawMainWindow()
        {
	        if (!Visible) return;

	        ImGui.SetNextWindowSize(new Vector2(400, 350), ImGuiCond.FirstUseEver);
	        ImGui.SetNextWindowSizeConstraints(new Vector2(400, 200), new Vector2(float.MaxValue, float.MaxValue));
	        if (ImGui.Begin("DamageInfoDebug", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
		        bool hijack = damageInfoPlugin.Hijack;
		        if (ImGui.Checkbox("Hijack flytext", ref hijack)) {
			        damageInfoPlugin.Hijack = hijack;
		        }

		        if (ImGui.Button("Show Settings"))
		        {
			        SettingsVisible = true;
		        }

		        ImGui.Spacing();
		        ImGui.InputInt("kind", ref kind);
		        ImGui.Spacing();
		        ImGui.InputInt("val1", ref val1);
		        ImGui.Spacing();
		        ImGui.InputInt("val2", ref val2);
		        ImGui.Spacing();
		        ImGui.InputText("text1", text1, (uint) text1.Length);
		        ImGui.Spacing();
		        ImGui.InputText("text2", text2, (uint) text2.Length);
				ImGui.Spacing();
                ImGui.InputFloat4("color", ref color);
		        ImGui.Spacing();
		        ImGui.InputInt("icon", ref icon);

		        if (ImGui.Button($"Set Hijack Struct")) {
			        uint tKind = (uint)kind;
			        uint tVal1 = (uint)val1;
			        uint tVal2 = (uint)val2;
			        uint tIcon = (uint)icon;
		        
			        uint tColor = ImGui.GetColorU32(color);
					
			        string text1Str = System.Text.Encoding.ASCII.GetString(text1);
			        string text2Str = System.Text.Encoding.ASCII.GetString(text2);

					PluginLog.Log($"text1 : {text1Str}");
					PluginLog.Log($"text2 : {text2Str}");
					
			        IntPtr tText1 = Marshal.StringToHGlobalAnsi(text1Str);
			        IntPtr tText2 = Marshal.StringToHGlobalAnsi(text2Str);
		        
			        damageInfoPlugin.hijackStruct = new HijackStruct() {
				        kind = tKind,
				        val1 = tVal1,
				        val2 = tVal2,
				        color = tColor,
				        icon = tIcon,
				        text1 = tText1,
				        text2 = tText2
			        };
		        }
	        }
	        ImGui.End();
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible) return;

            ImGui.SetNextWindowSize(new Vector2(370, 300), ImGuiCond.Always);
            if (ImGui.Begin("Damage Info Config", ref settingsVisible,
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse | 
                ImGuiWindowFlags.NoScrollbar | 
                ImGuiWindowFlags.NoScrollWithMouse
                )) {

                // local copies of config properties
	            var lPhys = configuration.PhysicalColor;
	            var lMag = configuration.MagicColor;
	            var lDark = configuration.DarknessColor;
	            var effectLogConfigValue = configuration.EffectLogEnabled;
	            var flytextLogConfigValue = configuration.FlyTextLogEnabled;
	            var colorIncTextConfigValue = configuration.IncomingColorEnabled;
	            var colorOutTextConfigValue = configuration.OutgoingColorEnabled;
	            var petColorConfigValue = configuration.PetDamageColorEnabled;
	            var sourceTextConfigValue = configuration.SourceTextEnabled;
	            var petSourceTextConfigValue = configuration.PetSourceTextEnabled;

	            if (ImGui.ColorEdit4("Physical Color", ref lPhys)) {
				    configuration.PhysicalColor = lPhys;
				    configuration.Save();
				}

				if (ImGui.ColorEdit4("Magical Color", ref lMag))
				{
					configuration.MagicColor = lMag;
					configuration.Save();
				}

				if (ImGui.ColorEdit4("Darkness Color", ref lDark))
				{
					configuration.DarknessColor = lDark;
					configuration.Save();
				}

                if (ImGui.Checkbox("EffectLog Enabled (Debug)", ref effectLogConfigValue))
                {
                    configuration.EffectLogEnabled = effectLogConfigValue;
                    configuration.Save();
                }

                if (ImGui.Checkbox("FlyTextLog Enabled (Debug)", ref flytextLogConfigValue))
                {
	                configuration.FlyTextLogEnabled = flytextLogConfigValue;
	                configuration.Save();
                }

                if (ImGui.Checkbox("Incoming Text Coloring", ref colorIncTextConfigValue))
                {
	                configuration.IncomingColorEnabled = colorIncTextConfigValue;
                    configuration.Save();
                }

                if (ImGui.Checkbox("Outgoing Text Coloring", ref colorOutTextConfigValue))
                {
	                configuration.OutgoingColorEnabled = colorOutTextConfigValue;
	                configuration.Save();
                }

                if (ImGui.Checkbox("Pet Damage Coloring", ref petColorConfigValue))
                {
	                configuration.PetDamageColorEnabled = petColorConfigValue;
	                configuration.Save();
                }

				if (ImGui.Checkbox("Source Text", ref sourceTextConfigValue))
                {
	                configuration.SourceTextEnabled = sourceTextConfigValue;
	                configuration.Save();
                }

				if (ImGui.Checkbox("Pet Source Text", ref petSourceTextConfigValue))
				{
					configuration.PetSourceTextEnabled = petSourceTextConfigValue;
					configuration.Save();
				}
			}
            ImGui.End();
        }
    }
}
