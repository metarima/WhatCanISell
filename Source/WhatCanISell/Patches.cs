using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WhatCanISell
{
    // Adds the button to every world object you can trade with, not just Settlement: modded places such as
    // VFE Medieval 2's Merchant's guild override GetGizmos themselves, so a Settlement patch never sees them.
    [HarmonyPatch]
    public static class Patch_TradingPlace_GetGizmos
    {
        private static readonly Dictionary<Type, MethodInfo> MostDerived = new Dictionary<Type, MethodInfo>();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new HashSet<MethodInfo>();
            foreach (Type type in GenTypes.AllSubclassesNonAbstract(typeof(WorldObject)))
            {
                try
                {
                    if (typeof(ITrader).IsAssignableFrom(type))
                    {
                        methods.Add(GizmosMethod(type));
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(WhatCanISellMod.LogPrefix + "Skipped " + type.FullName + ": " + e.Message);
                }
            }
            methods.Remove(null);
            return methods;
        }

        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, WorldObject __instance, MethodBase __originalMethod)
        {
            // Overrides that call base would otherwise add the button twice.
            bool show = Equals(GizmosMethod(__instance.GetType()), __originalMethod) && SellableStock.CanShowFor(__instance);
            bool added = false;
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
                if (show && !added && gizmo is Command_Action cmd && cmd.icon == Settlement.ShowSellableItemsCommand)
                {
                    added = true;
                    yield return MakeCommand(__instance);
                }
            }
            if (show && !added)
            {
                yield return MakeCommand(__instance);
            }
        }

        // The GetGizmos that actually runs for this type.
        private static MethodInfo GizmosMethod(Type type)
        {
            if (!MostDerived.TryGetValue(type, out MethodInfo method))
            {
                method = AccessTools.Method(type, nameof(WorldObject.GetGizmos), Type.EmptyTypes);
                MostDerived[type] = method;
            }
            return method;
        }

        private static Command_Action MakeCommand(WorldObject place)
        {
            return new Command_Action
            {
                defaultLabel = "WCIS_CommandLabel".Translate(),
                defaultDesc = "WCIS_CommandDesc".Translate(),
                icon = Textures.ThingsYouCanSell,
                action = () => Find.WindowStack.Add(new Dialog_ThingsYouCanSell(place)),
            };
        }
    }

    [HarmonyPatch(typeof(Dialog_SellableItems), "DoBottomButtons")]
    public static class Patch_Dialog_SellableItems_DoBottomButtons
    {
        private const float ButtonWidth = 200f;
        private const float ButtonHeight = 40f;

        public static void Postfix(Dialog_SellableItems __instance, Rect rect, ITrader ___trader)
        {
            if (!(___trader is WorldObject place) || !SellableStock.CanShowFor(place))
            {
                return;
            }
            Rect button = new Rect(rect.width - ButtonWidth, rect.height - 55f, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(button, "WCIS_CommandLabel".Translate()))
            {
                __instance.Close();
                Find.WindowStack.Add(new Dialog_ThingsYouCanSell(place));
            }
        }
    }
}
