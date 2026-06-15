
This is a mod for **Pathfinder: Wrath of the Righteous** that makes sure your summoned creatures and reanimated minions (from Lich Repurpose) actually stick with your party through both local and global area transitions.

Tested with **Pathfinder: WotR 2.7.0x** and **Unity Mod Manager (UMM) 0.32.4**.

---


Sometimes, temporary minions are tied to the spot where you summoned them.
- **Local Transitions (doors, caves)**: Sometimes, only your main party members get teleported. Your summons get stuck on the other side of whatever barrier you just went through.
- **Global Transitions (loading screens, world map)**: The game wipes anything that’s not a companion from active cross-scene data.


**Summons Transition Fix** bumps your active minions into the persistent cross-scene buffer during transitions, then slips them right back in when the new area loads, so they pop up at their master’s side.

### Key Features
- **Universal Minion Detection**: Picks up normal summons, Lich-reanimated undead, and other converted allies automatically.
- **State Integrity Shield**: Stops the engine from stripping away buffs during transition tunnels, so reanimated undead don’t lose their templates or flip back to hostile factions.
- **Heavy Load Protection**: Tested with up to 50 active minions at the same time. I tried my best to makes the mod dodges Owlcat’s built-in marching order calculator for temporary minions, which keeps things running smooth and avoids crash.
- **UI & Save Game Safe**: Doesn’t mess with your UI, and doesn’t write any weird data to your save files. You can install or remove it any time during a playthrough—no worries.

Let me know if you find any issues
---

## Settings & Gameplay Customization

With the Unity Mod Manager (UMM) menu (`Ctrl + F10`), you can tweak these settings:

1. **Enable Local Transitions** (Default: *Enabled*)
   - Makes sure summons and minions jump through doors, athletic or mobility checks, or caves right alongside their masters.
2. **Enable Global Transitions** (Default: *Enabled*)
   - Minions follow your party across big zone loads and world map travel.

### Minion Management for Liches and Summoners
- **Natural Lifespan**: Summons still fade out after their normal spell duration. No change here.
- **Minion Overcrowding**: If you’ve got a small army of undead and need to leave some behind, just turn off either transition option in the UMM menu before you move to a new area.
- **Lich Special Ability**: You can always use the Lich’s “Cancel Repurpose” ability to dismiss reanimated minions by hand.

---

## Installation
1. Install **Unity Mod Manager** (UMM).
2. Download the latest release of **Summons Transition Fix**.
3. Unzip it into your `<GamePath>/Mods/` folder, or just install it through the UMM GUI.

## Source Code & Credits
Developed by **LilithSeven**. You’ll find the source code and development logs on [GitHub](https://github.com/LilithSeven/SummonsTransitionFix).
Licensed under the **MIT License**.