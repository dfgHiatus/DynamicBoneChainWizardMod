# DynamicBoneWizardMod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that automatically sets up [Dynamic Bone Chains](https://wiki.resonite.com/Dynamic_Bone_Chain/en) on avatars and validly named slots.

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [DynamicBoneWizardMod.zip](https://github.com/dfgHiatus/DynamicBoneChainWizardMod/releases/tag/v1.0.0) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

### Starting a bone's name with \<DynBone> will indicate to the mod a DynamicBoneChain should be placed on it.

## Info and Customization
The DynamicBoneWizardMod creates a folder called `_BoneLists` in `rml_mods`. Inside, there are 3 text files:
1. listOfSingletons
2. listOfPrefixes
3. listOfSuffixes

At a high level, the mod creates a master list comprised of all the elements of "listOfSingletons", followed by a prefix (from listOfPrefixes) with every suffix (from listOfSuffixes) appended to it. You can edit these in real-time while the game engine is running to add custom bone names to your liking. If you'd like to add bone names to this repository, feel free to open a pull request!

Thanks to all those who helped me create and test my mod. Have fun!

## And a big thanks to FirrSkunk for sponsoring me and my projects!
