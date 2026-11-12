using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhatCanISell
{
    /// <summary>One row in the window: a group of identical colony items the trader will buy.</summary>
    public class SellableEntry
    {
        public readonly TransferableOneWay transferable;
        public float marketValue;
        public float sellPrice;
        public float massEach;

        public SellableEntry(TransferableOneWay transferable)
        {
            this.transferable = transferable;
        }

        public int Available => transferable.GetMaximumToTransfer();
        public int Selected => transferable.CountToTransfer;
        public float TotalValueAvailable => sellPrice * Available;
    }

    public static class SellableStock
    {
        public static bool CanShowFor(Settlement settlement)
        {
            return settlement?.TraderKind != null
                && settlement.Faction != null
                && settlement.Faction != Faction.OfPlayer
                && !settlement.Faction.def.permanentEnemy;
        }

        /// <summary>
        /// Items the Form Caravan window could take from this map (same source vanilla uses),
        /// filtered to what this settlement's trader will buy, grouped like the caravan/trade UI.
        /// </summary>
        public static List<SellableEntry> Gather(Map map, Settlement settlement, Pawn negotiator)
        {
            var result = new List<SellableEntry>();
            TraderKindDef kind = settlement?.TraderKind;
            if (map == null || kind == null)
            {
                return result;
            }

            var groups = new List<TransferableOneWay>();
            foreach (Thing thing in CaravanFormingUtility.AllReachableColonyItems(map))
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner.def == ThingDefOf.Silver)
                {
                    continue; // silver is the currency, not something you sell
                }
                if (!kind.WillTrade(inner.def) || !TradeUtility.PlayerSellableNow(thing, settlement))
                {
                    continue;
                }
                TransferableOneWay group = TransferableUtility.TransferableMatching(thing, groups, TransferAsOneMode.PodsOrCaravanPacking);
                if (group == null)
                {
                    group = new TransferableOneWay();
                    groups.Add(group);
                }
                if (!group.things.Contains(thing))
                {
                    group.things.Add(thing);
                }
            }

            foreach (TransferableOneWay group in groups)
            {
                result.Add(new SellableEntry(group));
            }
            UpdatePrices(result, settlement, negotiator);
            return result;
        }

        public static void UpdatePrices(List<SellableEntry> entries, Settlement settlement, Pawn negotiator)
        {
            foreach (SellableEntry entry in entries)
            {
                Thing any = entry.transferable.AnyThing;
                Thing inner = any.GetInnerIfMinified();
                entry.marketValue = inner.MarketValue;
                entry.massEach = any.GetStatValue(StatDefOf.Mass);
                entry.sellPrice = EstimateSellPrice(inner, settlement, negotiator);
            }
        }

        /// <summary>Mirrors Tradeable.InitPriceDataIfNeeded (player-sells side) without needing a TradeSession.</summary>
        public static float EstimateSellPrice(Thing thing, Settlement settlement, Pawn negotiator)
        {
            TraderKindDef kind = settlement.TraderKind;
            TradeCurrency currency = settlement.TradeCurrency;

            float negotiatorGain = negotiator?.GetStatValue(StatDefOf.TradePriceImprovement) ?? 0f;
            float settlementGain = settlement.TradePriceImprovementOffsetForPlayer;
            float drugBonus = 0f;
            float animalProduceBonus = 0f;
            if (negotiator != null)
            {
                if (thing.def.IsNonMedicalDrug)
                {
                    drugBonus = negotiator.GetStatValue(StatDefOf.DrugSellPriceImprovement);
                }
                if (ModsConfig.IdeologyActive && (thing.def.IsLeather || thing.def.IsMeat || thing.def.IsWool))
                {
                    animalProduceBonus = negotiator.GetStatValue(StatDefOf.AnimalProductsSellImprovement);
                }
            }

            float sellFactor = kind.PriceTypeFor(thing.def, TradeAction.PlayerSells).PriceMultiplier();
            float sell = TradeUtility.GetPricePlayerSell(thing, sellFactor, 1f, negotiatorGain, settlementGain, drugBonus, animalProduceBonus, currency);

            if (currency == TradeCurrency.Silver)
            {
                float buyFactor = kind.PriceTypeFor(thing.def, TradeAction.PlayerBuys).PriceMultiplier();
                float buy = TradeUtility.GetPricePlayerBuy(thing, buyFactor, 1f, negotiatorGain, settlementGain);
                if (sell >= buy)
                {
                    sell = buy;
                }
            }
            return sell;
        }

        public static List<Pawn> NegotiatorCandidates(Map map)
        {
            if (map == null)
            {
                return new List<Pawn>();
            }
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => p.skills != null && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                .OrderByDescending(p => p.GetStatValue(StatDefOf.TradePriceImprovement))
                .ToList();
        }

        public static string FormatPrice(float amount, Settlement settlement)
        {
            if (settlement.TradeCurrency == TradeCurrency.Favor)
            {
                return "WCIS_Favor".Translate(amount.ToString("0.#"));
            }
            return amount.ToStringMoney();
        }
    }
}
