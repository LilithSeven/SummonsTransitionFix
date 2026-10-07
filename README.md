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
- **You choose who stays**: a list of your raised creatures lets you leave some of them in an area and take them back later.
- **Optional formation settings**: summons and raised creatures can take a place in the party formation instead of trailing behind. Disabled by default.
- **Light on your saves**: the mod adds no data of its own to your save files.

If you run into a problem, let me know on the Nexus page. The place and the creature involved help a lot.

## Settings

Open the Unity Mod Manager menu (`Ctrl + F10`):

1. **Fix local transitions** (default: enabled)
   Summons and minions go through doors, athletics or mobility checks, and caves along with their masters.
2. **Fix global transitions** (default: enabled)
   Minions follow your party across loading screens and world map travel.
3. **Diagnostic report** (default: disabled)
   Writes what the mod sees at each transition to the Unity Mod Manager log. If a creature refuses to follow you, turn it on, go through one loading screen with that creature, and send me the lines marked `[Diagnostic]`.

### The list of raised creatures

Press `Ctrl + F9` to open the list of the creatures you raised with a Lich spell in the current area. You can change the shortcut in the mod settings, where the same list is shown. For each creature, choose **Comes with you** or **Stays in this area**.

- A creature that stays simply waits where you left it. Go back to that area and tick it again to take it with you.
- Creatures left in other areas are listed with the name of the area. The **Forget** button cancels the order.
- The list is kept in the mod's own settings file and follows your saves: loading an older save brings back the choices you had made at that time.
- A creature left in an area you can never visit again is lost, as it would be without the mod.

### Formation (optional)

Three settings, all disabled by default. Each one comes with fine tuning sliders and a button to restore the default values.

1. **Summons and raised creatures take a place in the formation**
   Melee creatures walk ahead of the party and the others behind it, instead of trailing behind their master.
2. **Slow summons keep up with their master**
   A summon slower than its master moves at its master's speed while it follows.
3. **Smarter automatic formation for the party**
   Only when the party uses the automatic formation of the game. Four lines: tanks, melee fighters, support, ranged. This is the only setting that concerns your companions: it changes where they stand in the formation, and nothing is written to the save.

### Managing your minions

- **Natural lifespan**: summons still fade out after their normal spell duration. No change here.
- **Too many minions**: use the list to leave raised creatures in an area. To leave everything behind, summons included, turn off the two transition options before you move to a new area.
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
