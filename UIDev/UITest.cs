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

        private TextureWrap goatImage;
        private SimpleImGuiScene scene;
        private Vector3 baseColor = new Vector3();

        private int kind, val1, val2, color, icon;

        public void Initialize(SimpleImGuiScene scene)
        {
            // scene is a little different from what you have access to in dalamud
            // but it can accomplish the same things, and is really only used for initial setup here

            // eg, to load an image resource for use with ImGui 
            this.goatImage = scene.LoadImage(@"goat.png");

            scene.OnBuildUI += Draw;

            this.Visible = true;

            // saving this only so we can kill the test application by closing the window
            // (instead of just by hitting escape)
            this.scene = scene;
        }

        public void Dispose()
        {
            this.goatImage.Dispose();
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
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
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

                ImGui.Spacing();
                ImGui.InputInt("kind", ref kind);
                ImGui.Spacing();
                ImGui.InputInt("val1", ref val1);
                ImGui.Spacing();
                ImGui.InputInt("val2", ref val2);
                ImGui.Spacing();
                ImGui.InputFloat3("color", ref baseColor);
                ImGui.Spacing();
                ImGui.InputInt("icon", ref icon);

                if (ImGui.Button($"Show flytext"))
                {
					System.Diagnostics.Debug.WriteLine($"k: {kind} val1: {val1} etc");
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

            ImGui.SetNextWindowSize(new Vector2(232, 75), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("A Wonderful Configuration Window", ref this.settingsVisible,
                // ImGuiWindowFlags.AlwaysAutoResize |
                // ImGuiWindowFlags.NoResize |
                // ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse
                )) {
	            // ImGui.ColorEdit3("test", ref colorOut);
                if (ImGui.ColorPicker3("test2", ref baseColor,
	                ImGuiColorEditFlags.Float
	                )) {
                    System.Diagnostics.Debug.WriteLine($"{baseColor}");
                    System.Diagnostics.Debug.WriteLine($"{Color3ToUint(baseColor)}");
                }
	            
                if (ImGui.Checkbox("Random Config Bool", ref this.fakeConfigBool))
                {
                    // nothing to do in a fake ui!
                }
            }
            ImGui.End();
        }
        #endregion

        private static UInt32 Color3ToUint(Vector3 color)
        {
	        byte[] tmp = new byte[4];
	        tmp[0] = (byte)Math.Truncate(color.X * 255); //r
	        tmp[1] = (byte)Math.Truncate(color.Y * 255); //g
	        tmp[2] = (byte)Math.Truncate(color.Z * 255); //b
	        tmp[3] = 0xFF;

	        return BitConverter.ToUInt32(tmp, 0);
        }

        private static UInt32 Color4ToUint(Vector4 color) {
	        byte[] tmp = new byte[4];
	        tmp[0] = (byte)Math.Truncate(color.X * 255); //r
	        tmp[1] = (byte)Math.Truncate(color.Y * 255); //g
	        tmp[2] = (byte)Math.Truncate(color.Z * 255); //b
	        tmp[3] = (byte)Math.Truncate(color.W * 255); //a

	        return BitConverter.ToUInt32(tmp, 0);
        }
    }
}
