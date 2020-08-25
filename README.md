[![Download count](https://img.shields.io/endpoint?url=https%3A%2F%2Fvz32sgcoal.execute-api.us-east-1.amazonaws.com%2FDamageInfoPlugin)](https://github.com/lmcintyre/DamageInfoPlugin)

# DamageInfoPlugin
 Dalamud plugin for extra damage info in the native XIV flytext.

/dmginfo opens the configuration menu, where you can set the flytext color for the three different types of damage - physical, magical, and darkness. Blunt, piercing, and slashing are combined into physical, magic is magic, and darkness is a type of damage used by the game that is not magical or physical.

## Purpose
The purpose of this plugin is to provide extra information in channels untapped by Square in FFXIV. FFXIV has always had support for coloring the flying text, but has never used it to display anything meaningful. In addition, there are a number of text setups that can provide extra information in the subtitle usually reserved for parries or blocks.

## Stability
Plugin has been verified as stable. However, **if you do not want to risk crashing mid-combat, do not enable this plugin during combat.** Despite playing the game quite a bit with this enabled, there's always a possibility of weird behavior.

## Known issues
- The plugin does not work in TEA. Support is planned eventually.
- Misses, dodges, and incoming damage of 0 (due to shields or otherwise) are not supported.
- With source text enabled, a lot of strings are allocated. They are freed, but the long-term effects of this are unknown at the moment.
- From experience the plugin in its current state is quite reliable with damage type, however, it is for a general idea of damage type, rather than a 100% guarantee due to the way damage/flytext is linked.
If you encounter an issue, don't hesitate to mention me in the Dalamud/XIVLauncher discord in the appropriate channel. Please note your settings, the instance, the mob, and the job you were on/any buffs that were active when letting me know about a bug.