namespace SummonsTransitionFix
{
    public enum BondBuffKind
    {
        None,
        Servitude,
        EndOfServitude,
    }

    public static class LoyaltyRules
    {
        public const string RepurposeBuffUndeadId = "5e18ce2e21330e34690c372fbd9d6d60";
        public const string RepurposeEndBuffId = "4b16cf4b43ce40d08dbe9db4cefb63d1";
        public const string RepurposeForcedEndId = "9520c680f8584c3b8c165fae9f05278d";

        public const int MaxBlocksOfOneBuffPerFrame = 8;

        public static bool AllowsRemoval(
            BondBuffKind kind,
            bool bondIsProtected,
            bool removedByReleaseAbility,
            bool servitudeIsBeingActivated,
            bool insideEndOfServitudeActions)
        {
            if (kind == BondBuffKind.None || !bondIsProtected) return true;

            if (kind == BondBuffKind.EndOfServitude)
            {
                return removedByReleaseAbility || servitudeIsBeingActivated;
            }

            return insideEndOfServitudeActions;
        }

        public static bool RunsEndOfServitudeActions(bool bondIsProtected, bool endOfServitudeIsBeingRemoved, bool servitudeIsBeingActivated)
        {
            if (!bondIsProtected) return true;

            return endOfServitudeIsBeingRemoved && !servitudeIsBeingActivated;
        }

        public static bool StopsBlocking(int blocksOfThisBuffInThisFrame)
        {
            return blocksOfThisBuffInThisFrame > MaxBlocksOfOneBuffPerFrame;
        }
    }
}
