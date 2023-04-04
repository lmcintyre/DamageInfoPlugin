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
            if (ImGui.Begin("Damage Info Config", ref settingsVisible, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                // local copies of config properties
                var lPhys = configuration.PhysicalColor;
                var lMag = configuration.MagicColor;
                var lDark = configuration.DarknessColor;
                var lPosHit = configuration.PositionalHitColor;
                var lPosMiss = configuration.PositionalMissColor;
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
                var petColorConfigValue = configuration.PetColorEnabled;
                var posColorHitConfigValue = configuration.PositionalHitColorEnabled;
                var posColorMissConfigValue = configuration.PositionalMissColorEnabled;
                var seDamageIconDisableValue = configuration.SeDamageIconDisable;

                var sourceTextConfigValue = configuration.SourceTextEnabled;
                var petSourceTextConfigValue = configuration.PetSourceTextEnabled;
                var healSourceTextConfigValue = configuration.HealSourceTextEnabled;
                
                var incAttackTextConfigValue = configuration.IncomingAttackTextEnabled;
                var outAttackTextConfigValue = configuration.OutgoingAttackTextEnabled;
                var petAttackTextConfigValue = configuration.PetAttackTextEnabled;
                var healAttackTextConfigValue = configuration.HealAttackTextEnabled;

                var posPrefixTextHitEnabled = configuration.PositionalHitTextSettings.PrefixEnabled;
                var posPrefixTextMissEnabled = configuration.PositionalMissTextSettings.PrefixEnabled;
                var posSuffixTextHitEnabled = configuration.PositionalHitTextSettings.SuffixEnabled;
                var posSuffixTextMissEnabled = configuration.PositionalMissTextSettings.SuffixEnabled;
                var posPrefixTextHit = configuration.PositionalHitTextSettings.Prefix;
                var posPrefixTextMiss = configuration.PositionalMissTextSettings.Prefix;
                var posSuffixTextHit = configuration.PositionalHitTextSettings.Suffix;
                var posSuffixTextMiss = configuration.PositionalMissTextSettings.Suffix;
                var posOverrideEnabled = configuration.PositionalAttackTextOverrideEnabled;

                // computed state
                var colorAllConfigValue = incomingColorConfigValue && outgoingColorConfigValue && petColorConfigValue;
                var sourceTextAllConfigValue = sourceTextConfigValue && petSourceTextConfigValue && healSourceTextConfigValue;
                var attackTextAllConfigValue = incAttackTextConfigValue && outAttackTextConfigValue && petAttackTextConfigValue && healAttackTextConfigValue;
                

                if (ImGui.CollapsingHeader("Damage type information"))
                {
                    ImGui.TextWrapped(
                        "Each attack in the game has a specific damage type, such as blunt, piercing, magic, " +
                        "limit break, \"breath\", and more. The only important damage types for mitigation are " +
                        "physical (encompassing slashing, blunt, and piercing), magic, and unique (sometimes referred to by the " +
                        "community as \"darkness\" damage).");
                    ImGui.TextWrapped(
                        "Physical damage can be mitigated by reducing an enemy's strength stat, or with moves " +
                        "that specifically mention physical damage reduction. Magic damage can be mitigated by " +
                        "reducing an enemy's intelligence stat, or with moves that specifically mention magic damage " +
                        "reduction. Unique damage cannot be mitigated by reducing an enemy's stats or mitigating " +
                        "against physical or magic damage - only moves that \"reduce a target's damage dealt\" will " +
                        "affect unique damage.");
                }

                if (ImGui.CollapsingHeader("Flytext"))
                {
                    ImGui.Text("Flytext Options");
                    ImGui.Columns(4, "dmginfoflytextoptions", false);
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
                        configuration.PetColorEnabled = colorAllConfigValue;
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
                    if (ImGui.Checkbox("##petcolor", ref petColorConfigValue))
                    {
                        configuration.PetColorEnabled = petColorConfigValue;
                        configuration.Save();
                    }
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
                    if (ImGui.Checkbox("Disable vanilla damage type icons", ref seDamageIconDisableValue))
                    {
                        configuration.SeDamageIconDisable = seDamageIconDisableValue;
                        configuration.Save();
                    }

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

                    if (ImGui.ColorEdit4("Unique##flytext", ref lDark))
                    {
                        configuration.DarknessColor = lDark;
                        configuration.Save();
                    }

                    if (ImGui.Button("Set colors to match vanilla damage type icons"))
                        ImGui.OpenPopup("Damage Info");

                    var center = ImGui.GetMainViewport().GetCenter();
                    ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
                    var set = false;

                    var b1 = true;
                    if (ImGui.BeginPopupModal("Damage Info", ref b1, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.Text("This will wipe your existing flytext colors for physical, magical,\n" +
                                   "and unique damage, and replace them with pre-set colors\n" +
                                   "that match the vanilla flytext damage type icons.\n" +
                                   "This is meant to complement the vanilla damage icons\n" +
                                   "rather than improve damage type visibility.\n\n" +
                                   "This cannot be undone automatically.\n" +
                                   "Are you sure you want to continue?");
                        if (ImGui.Button("Continue"))
                        {
                            set = true;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                    
                    if (set)
                    {
                        configuration.PhysicalColor = ImGui.ColorConvertU32ToFloat4(0xffc39c5f);
                        configuration.MagicColor = ImGui.ColorConvertU32ToFloat4(0xffc059a8);
                        configuration.DarknessColor = ImGui.ColorConvertU32ToFloat4(0xff49b859);
                        configuration.Save();
                    }
                }

                if (ImGui.CollapsingHeader("Positionals"))
                {
                    ImGui.Text("Enable positional hit color");
                    ImGui.SameLine();
                    if (ImGui.Checkbox("##positionalcolorhit", ref posColorHitConfigValue))
                    {
                        configuration.PositionalHitColorEnabled = posColorHitConfigValue;
                        configuration.Save();
                    }
                    ImGui.Text("Enable positional miss color");
                    ImGui.SameLine();
                    if (ImGui.Checkbox("##positionalcolormiss", ref posColorMissConfigValue))
                    {
                        configuration.PositionalMissColorEnabled = posColorMissConfigValue;
                        configuration.Save();
                    }
                    ImGui.Separator();

                    ImGui.TextUnformatted("Positional Color options");

                    if (ImGui.ColorEdit4("Positional Hit##flytext", ref lPosHit))
                    {
                        configuration.PositionalHitColor = lPosHit;
                        configuration.Save();
                    }
                    if (ImGui.ColorEdit4("Positional Miss##flytext", ref lPosMiss))
                    {
                        configuration.PositionalMissColor = lPosMiss;
                        configuration.Save();
                    }
                    if (ImGui.Button("Copy Hit to Miss"))
                    {
                        configuration.PositionalMissColor = configuration.PositionalHitColor;
                        configuration.Save();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Copy Miss to Hit"))
                    {
                        configuration.PositionalHitColor = configuration.PositionalMissColor;
                        configuration.Save();
                    }
                    ImGui.Separator();
                    ImGui.TextUnformatted("Positional Attack Text options");
                    ImGui.Columns(3, "dmginfopositionaltextoptions", false);
                    ImGui.NextColumn();
                    ImGui.Text("Prefix");
                    ImGui.NextColumn();
                    ImGui.Text("Suffix");
                    ImGui.NextColumn();
                    ImGui.Text("Hit");
                    ImGui.NextColumn();
                    if (ImGui.Checkbox("##hitprefix", ref posPrefixTextHitEnabled))
                    {
                        configuration.PositionalHitTextSettings.PrefixEnabled = posPrefixTextHitEnabled;
                        configuration.Save();
                    }
                    ImGui.NextColumn();
                    if (ImGui.Checkbox("##hitsuffix", ref posSuffixTextHitEnabled))
                    {
                        configuration.PositionalHitTextSettings.SuffixEnabled = posSuffixTextHitEnabled;
                        configuration.Save();
                    }
                    ImGui.NextColumn();
                    ImGui.Text("Miss");
                    ImGui.NextColumn();
                    if (ImGui.Checkbox("##missprefix", ref posPrefixTextMissEnabled))
                    {
                        configuration.PositionalMissTextSettings.PrefixEnabled = posPrefixTextMissEnabled;
                        configuration.Save();
                    }
                    ImGui.NextColumn();
                    if (ImGui.Checkbox("##misssuffix", ref posSuffixTextMissEnabled))
                    {
                        configuration.PositionalMissTextSettings.SuffixEnabled = posSuffixTextMissEnabled;
                        configuration.Save();
                    }
                    ImGui.Columns(1, "dmginfopositionaltextoptions", false);
                    if (ImGui.Checkbox("Positional Text overrides Attack Text options", ref posOverrideEnabled))
                    {
                        configuration.PositionalAttackTextOverrideEnabled = posOverrideEnabled;
                        configuration.Save();
                    }
                    if (posOverrideEnabled)
                    {
                        ImGui.TextWrapped("The Positional Action Text options will override the Action Text options for Positional Attacks. " +
                                          "For example, if Action Text is disabled for outgoing damage, Positional Text will still show if you have " +
                                          "any prefixes or suffixes enabled.");
                    }
                    else
                    {
                        ImGui.TextWrapped("The Positional Action Text options will not override the Action Text options for Positional Attacks. " +
                                          "For example, if Action Text is disabled for outgoing damage, Positional Text will not show, even if you have " +
                                          "any prefixes or suffixes enabled.");
                    }
                    ImGui.Separator();
                    ImGui.TextUnformatted("Positional hit:");
                    ImGui.PushItemWidth(ImGuiHelpers.GlobalScale * 120);
                    ImGui.SameLine();
                    if (ImGui.InputText("Prefix##positionalhittext", ref posPrefixTextHit, 16))
                    {
                        configuration.PositionalHitTextSettings.Prefix = posPrefixTextHit;
                        configuration.Save();
                    }
                    ImGui.SameLine();
                    if (ImGui.InputText("Suffix##positionalhittext", ref posSuffixTextHit, 16))
                    {
                        configuration.PositionalHitTextSettings.Suffix = posSuffixTextHit;
                        configuration.Save();
                    }
                    ImGui.PopItemWidth();
                    ImGui.TextUnformatted("Positional miss:");
                    ImGui.PushItemWidth(ImGuiHelpers.GlobalScale * 120);
                    ImGui.SameLine();
                    if (ImGui.InputText("Prefix##positionalmisstext", ref posPrefixTextMiss, 16))
                    {
                        configuration.PositionalMissTextSettings.Prefix = posPrefixTextMiss;
                        configuration.Save();
                    }
                    ImGui.SameLine();
                    if (ImGui.InputText("Suffix##positionalmisstext", ref posSuffixTextMiss, 16))
                    {
                        configuration.PositionalMissTextSettings.Suffix = posSuffixTextMiss;
                        configuration.Save();
                    }
                    ImGui.PopItemWidth();
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

                    if (ImGui.ColorEdit4("Unique##gauge", ref gDark))
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

                    if (ImGui.ColorEdit4("Unique##bg", ref bgDark))
                    {
                        configuration.DarknessBgColor = bgDark;
                        configuration.Save();
                    }
                }
                
                Fools2023.DrawConfig();

                if (ImGui.CollapsingHeader("Debug"))
                {
                    if (ImGui.Checkbox("Debug Log Enabled", ref debugLogConfigValue))
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