using UnityModManagerNet;

namespace SummonsTransitionFix
{
    public class Settings : UnityModManager.ModSettings
    {
        
        public bool EnableLocalTransitions = true;

        
        public bool EnableGlobalTransitions = true;

        
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}