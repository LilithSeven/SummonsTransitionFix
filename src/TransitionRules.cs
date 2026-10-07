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

        public static bool WritesDiagnosticReport(bool enabled, bool? diagnosticSetting)
        {
            return enabled && diagnosticSetting == true;
        }

        public static bool IsOptionOn(bool enabled, bool? optionSetting)
        {
            return enabled && optionSetting == true;
        }

        public static bool CanBeToldToStay(MinionVerdict verdict)
        {
            return verdict == MinionVerdict.BoundByServitude;
        }

        public static bool EndsStayOrder(MinionVerdict verdict)
        {
            return verdict == MinionVerdict.Dead;
        }

        public static bool IsTakenAlong(MinionVerdict verdict, bool staysInArea)
        {
            if (verdict == MinionVerdict.SummonedByParty) return true;

            return verdict == MinionVerdict.BoundByServitude && !staysInArea;
        }

        public static List<TState> SelectAreaStates<TState>(TState? mainState, IEnumerable<TState>? additionalStates) where TState : class
        {
            var states = new List<TState>();
            if (mainState == null)
            {
                return states;
            }

            states.Add(mainState);

            if (additionalStates == null)
            {
                return states;
            }

            foreach (var state in additionalStates)
            {
                if (state != null && !states.Contains(state))
                {
                    states.Add(state);
                }
            }

            return states;
        }
    }
}
