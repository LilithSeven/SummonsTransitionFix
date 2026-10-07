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
        public bool EnableMinionFormation = false;
        public bool EnableMinionSpeed = false;
        public float SummonFrontMargin = 2.5f;
        public float SummonBackMargin = 2.0f;
        public float SummonLateralSpacing = 2.4f;
        public float SummonCorridorWidth = 2.0f;
        public float SummonRepathDistance = 4.0f;
        public float SummonCatchUpFactor = 1.1f;
        public float RaisedFrontPush = 4.0f;
        public float RaisedLineGap = 2.8f;
        public float RaisedLateralSpacing = 2.4f;
        public float RaisedCorridorWidth = 1.8f;
        public bool EnablePartyAutoFormation = false;
        public float PartyFrontGap = 4.5f;
        public float PartyMidGap = 4.0f;
        public float PartyBackGap = 3.5f;
        public float PartyLateralSpacing = 2.6f;

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
