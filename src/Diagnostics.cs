using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;

namespace SummonsTransitionFix
{
    public static class Diagnostics
    {
        public static void ReportUnits(string moment)
        {
            if (!Main.WritesDiagnosticReport) return;

            try
            {
                var game = Game.Instance;
                var area = game.LoadedAreaState;
                var mainState = area?.MainState;
                var version = Assembly.GetExecutingAssembly().GetName().Version;

                Write($"{moment}. Mod version {version}. Area: {game.CurrentlyLoadedArea?.name ?? "none"}. Local setting: {Main.ModSettings?.EnableLocalTransitions}. Global setting: {Main.ModSettings?.EnableGlobalTransitions}.");

                var reported = 0;

                if (area != null && mainState != null)
                {
                    foreach (var state in TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates()))
                    {
                        reported += ReportState(state, state == mainState ? "main" : "additional");
                    }
                }

                var crossState = game.Player?.CrossSceneState;
                if (crossState != null)
                {
                    reported += ReportState(crossState, "cross-scene");
                }

                Write($"{moment}. {reported} unit(s) reported.");
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to write the diagnostic report: {ex}");
            }
        }

        private static int ReportState(SceneEntitiesState state, string kind)
        {
            var units = state.AllEntityData.OfType<UnitEntityData>().Where(Minions.IsWorthReporting).ToList();

            foreach (var unit in units)
            {
                var master = Minions.GetMinionMaster(unit);
                Write($"  [{kind}] scene={state.SceneName ?? "none"} unit={unit.CharacterName} blueprint={unit.Blueprint?.name ?? "none"} verdict={Minions.Classify(unit)} master={master?.CharacterName ?? "none"} inGame={unit.IsInGame} buffs=[{string.Join(", ", DescribeBuffs(unit))}]");
            }

            return units.Count;
        }

        private static IEnumerable<string> DescribeBuffs(UnitEntityData unit)
        {
            var buffs = unit.Buffs;
            if (buffs == null) yield break;

            foreach (var buff in buffs)
            {
                yield return $"{buff.Blueprint?.name ?? "none"} from {buff.Context?.MaybeCaster?.CharacterName ?? "nobody"}";
            }
        }

        private static void Write(string line)
        {
            Main.Logger?.Log($"[SummonsTransitionFix] [Diagnostic] {line}");
        }
    }
}
