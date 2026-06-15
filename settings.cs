using UnityModManagerNet;

namespace SummonsTransitionFix
{
    public class Settings : UnityModManager.ModSettings
    {
        // Permet d'activer ou désactiver le fix sur les téléportations locales (portes, grottes)
        public bool EnableLocalTransitions = true;

        // Permet d'activer ou désactiver le fix sur les changements de zone globaux (écrans de chargement)
        public bool EnableGlobalTransitions = true;

        // Méthode de sauvegarde automatique appelée par UMM
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}