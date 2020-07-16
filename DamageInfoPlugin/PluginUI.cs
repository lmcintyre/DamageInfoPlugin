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
        private Vector3 color;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        // passing in the image here just for simplicity
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
	        if (!Visible)
	        {
		        return;
	        }

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
		        ImGui.InputFloat3("color", ref color);
		        ImGui.Spacing();
		        ImGui.InputInt("icon", ref icon);

		        if (ImGui.Button($"Set Hijack Struct")) {
			        uint tKind = (uint)kind;
			        uint tVal1 = (uint)val1;
			        uint tVal2 = (uint)val2;
			        uint tIcon = (uint)icon;

			        uint tColor = DamageInfoPlugin.Color3ToUint(color);

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
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(232, 75), ImGuiCond.Always);
            if (ImGui.Begin("A Wonderful Configuration Window", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse | 
                ImGuiWindowFlags.NoScrollbar | 
                ImGuiWindowFlags.NoScrollWithMouse
                ))
            {
                // can't ref a property, so use a local copy
                var configValue = this.configuration.Enabled;
                if (ImGui.Checkbox("Enabled: ", ref configValue))
                {
                    this.configuration.Enabled = configValue;
                    // can save immediately on change, if you don't want to provide a "Save and Close" button
                    this.configuration.Save();
                }
            }
            ImGui.End();
        }
    }
}
