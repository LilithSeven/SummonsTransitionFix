using System.Collections.Generic;

namespace SummonsTransitionFix
{
    public static class TransitionRules
    {
        public static bool HandlesGlobalTransitions(bool enabled, bool? globalSetting)
        {
            return enabled && globalSetting == true;
        }

        public static bool HandlesAnyTransition(bool enabled, bool? localSetting, bool? globalSetting)
        {
            return enabled && (localSetting == true || globalSetting == true);
        }

        public static List<TState> SelectAreaStates<TState>(TState? mainState, IEnumerable<TState>? additionalStates) where TState : class
        {
            var states = new List<TState>();
            if (mainState != null)
            {
                states.Add(mainState);
            }
            return states;
        }
    }
}
