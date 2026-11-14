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
        Captive,
        Animal,
    }

    public readonly struct Buyer
    {
        public readonly WorldObject place;
        public readonly int tiles;

        public Buyer(WorldObject place, int tiles)
        {
            this.place = place;
            this.tiles = tiles;
        }
    }

    public class SellableEntry
    {
        public readonly TransferableOneWay transferable;
        public readonly SellableKind kind;
        public float marketValue;
        public float sellPrice;
        public float massEach;
        // Only set for things the current trader won't buy: other settlements that will, nearest first.
        public List<Buyer> buyers;
        private string label;

        // Nothing moves while the window has the game paused, so these are filled once per price update.
        public int available;
        public string availableText;
        public string marketText;
        public string priceText;

        public SellableEntry(TransferableOneWay transferable, SellableKind kind)
        {
            this.transferable = transferable;
            this.kind = kind;
        }

        public bool IsPawn => kind != SellableKind.Item;
        public int Selected => transferable.CountToTransfer;
        public float TotalValueAvailable => sellPrice * available;

        // Matches Tradeable_Pawn.Label for pawns.
        public string Label => label ??= BuildLabel();

        public string Tooltip
        {
            get
            {
                if (transferable.AnyThing is Pawn pawn)
                {
                    return Label + "\n\n" + pawn.MainDesc(writeFaction: true) + "\n\n" + pawn.DescriptionDetailed;
                }
                return Label + "\n\n" + transferable.TipDescription;
            }
        }

        private string BuildLabel()
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

    public static class SellableStock
    {
        // A place is any world object you can trade with: vanilla settlements, but also modded ones
        // that aren't Settlements, such as VFE Medieval 2's Merchant's guild camps.
        public static ITrader Trader(this WorldObject place)
        {
            return place as ITrader;
        }

        public static bool CanShowFor(WorldObject place)
        {
            return place.Trader()?.TraderKind != null
                && place.Faction != null
                && place.Faction != Faction.OfPlayer
                && !place.Faction.def.permanentEnemy;
        }

        public static bool CanSendCaravanTo(WorldObject place)
        {
            return CanShowFor(place) && !place.Faction.HostileTo(Faction.OfPlayer);
        }

        // Splits the colony's sellable stock into what this place buys and, in elsewhere, what it doesn't.
        public static List<SellableEntry> Gather(Map map, WorldObject place, Pawn negotiator, List<SellableEntry> elsewhere)
        {
            var result = new List<SellableEntry>();
            elsewhere.Clear();
            ITrader trader = place?.Trader();
            TraderKindDef kind = trader?.TraderKind;
            if (map == null || kind == null)
            {
                return result;
            }

            // Grouped with the same mode Dialog_FormCaravan uses, so each row maps onto one of its rows.
            var items = new Groups();
            var otherItems = new Groups();
            foreach (Thing thing in CaravanFormingUtility.AllReachableColonyItems(map))
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner.def == ThingDefOf.Silver || !TradeUtility.EverPlayerSellable(inner.def))
                {
                    continue;
                }
                if (kind.WillTrade(inner.def) && TradeUtility.PlayerSellableNow(thing, trader))
                {
                    items.Add(thing);
                }
                else
                {
                    otherItems.Add(thing);
                }
            }

            var sendable = new HashSet<Pawn>(Dialog_FormCaravan.AllSendablePawns(map, reform: false));
            var pawns = new Groups();
            var otherPawns = new Groups();
            foreach (Pawn pawn in TradeUtility.AllSellableColonyPawns(map, checkAcceptableTemperatureOfAnimals: false))
            {
                if (!sendable.Contains(pawn))
                {
                    continue;
                }
                if (kind.WillTrade(pawn.def) && TradeUtility.PlayerSellableNow(pawn, trader))
                {
                    pawns.Add(pawn);
                }
                else
                {
                    otherPawns.Add(pawn);
                }
            }

            AddEntries(result, items, pawns);
            AddEntries(elsewhere, otherItems, otherPawns);
            UpdatePrices(result, place, negotiator);
            return result;
        }

        private static void AddEntries(List<SellableEntry> into, Groups items, Groups pawns)
        {
            foreach (TransferableOneWay group in items.all)
            {
                into.Add(new SellableEntry(group, SellableKind.Item));
            }
            foreach (TransferableOneWay group in pawns.all)
            {
                Pawn pawn = (Pawn)group.AnyThing;
                into.Add(new SellableEntry(group, pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony ? SellableKind.Captive : SellableKind.Animal));
            }
        }

        // TransferableMatching only ever matches things of the same def, so bucketing by def
        // avoids comparing every thing against every group.
        private sealed class Groups
        {
            public readonly List<TransferableOneWay> all = new List<TransferableOneWay>();
            private readonly Dictionary<ThingDef, List<TransferableOneWay>> byDef = new Dictionary<ThingDef, List<TransferableOneWay>>();

            public void Add(Thing thing)
            {
                ThingDef def = thing.GetInnerIfMinified().def;
                if (!byDef.TryGetValue(def, out List<TransferableOneWay> groups))
                {
                    groups = new List<TransferableOneWay>();
                    byDef[def] = groups;
                }
                TransferableOneWay group = TransferableUtility.TransferableMatching(thing, groups, TransferAsOneMode.PodsOrCaravanPacking);
                if (group == null)
                {
                    group = new TransferableOneWay();
                    groups.Add(group);
                    all.Add(group);
                }
                if (!group.things.Contains(thing))
                {
                    group.things.Add(thing);
                }
            }
        }

        // Fills in buyers for the entries the current place won't buy and returns those that have any.
        public static List<SellableEntry> FindBuyers(Map map, WorldObject here, List<SellableEntry> candidates, int maxTiles)
        {
            var result = new List<SellableEntry>();
            if (map == null || candidates.Count == 0)
            {
                return result;
            }

            List<Buyer> nearby = NearbyTradingPlaces(map, here, maxTiles);
            if (nearby.Count == 0)
            {
                return result;
            }

            var willTrade = new Dictionary<(TraderKindDef, ThingDef), bool>();
            foreach (SellableEntry entry in candidates)
            {
                entry.buyers = null;
                Thing any = entry.transferable.AnyThing;
                ThingDef def = any.GetInnerIfMinified().def;
                foreach (Buyer buyer in nearby)
                {
                    ITrader trader = buyer.place.Trader();
                    TraderKindDef kind = trader.TraderKind;
                    if (!willTrade.TryGetValue((kind, def), out bool trades))
                    {
                        trades = kind.WillTrade(def);
                        willTrade[(kind, def)] = trades;
                    }
                    if (trades && TradeUtility.PlayerSellableNow(any, trader))
                    {
                        (entry.buyers ??= new List<Buyer>()).Add(buyer);
                    }
                }
                if (entry.buyers != null)
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        // Places a colonist could reach and trade with, within maxTiles of the colony, nearest first.
        private static List<Buyer> NearbyTradingPlaces(Map map, WorldObject here, int maxTiles)
        {
            var result = new List<Buyer>();
            PlanetTile origin = map.Tile;
            // A moving camp can stop on a settlement's tile, so a tile can hold more than one place.
            var byTile = new Dictionary<PlanetTile, List<WorldObject>>();
            int remaining = 0;
            foreach (WorldObject place in Find.WorldObjects.AllWorldObjects)
            {
                if (place != here && place.Tile.Layer == origin.Layer && CanSendCaravanTo(place))
                {
                    if (!byTile.TryGetValue(place.Tile, out List<WorldObject> atTile))
                    {
                        byTile[place.Tile] = atTile = new List<WorldObject>();
                    }
                    atTile.Add(place);
                    remaining++;
                }
            }
            if (remaining == 0)
            {
                return result;
            }

            // One breadth-first fill gives the traversal distance to every place in range.
            origin.Layer.Filler.FloodFill(origin, tile => !Find.World.Impassable(tile), new Predicate<PlanetTile, int>((tile, dist) =>
            {
                if (dist > maxTiles)
                {
                    return true;
                }
                if (byTile.TryGetValue(tile, out List<WorldObject> places))
                {
                    foreach (WorldObject place in places)
                    {
                        result.Add(new Buyer(place, dist));
                    }
                    remaining -= places.Count;
                    return remaining == 0;
                }
                return false;
            }), Find.WorldGrid.TilesNumWithinTraversalDistance(maxTiles + 1));

            List<Pawn> colonists = PotentialNegotiators(map);
            result.RemoveAll(b => !colonists.Any(p => p.CanTradeWith(b.place.Faction, b.place.Trader().TraderKind).Accepted));
            return result;
        }

        public static void UpdatePrices(List<SellableEntry> entries, WorldObject place, Pawn negotiator)
        {
            foreach (SellableEntry entry in entries)
            {
                Thing any = entry.transferable.AnyThing;
                Thing inner = any.GetInnerIfMinified();
                entry.marketValue = inner.MarketValue;
                // Caravan mass usage excludes pawns' own body mass.
                entry.massEach = entry.IsPawn ? 0f : any.GetStatValue(StatDefOf.Mass);
                WorldObject buyer = PricePlace(entry, place);
                entry.sellPrice = EstimateSellPrice(inner, buyer.Trader(), negotiator);
                entry.available = entry.transferable.GetMaximumToTransfer();
                entry.availableText = entry.available.ToStringCached();
                entry.marketText = entry.marketValue.ToStringMoney();
                entry.priceText = FormatPrice(entry.sellPrice, buyer);
            }
        }

        // Entries sold elsewhere are priced at their nearest buyer.
        public static WorldObject PricePlace(SellableEntry entry, WorldObject place)
        {
            return entry.buyers.NullOrEmpty() ? place : entry.buyers[0].place;
        }

        // Player-sells side of Tradeable.InitPriceDataIfNeeded, without a TradeSession.
        public static float EstimateSellPrice(Thing thing, ITrader trader, Pawn negotiator)
        {
            TraderKindDef kind = trader.TraderKind;
            TradeCurrency currency = trader.TradeCurrency;

            float negotiatorGain = negotiator?.GetStatValue(StatDefOf.TradePriceImprovement) ?? 0f;
            float settlementGain = trader.TradePriceImprovementOffsetForPlayer;
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
                sell = Mathf.Min(sell, buy);
            }
            return sell;
        }

        // Same rules vanilla uses to pick a caravan's negotiator (BestCaravanPawnUtility, CaravanVisitUtility),
        // limited to colonists the Form Caravan window can send.
        public static List<Pawn> NegotiatorCandidates(Map map, WorldObject place)
        {
            TraderKindDef kind = place.Trader().TraderKind;
            return PotentialNegotiators(map)
                .Where(p => p.CanTradeWith(place.Faction, kind).Accepted)
                .OrderByDescending(p => p.GetStatValue(StatDefOf.TradePriceImprovement))
                .ToList();
        }

        private static List<Pawn> PotentialNegotiators(Map map)
        {
            if (map == null)
            {
                return new List<Pawn>();
            }
            var sendable = new HashSet<Pawn>(Dialog_FormCaravan.AllSendablePawns(map, reform: false));
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => sendable.Contains(p)
                    && !p.Downed && !p.InMentalState
                    && !StatDefOf.TradePriceImprovement.Worker.IsDisabledFor(p))
                .ToList();
        }

        public static string NoNegotiatorReason(WorldObject place)
        {
            RoyalTitlePermitDef permit = place.Trader().TraderKind.permitRequiredForTrading;
            return permit != null
                ? "CommandTradeFailNeedPermit".Translate(permit.LabelCap)
                : "CommandTradeFailNoNegotiator".Translate();
        }

        public static string FormatPrice(float amount, WorldObject place)
        {
            if (place.Trader().TradeCurrency == TradeCurrency.Favor)
            {
                return "WCIS_Favor".Translate(amount.ToString("0.#"));
            }
            return amount.ToStringMoney();
        }
    }
}
