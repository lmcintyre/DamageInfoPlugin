[![Download count](https://img.shields.io/endpoint?url=https%3A%2F%2Fvz32sgcoal.execute-api.us-east-1.amazonaws.com%2FDamageInfoPlugin)](https://github.com/lmcintyre/DamageInfoPlugin)
[![Build status](https://github.com/lmcintyre/DamageInfoPlugin/actions/workflows/build.yml/badge.svg)](https://github.com/lmcintyre/DamageInfoPlugin)
[![Latest release](https://img.shields.io/github/v/release/lmcintyre/DamageInfoPlugin)](https://github.com/lmcintyre/DamageInfoPlugin)

# DamageInfoPlugin
 Dalamud plugin for extra damage info in the native XIV flytext.

/dmginfo opens the configuration menu, where you can set the flytext color for the three different types of damage - physical, magical, and darkness. Blunt, piercing, and slashing are combined into physical, magic is magic, and darkness is a type of damage used by the game that is not magical or physical.

## Purpose
The purpose of this plugin is to provide extra information in channels untapped by Square in FFXIV. FFXIV has always had support for coloring the flying text, but has never used it to display anything meaningful. In addition, there are a number of text setups that can provide extra information in the subtitle usually reserved for parries or blocks.

## Stability
Plugin has been verified as stable. No crashes have been reported in many months, so feel free to use this plugin during combat. If you do encounter a crash, please mention me in Discord as soon as possible so it can be rectified.

## Known issues
- From experience the plugin in its current state is quite reliable with damage type, however, it is for a general idea of damage type, rather than a 100% guarantee due to the way damage/flytext is linked.
- Some actions that are cast by enemies are not the same action that take effect on the player. For example, the boss may cast "Damaging Attack", that is a magic action. However, the damage that the player receives is a _different action_ called "Damaging Attack", that may be physical. The reason for this is unknown. It only affects cast bar accuracy. It cannot be fixed.
If you encounter an issue, don't hesitate to mention me in the Dalamud/XIVLauncher discord in the appropriate channel. Please note your settings, the instance, the mob, and the job you were on/any buffs that were active when letting me know about a bug.