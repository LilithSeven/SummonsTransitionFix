# Summons Transition Fix

A mod for **Pathfinder: Wrath of the Righteous** that keeps your summoned creatures and reanimated minions (Lich Repurpose and similar abilities) with your party through doors, loading screens and world map travel.

Tested with **Pathfinder: WotR 2.7.0x** and **Unity Mod Manager (UMM) 0.32.4**.

## The problem

In the base game, temporary minions are tied to the spot where you summoned them.

- **Local transitions (doors, caves)**: only your main party members get teleported. Your summons get stuck on the other side of whatever you just went through.
- **Global transitions (loading screens, world map)**: the game removes anything that is not a companion when it unloads the area.

## What the mod does

When you leave an area, the mod takes your active minions along with the party. When the new area loads, it puts them right back at their master's side.

- **Summons and reanimated minions**: regular summons, undead raised with Repurpose or Flay for Purpose, and creatures bound by Doom of Servitude.
- **Only your minions**: companions (active or in reserve), pets, mounts and NPCs are left alone. Dead minions stay behind.
- **No marching order**: minions appear next to their master instead of taking a slot in the party formation.
- **Light on your saves**: the mod adds no data of its own to your save files.

If you run into a problem, let me know on the Nexus page. The place and the creature involved help a lot.

## Settings

Open the Unity Mod Manager menu (`Ctrl + F10`):

1. **Enable Local Transitions** (default: enabled)
   Summons and minions go through doors, athletics or mobility checks, and caves along with their masters.
2. **Enable Global Transitions** (default: enabled)
   Minions follow your party across loading screens and world map travel.

### Managing your minions

- **Natural lifespan**: summons still fade out after their normal spell duration. No change here.
- **Too many minions**: if you have a small army of undead and want to leave some behind, turn off either option before you move to a new area.
- **Lich ability**: you can always use "Cancel Repurpose" to dismiss reanimated minions by hand.

## Installation

1. Install **Unity Mod Manager** (UMM).
2. Download the latest release of **Summons Transition Fix**.
3. Unzip it into the `Mods` folder of your game, or install it through the UMM window.

## Building from source

You need the .NET SDK and the game installed. Tell the build where the game is, then build:

    dotnet build -c Release -p:GamePath="path to your game folder"

The zip is written to the `bin` folder.

## Credits

Developed by **LilithSeven**. Source code on [GitHub](https://github.com/LilithSeven/SummonsTransitionFix).
Licensed under the **MIT License**.
