using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace SummonsTransitionFix
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableLocalTransitions = true;

        public bool EnableGlobalTransitions = true;

        public bool EnableDiagnosticLog = false;

        public KeyCode CreatureListKey = KeyCode.F9;

        public bool CreatureListKeyNeedsCtrl = true;

        public bool CreatureListKeyNeedsShift = false;

        public bool CreatureListKeyNeedsAlt = false;

        public List<StayEntry> StayingCreatures = new List<StayEntry>();

        public List<StaySnapshot> StaySnapshots = new List<StaySnapshot>();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
