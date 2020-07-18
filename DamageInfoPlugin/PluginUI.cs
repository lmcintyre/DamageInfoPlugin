using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

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

	        ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
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
		        ImGui.InputFloat4("color", ref color);
		        ImGui.Spacing();
		        ImGui.InputInt("icon", ref icon);

		        if (ImGui.Button($"Set Hijack Struct")) {
			        uint tKind = (uint)kind;
			        uint tVal1 = (uint)val1;
			        uint tVal2 = (uint)val2;
			        uint tIcon = (uint)icon;
		        
			        uint tColor = ImGui.GetColorU32(color);
		        
			        string test1 = "test1";
			        string test2 = "test2";
		        
			        IntPtr tText1 = Marshal.StringToHGlobalAnsi(test1);
			        IntPtr tText2 = Marshal.StringToHGlobalAnsi(test2);
		        
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

            ImGui.SetNextWindowSize(new Vector2(370, 195), ImGuiCond.Always);
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
	            var colorTextConfigValue = configuration.TextColoringEnabled;

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

                if (ImGui.Checkbox("Text Coloring Enabled", ref colorTextConfigValue))
                {
	                configuration.TextColoringEnabled = colorTextConfigValue;
                    configuration.Save();
                }
            }
            ImGui.End();
        }
    }
}
