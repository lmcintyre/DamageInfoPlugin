using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;

namespace DamageInfoPlugin
{
    class PluginUI : IDisposable
    {
        private Configuration configuration;
        private DamageInfoPlugin damageInfoPlugin;

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
	        DrawSettingsWindow();
        }

        private void DrawSettingsWindow()
        {
	        if (!SettingsVisible) return;

            ImGui.SetNextWindowSize(new Vector2(400, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Damage Info Config", ref settingsVisible, ImGuiWindowFlags.AlwaysVerticalScrollbar)) {

                // local copies of config properties
	            var lPhys = configuration.PhysicalColor;
	            var lMag = configuration.MagicColor;
	            var lDark = configuration.DarknessColor;
	            var gPhys = configuration.PhysicalCastColor;
	            var gMag = configuration.MagicCastColor;
	            var gDark = configuration.DarknessCastColor;
	            var bgPhys = configuration.PhysicalBgColor;
	            var bgMag = configuration.MagicBgColor;
	            var bgDark = configuration.DarknessBgColor;
	            var castBarConfigValue = configuration.MainTargetCastBarColorEnabled;
	            var ftCastBarConfigValue = configuration.FocusTargetCastBarColorEnabled;
	            var debugLogConfigValue = configuration.DebugLogEnabled;
	            var incomingColorConfigValue = configuration.IncomingColorEnabled;
	            var outgoingColorConfigValue = configuration.OutgoingColorEnabled;
	            var sourceTextConfigValue = configuration.SourceTextEnabled;
	            var petSourceTextConfigValue = configuration.PetSourceTextEnabled;
	            var healSourceTextConfigValue = configuration.HealSourceTextEnabled;
	            var incAttackTextConfigValue = configuration.IncomingAttackTextEnabled;
	            var outAttackTextConfigValue = configuration.OutgoingAttackTextEnabled;
	            var petAttackTextConfigValue = configuration.PetAttackTextEnabled;
	            var healAttackTextConfigValue = configuration.HealAttackTextEnabled;
	            
	            // computed state
	            var colorAllConfigValue = incomingColorConfigValue && outgoingColorConfigValue;
	            var sourceTextAllConfigValue = sourceTextConfigValue && petSourceTextConfigValue && healSourceTextConfigValue;
	            var attackTextAllConfigValue = incAttackTextConfigValue && outAttackTextConfigValue && petAttackTextConfigValue && healAttackTextConfigValue;

	            if (ImGui.CollapsingHeader("Damage type information"))
	            {
		            ImGui.TextWrapped(
			        "Each attack in the game has a specific damage type, such as blunt, piercing, magic, " +
			        "limit break, \"breath\", and more. The only important damage types for mitigation are " +
			        "physical (encompassing slashing, blunt, and piercing), magic, and breath (referred to the " +
			        "community as \"darkness\" damage).");
		            ImGui.TextWrapped(
			        "Physical damage can be mitigated by reducing an enemy's strength stat, or with moves " +
			        "that specifically mention physical damage reduction. Magic damage can be mitigated by " +
			        "reducing an enemy's intelligence stat, or with moves that specifically mention magic damage " +
			        "reduction. Darkness damage cannot be mitigated by reducing an enemy's stats or mitigating " +
			        "against physical or magic damage - only moves that \"reduce a target's damage dealt\" will " +
			        "affect darkness damage.");
	            }

	            if (ImGui.CollapsingHeader("Flytext"))
	            {
		            ImGui.Columns(4, "FT Options", false);
		            ImGui.NextColumn();
		            ImGui.Text("Color");
		            ImGui.NextColumn();
		            ImGui.Text("Source Text");
		            ImGui.NextColumn();
		            ImGui.Text("Action Text");
		            ImGui.NextColumn();
		            ImGui.Text("All");
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##allcolor", ref colorAllConfigValue))
		            {
			            configuration.IncomingColorEnabled = colorAllConfigValue;
			            configuration.OutgoingColorEnabled = colorAllConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##allsource", ref sourceTextAllConfigValue))
		            {
			            configuration.SourceTextEnabled = sourceTextAllConfigValue;
			            configuration.PetSourceTextEnabled = sourceTextAllConfigValue;
			            configuration.HealSourceTextEnabled = sourceTextAllConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##allattacktext", ref attackTextAllConfigValue))
		            {
			            configuration.IncomingAttackTextEnabled = attackTextAllConfigValue;
			            configuration.OutgoingAttackTextEnabled = attackTextAllConfigValue;
			            configuration.PetAttackTextEnabled = attackTextAllConfigValue;
			            configuration.HealAttackTextEnabled = attackTextAllConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            ImGui.Text("Incoming Damage");
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##incomingcolor", ref incomingColorConfigValue))
		            {
			            configuration.IncomingColorEnabled = incomingColorConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##incomingsource", ref sourceTextConfigValue))
		            {
			            configuration.SourceTextEnabled = sourceTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##incomingattack", ref incAttackTextConfigValue))
		            {
			            configuration.IncomingAttackTextEnabled = incAttackTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            ImGui.Text("Outgoing Damage");
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##outgoingcolor", ref outgoingColorConfigValue))
		            {
			            configuration.OutgoingColorEnabled = outgoingColorConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##outgoingattack", ref outAttackTextConfigValue))
		            {
			            configuration.OutgoingAttackTextEnabled = outAttackTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            ImGui.Text("Pet");
		            ImGui.NextColumn();
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##petsourcetext", ref petSourceTextConfigValue))
		            {
			            configuration.PetSourceTextEnabled = petSourceTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##petattack", ref petAttackTextConfigValue))
		            {
			            configuration.PetAttackTextEnabled = petAttackTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            ImGui.Text("Heals");
		            ImGui.NextColumn();
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##healsourcetext", ref healSourceTextConfigValue))
		            {
			            configuration.HealSourceTextEnabled = healSourceTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.NextColumn();
		            if (ImGui.Checkbox("##healattacktext", ref healAttackTextConfigValue))
		            {
			            configuration.HealAttackTextEnabled = healAttackTextConfigValue;
			            configuration.Save();
		            }
		            ImGui.Columns(1, "FT Options");
		            ImGui.Separator();
		            
		            ImGui.Text("Flytext Colors");

		            if (ImGui.ColorEdit4("Physical##flytext", ref lPhys))
		            {
			            configuration.PhysicalColor = lPhys;
			            configuration.Save();
		            }

		            if (ImGui.ColorEdit4("Magical##flytext", ref lMag))
		            {
			            configuration.MagicColor = lMag;
			            configuration.Save();
		            }

		            if (ImGui.ColorEdit4("Darkness##flytext", ref lDark))
		            {
			            configuration.DarknessColor = lDark;
			            configuration.Save();
		            }
	            }

	            if (ImGui.CollapsingHeader("Castbars"))
	            {
		            ImGui.Text("Main target");
		            ImGui.SameLine();
		            if (ImGui.Checkbox("##maintargetcheck", ref castBarConfigValue))
		            {
			            configuration.MainTargetCastBarColorEnabled = castBarConfigValue;
			            configuration.Save();
		            }
		            
		            ImGui.Text("Focus target");
		            ImGui.SameLine();
		            if (ImGui.Checkbox("##focustargetcheck", ref ftCastBarConfigValue))
		            {
			            configuration.FocusTargetCastBarColorEnabled = ftCastBarConfigValue;
			            configuration.Save();
		            }
		            
		            ImGui.Text("Castbar Gauge Colors:");

		            if (ImGui.ColorEdit4("Physical##gauge", ref gPhys))
		            {
			            configuration.PhysicalCastColor = gPhys;
			            configuration.Save();
		            }

		            if (ImGui.ColorEdit4("Magical##gauge", ref gMag))
		            {
			            configuration.MagicCastColor = gMag;
			            configuration.Save();
		            }

		            if (ImGui.ColorEdit4("Darkness##gauge", ref gDark))
		            {
			            configuration.DarknessCastColor = gDark;
			            configuration.Save();
		            }
		            
		            ImGui.Text("Castbar Gauge Border Colors:");
		            ImGui.Bullet();
		            ImGui.SameLine();
		            ImGui.TextWrapped("Due to the castbar border texture being yellow, the blue channel does " +
		                              "not appear properly on the castbar border.");
		            if (ImGui.ColorEdit4("Physical##bg", ref bgPhys))
		            {
			            configuration.PhysicalBgColor = bgPhys;
			            configuration.Save();
		            }

		            if (ImGui.ColorEdit4("Magical##bg", ref bgMag))
		            {
			            configuration.MagicBgColor = bgMag;
			            configuration.Save();
		            }

		            if (ImGui.ColorEdit4("Darkness##bg", ref bgDark))
		            {
			            configuration.DarknessBgColor = bgDark;
			            configuration.Save();
		            }
	            }
	            
	            if (ImGui.CollapsingHeader("Debug"))
	            {
		            if (ImGui.Checkbox("Enable Debug Logging", ref debugLogConfigValue))
		            {
			            configuration.DebugLogEnabled = debugLogConfigValue;
			            configuration.Save();
		            }
	            }
			}
            ImGui.End();
        }
    }
}
