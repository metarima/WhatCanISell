using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhatCanISell
{
    /// <summary>
    /// Opens the vanilla Form Caravan window and, once it has built its own item list,
    /// sets the chosen counts and adds the settlement as the route destination.
    /// </summary>
    public static class FormCaravanPrefill
    {
        private class Pending
        {
            public Dialog_FormCaravan dialog;
            public Settlement destination;
            public List<(List<Thing> things, int count)> selections;
        }

        private static Pending pending;

        private static readonly AccessTools.FieldRef<Dialog_FormCaravan, bool> AutoSelectTravelSupplies =
            AccessTools.FieldRefAccess<Dialog_FormCaravan, bool>("autoSelectTravelSupplies");

        private static readonly Action<Dialog_FormCaravan> NotifyTransferablesChanged =
            AccessTools.MethodDelegate<Action<Dialog_FormCaravan>>(AccessTools.Method(typeof(Dialog_FormCaravan), "Notify_TransferablesChanged"));

        public static void Open(Map map, Settlement destination, List<(List<Thing> things, int count)> selections)
        {
            var dialog = new Dialog_FormCaravan(map);
            pending = new Pending { dialog = dialog, destination = destination, selections = selections };
            Find.WindowStack.Add(dialog);
        }

        internal static void TryApply(Dialog_FormCaravan dialog)
        {
            if (pending == null || pending.dialog != dialog)
            {
                return;
            }
            Pending p = pending;
            pending = null;

            try
            {
                Apply(dialog, p);
            }
            catch (Exception e)
            {
                Log.Error(WhatCanISellMod.LogPrefix + "Failed to pre-fill the caravan: " + e);
            }
        }

        private static void Apply(Dialog_FormCaravan dialog, Pending p)
        {
            var wanted = new Dictionary<TransferableOneWay, int>();
            foreach ((List<Thing> things, int count) in p.selections)
            {
                TransferableOneWay target = null;
                foreach (Thing thing in things)
                {
                    target = dialog.transferables.FirstOrDefault(t => t.things.Contains(thing));
                    if (target != null)
                    {
                        break;
                    }
                }
                if (target == null && things.Count > 0)
                {
                    target = TransferableUtility.TransferableMatching(things[0], dialog.transferables, TransferAsOneMode.PodsOrCaravanPacking);
                }
                if (target == null)
                {
                    continue; // item was moved/used since the window was opened
                }
                wanted.TryGetValue(target, out int existing);
                wanted[target] = existing + count;
            }

            bool selectedTravelSupplies = false;
            foreach (KeyValuePair<TransferableOneWay, int> kv in wanted)
            {
                kv.Key.AdjustTo(kv.Key.ClampAmount(kv.Value));
                selectedTravelSupplies |= IsAutoSelectedTravelSupply(kv.Key);
            }

            // Vanilla's automatic travel supplies would reset food/medicine/beds to its own picks,
            // wiping out what the player chose to sell, so turn it off in that case.
            if (selectedTravelSupplies && AutoSelectTravelSupplies(dialog))
            {
                AutoSelectTravelSupplies(dialog) = false;
                Messages.Message("WCIS_AutoSuppliesOff".Translate(), MessageTypeDefOf.CautionInput, historical: false);
            }
            NotifyTransferablesChanged(dialog);
            SetDestination(dialog, p.destination);
        }

        /// <summary>
        /// Sets the route straight to the settlement, skipping the world route planner.
        /// The destination only reaches the dialog when the planner's Accept button is pressed;
        /// leaving the planner any other way (Escape, or mods like Instant Caravan that send
        /// without checking for a route) would otherwise form a caravan with no destination.
        /// </summary>
        private static void SetDestination(Dialog_FormCaravan dialog, Settlement destination)
        {
            if (destination == null || destination.Destroyed)
            {
                return;
            }
            PlanetTile from = dialog.CurrentTile;
            PlanetTile to = destination.Tile;
            if (to.Layer != from.Layer || !Find.WorldReachability.CanReach(from, to))
            {
                return; // leave vanilla's route planner open so the player can pick a route
            }

            WorldRoutePlanner planner = Find.WorldRoutePlanner;
            if (planner.FormingCaravan)
            {
                planner.Stop();
            }
            if (!Find.WindowStack.IsOpen(dialog))
            {
                Find.WindowStack.Add(dialog);
            }
            dialog.Notify_ChoseRoute(to);
        }

        /// <summary>Same categories Dialog_FormCaravan.SelectApproximateBestTravelSupplies resets.</summary>
        private static bool IsAutoSelectedTravelSupply(TransferableOneWay t)
        {
            if (!t.HasAnyThing || t.ThingDef.category == ThingCategory.Pawn)
            {
                return false;
            }
            ThingDef def = t.ThingDef;
            if (!def.thingCategories.NullOrEmpty() && def.thingCategories.Contains(ThingCategoryDefOf.Medicine))
            {
                return true;
            }
            if (def.IsIngestible && !def.IsDrug && !def.IsCorpse)
            {
                return true;
            }
            if (ModsConfig.BiotechActive && def == ThingDefOf.HemogenPack)
            {
                return true;
            }
            return t.AnyThing.GetInnerIfMinified() is Building_Bed bed && bed.def.building.bed_caravansCanUse;
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.PostOpen))]
    public static class Patch_Dialog_FormCaravan_PostOpen
    {
        public static void Postfix(Dialog_FormCaravan __instance)
        {
            FormCaravanPrefill.TryApply(__instance);
        }
    }
}
