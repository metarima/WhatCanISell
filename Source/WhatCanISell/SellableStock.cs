using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WhatCanISell
{
    public enum SellableKind
    {
        Item,
        Captive, // prisoners and slaves of the colony
        Animal,
    }

    /// <summary>
    /// One row in the window: a group of identical colony items, or a single prisoner, slave
    /// or animal, that the trader will buy.
    /// </summary>
    public class SellableEntry
    {
        public readonly TransferableOneWay transferable;
        public readonly SellableKind kind;
        public float marketValue;
        public float sellPrice;
        public float massEach;

        public SellableEntry(TransferableOneWay transferable, SellableKind kind)
        {
            this.transferable = transferable;
            this.kind = kind;
        }

        public bool IsPawn => kind != SellableKind.Item;
        public int Available => transferable.GetMaximumToTransfer();
        public int Selected => transferable.CountToTransfer;
        public float TotalValueAvailable => sellPrice * Available;

        /// <summary>Same label format as the vanilla trade screen (Tradeable_Pawn for pawns).</summary>
        public string Label
        {
            get
            {
                if (!(transferable.AnyThing is Pawn pawn))
                {
                    return transferable.LabelCap;
                }
                string text = pawn.LabelNoCount;
                if (pawn.Name != null && !pawn.Name.Numerical && !pawn.RaceProps.Humanlike)
                {
                    text += ", " + pawn.def.label;
                }
                text += " (" + pawn.GetGenderLabel() + ", " + Mathf.FloorToInt(pawn.ageTracker.AgeBiologicalYearsFloat) + ")";
                if (kind == SellableKind.Captive)
                {
                    text += " - " + (pawn.IsSlave ? "WCIS_Slave" : "WCIS_Prisoner").Translate();
                }
                return text.CapitalizeFirst();
            }
        }

        public string Tooltip
        {
            get
            {
                if (transferable.AnyThing is Pawn pawn)
                {
                    return Label + "\n\n" + pawn.MainDesc(writeFaction: true) + "\n\n" + pawn.DescriptionDetailed;
                }
                return transferable.LabelCap + "\n\n" + transferable.TipDescription;
            }
        }
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
        /// grouped like the caravan/trade UI, plus sellable prisoners, slaves and animals,
        /// all filtered to what this settlement's trader will buy.
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
                result.Add(new SellableEntry(group, SellableKind.Item));
            }

            // Prisoners, slaves and animals: the pool vanilla offers to traders (secure captives;
            // tame, healthy, non-berserk animals), limited to pawns the Form Caravan window will
            // actually list, so everything shown here can be pre-filled. Each pawn is its own row.
            var caravanSendable = new HashSet<Pawn>(Dialog_FormCaravan.AllSendablePawns(map, reform: false));
            foreach (Pawn pawn in TradeUtility.AllSellableColonyPawns(map, checkAcceptableTemperatureOfAnimals: false))
            {
                if (!caravanSendable.Contains(pawn) || !kind.WillTrade(pawn.def) || !TradeUtility.PlayerSellableNow(pawn, settlement))
                {
                    continue;
                }
                var single = new TransferableOneWay();
                single.things.Add(pawn);
                SellableKind pawnKind = pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony ? SellableKind.Captive : SellableKind.Animal;
                result.Add(new SellableEntry(single, pawnKind));
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
                // Pawns walk with the caravan; vanilla's mass usage doesn't count their body mass.
                entry.massEach = entry.IsPawn ? 0f : any.GetStatValue(StatDefOf.Mass);
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

            // With Ideology, humanlike captives sell at 0.6x, and the buy-side cap uses 0.6x for
            // pawns that would join as slaves (same factors as Tradeable.InitPriceDataIfNeeded).
            float humanPawnSellFactor = 1f;
            float joinAsBuyFactor = 1f;
            if (ModsConfig.IdeologyActive && thing is Pawn pawn && pawn.RaceProps.Humanlike && pawn.guest != null)
            {
                humanPawnSellFactor = 0.6f;
                if (pawn.guest.joinStatus == JoinStatus.JoinAsSlave)
                {
                    joinAsBuyFactor = 0.6f;
                }
            }

            float sellFactor = kind.PriceTypeFor(thing.def, TradeAction.PlayerSells).PriceMultiplier();
            float sell = TradeUtility.GetPricePlayerSell(thing, sellFactor, humanPawnSellFactor, negotiatorGain, settlementGain, drugBonus, animalProduceBonus, currency);

            if (currency == TradeCurrency.Silver)
            {
                float buyFactor = kind.PriceTypeFor(thing.def, TradeAction.PlayerBuys).PriceMultiplier();
                float buy = TradeUtility.GetPricePlayerBuy(thing, buyFactor, joinAsBuyFactor, negotiatorGain, settlementGain);
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
