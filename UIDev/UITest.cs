using ImGuiNET;
using ImGuiScene;
using System;
using System.Numerics;

namespace UIDev
{
    class UITest : IPluginUIMock
    {
        public static void Main(string[] args)
        {
            UIBootstrap.Inititalize(new UITest());
        }

        private SimpleImGuiScene scene;

        Vector4 testPhys = new Vector4(1, 0, 0, 1);
        Vector4 testMag = new Vector4(0, 0, 1, 1);
        Vector4 testDark = new Vector4(1, 0, 1, 1);

        public void Initialize(SimpleImGuiScene scene)
        {
            // scene is a little different from what you have access to in dalamud
            // but it can accomplish the same things, and is really only used for initial setup here

            // eg, to load an image resource for use with ImGui 

            scene.OnBuildUI += Draw;

            this.Visible = true;

            // saving this only so we can kill the test application by closing the window
            // (instead of just by hitting escape)
            this.scene = scene;
        }

        public void Dispose()
        {

        }

        // You COULD go all out here and make your UI generic and work on interfaces etc, and then
        // mock dependencies and conceivably use exactly the same class in this testbed and the actual plugin
        // That is, however, a bit excessive in general - it could easily be done for this sample, but I
        // don't want to imply that is easy or the best way to go usually, so it's not done here either
        private void Draw()
        {
            DrawMainWindow();
            DrawSettingsWindow();
            
            if (!Visible)
            {
                this.scene.ShouldQuit = true;
            }
        }

        #region Nearly a copy/paste of PluginUI
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

        // this is where you'd have to start mocking objects if you really want to match
        // but for simple UI creation purposes, just hardcoding values works
        private bool fakeConfigBool = true;

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(400, 200), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("DamageInfoDebug", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text($"The random config bool is {this.fakeConfigBool}");

                if (ImGui.Button("Show Settings"))
                {
                    SettingsVisible = true;
                }
            }
            ImGui.End();
        }

        /*
        // retired before it saw the light of day...
        // goodbye old friend
        private void DrawColorPickingWindow() {

	        if (!ShouldShowColorWindow)
		        return;

	        Vector4 thisColor;
	        string kind;
	        switch (ColorWindowType) {
                case 1:
	                thisColor = testPhys;
	                kind = "Physical";
	                break;
                case 2:
	                thisColor = testMag;
	                kind = "Magical";
                    break;
                case 3:
	                thisColor = testDark;
	                kind = "Darkness";
                    break;
                default:
	                return;
	        }

	        ImGui.SetNextWindowSize(new Vector2(300, 300), ImGuiCond.Always);
	        if (ImGui.Begin($"{kind} Color Picker", ref shouldShowColorWindow, ImGuiWindowFlags.Modal)) {
		        if (ImGui.ColorPicker4($"{kind} Color", ref thisColor, ImGuiColorEditFlags.Float)) {
		            System.Diagnostics.Debug.WriteLine($"{thisColor}");
		            System.Diagnostics.Debug.WriteLine($"{ImGui.GetColorU32(thisColor)}");
		        }
            }

	        switch (ColorWindowType) {
                case 1:
	                testPhys = thisColor;
	                break;
                case 2:
	                testMag = thisColor;
	                break;
                case 3:
	                testDark = thisColor;
	                break;
	        }
	        ImGui.End();
        }*/

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(370, 225), ImGuiCond.Always);
            if (ImGui.Begin("A Wonderful Configuration Window", ref this.settingsVisible,
	            ImGuiWindowFlags.NoScrollbar |
					ImGuiWindowFlags.NoScrollWithMouse
                )) {

	            Vector4 fakeV = new Vector4();

	            if (ImGui.ColorEdit4("Physical Color", ref fakeV)) { }

	            if (ImGui.ColorEdit4("Magical Color", ref fakeV))
	            {
	            }

	            if (ImGui.ColorEdit4("Darkness Color", ref fakeV))
	            {
	            }

	            if (ImGui.Checkbox("EffectLog Enabled (Debug)", ref fakeConfigBool))
	            {
	            }

	            if (ImGui.Checkbox("FlyTextLog Enabled (Debug)", ref fakeConfigBool))
	            {
	            }

	            if (ImGui.Checkbox("Text Coloring Enabled", ref fakeConfigBool))
	            {
	            }

	            if (ImGui.Checkbox("Source Text Enabled", ref fakeConfigBool))
	            {
	            }
            }
            ImGui.End();
        }
        #endregion
    }
}
