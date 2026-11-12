using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhatCanISell
{
    /// <summary>
    /// Trade-screen style list of colony items that a settlement will buy. Read-only as far as
    /// trading goes: the only action is pre-filling a Form Caravan window.
    /// </summary>
    public class Dialog_ThingsYouCanSell : Window
    {
        private readonly Settlement settlement;
        private readonly List<Map> colonyMaps;
        private Map map;
        private Pawn negotiator;
        private List<SellableEntry> entries = new List<SellableEntry>();
        private readonly List<SellableEntry> visible = new List<SellableEntry>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scrollPosition;
        private bool sortByValue = true;

        private const float RowHeight = 30f;
        private const float HeaderHeight = 24f;
        private const float BottomAreaHeight = 78f;
        private const float ControlsHeight = 28f;
        private const float ColTotal = 110f;
        private const float ColAdjust = 240f;
        private const float ColPrice = 110f;
        private const float ColMarket = 100f;
        private const float ColStorage = 80f;
        private const float IconX = 30f;
        private const float LabelX = 60f;
        private static readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);

        public override Vector2 InitialSize => new Vector2(980f, Mathf.Min(UI.screenHeight, 900));

        public Dialog_ThingsYouCanSell(Settlement settlement)
        {
            this.settlement = settlement;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;

            colonyMaps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            Map current = Find.CurrentMap;
            map = current != null && current.IsPlayerHome ? current : colonyMaps.FirstOrDefault();
            SetMap(map);
        }

        private void SetMap(Map newMap)
        {
            map = newMap;
            negotiator = SellableStock.NegotiatorCandidates(map).FirstOrDefault();
            entries = SellableStock.Gather(map, settlement, negotiator);
            scrollPosition = Vector2.zero;
        }

        private void SetNegotiator(Pawn pawn)
        {
            negotiator = pawn;
            SellableStock.UpdatePrices(entries, settlement, negotiator);
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(0f, y, inRect.width - 30f, 35f), "WCIS_Title".Translate(settlement.LabelCap));
            y += 36f;

            Text.Font = GameFont.Small;
            string subtitle = settlement.Faction.Name + " · " + settlement.TraderKind.LabelCap;
            Widgets.Label(new Rect(0f, y, inRect.width, 24f), subtitle);
            y += 26f;

            RoyalTitleDef titleRequired = settlement.TraderKind.TitleRequiredToTrade;
            if (titleRequired != null)
            {
                GUI.color = ColorLibrary.RedReadable;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), "WCIS_RequiresTitle".Translate(titleRequired.GetLabelCapForBothGenders()));
                GUI.color = Color.white;
                y += 26f;
            }

            DrawControls(new Rect(0f, y, inRect.width, ControlsHeight));
            y += ControlsHeight + 8f;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - BottomAreaHeight);
            DrawList(listRect);

            DrawBottom(new Rect(0f, inRect.height - BottomAreaHeight, inRect.width, BottomAreaHeight));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawControls(Rect rect)
        {
            float x = rect.x;
            if (colonyMaps.Count > 1 && map != null)
            {
                Rect colonyRect = new Rect(x, rect.y, 230f, rect.height);
                if (Widgets.ButtonText(colonyRect, "WCIS_Colony".Translate(map.Parent.LabelCap)))
                {
                    var options = colonyMaps.Select(m => new FloatMenuOption(m.Parent.LabelCap, () => SetMap(m))).ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                x += colonyRect.width + 8f;
            }

            Rect negotiatorRect = new Rect(x, rect.y, 290f, rect.height);
            string negotiatorLabel = negotiator != null
                ? "WCIS_Negotiator".Translate(NegotiatorLabel(negotiator)).ToString()
                : "WCIS_NoNegotiator".Translate().ToString();
            if (Widgets.ButtonText(negotiatorRect, negotiatorLabel))
            {
                List<Pawn> candidates = SellableStock.NegotiatorCandidates(map);
                if (candidates.Any())
                {
                    var options = candidates.Select(p => new FloatMenuOption(NegotiatorLabel(p), () => SetNegotiator(p))).ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            TooltipHandler.TipRegion(negotiatorRect, "WCIS_NegotiatorTip".Translate());
            x += negotiatorRect.width + 8f;

            Rect sortRect = new Rect(x, rect.y, 170f, rect.height);
            if (Widgets.ButtonText(sortRect, sortByValue ? "WCIS_SortByValue".Translate() : "WCIS_SortByName".Translate()))
            {
                sortByValue = !sortByValue;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            Rect searchRect = new Rect(rect.xMax - 250f, rect.y + 2f, 250f, QuickSearchWidget.WidgetHeight);
            search.OnGUI(searchRect, () => scrollPosition = Vector2.zero);
        }

        private static string NegotiatorLabel(Pawn pawn)
        {
            return pawn.LabelShortCap + " (" + pawn.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent() + ")";
        }

        private void RebuildVisible()
        {
            visible.Clear();
            foreach (SellableEntry entry in entries)
            {
                if (!entry.transferable.HasAnyThing)
                {
                    continue;
                }
                if (search.filter.Active && !search.filter.Matches(entry.transferable.Label) && !search.filter.Matches(entry.transferable.ThingDef.defName))
                {
                    continue;
                }
                visible.Add(entry);
            }
            if (sortByValue)
            {
                visible.SortByDescending(e => e.TotalValueAvailable);
            }
            else
            {
                visible.SortBy(e => e.transferable.Label);
            }
            search.noResultsMatched = search.filter.Active && visible.Count == 0;
        }

        private void DrawList(Rect rect)
        {
            if (map == null)
            {
                Widgets.Label(rect, "WCIS_NoColony".Translate());
                return;
            }

            float width = rect.width - 16f; // scrollbar
            Rect header = new Rect(rect.x, rect.y, width, HeaderHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(LabelX, header.y, ColumnStart(width, 4) - LabelX, HeaderHeight), "WCIS_ColItem".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(header, width, 4), "WCIS_ColInStorage".Translate());
            Widgets.Label(ColumnRect(header, width, 3), "WCIS_ColMarketValue".Translate());
            Rect priceHeader = ColumnRect(header, width, 2);
            Widgets.Label(priceHeader, "WCIS_ColSellPrice".Translate());
            TooltipHandler.TipRegion(priceHeader, "WCIS_SellPriceTip".Translate());
            Widgets.Label(ColumnRect(header, width, 0), "WCIS_ColTotal".Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x, header.yMax + 1f, width);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect outRect = new Rect(rect.x, header.yMax + 4f, rect.width, rect.height - HeaderHeight - 4f);
            RebuildVisible();
            if (visible.Count == 0)
            {
                if (!search.filter.Active)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, 60f), "WCIS_Nothing".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                return;
            }

            Rect viewRect = new Rect(0f, 0f, width, visible.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float minY = scrollPosition.y - RowHeight;
            float maxY = scrollPosition.y + outRect.height;
            for (int i = 0; i < visible.Count; i++)
            {
                float rowY = i * RowHeight;
                if (rowY > minY && rowY < maxY)
                {
                    DrawRow(new Rect(0f, rowY, width, RowHeight), visible[i], i);
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawRow(Rect row, SellableEntry entry, int index)
        {
            TransferableOneWay trad = entry.transferable;
            Thing thing = trad.AnyThing;
            float width = row.width;

            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(row);
            }
            Widgets.DrawHighlightIfMouseover(row);

            Widgets.InfoCardButton(row.x + 2f, row.y + (row.height - 24f) / 2f, thing);
            Widgets.ThingIcon(new Rect(IconX, row.y + (row.height - 27f) / 2f, 27f, 27f), thing);

            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(LabelX, row.y, ColumnStart(width, 4) - LabelX - 6f, row.height);
            Widgets.Label(labelRect, trad.LabelCap.Truncate(labelRect.width));
            TooltipHandler.TipRegion(labelRect, trad.LabelCap + "\n\n" + trad.TipDescription);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(row, width, 4), entry.Available.ToStringCached());
            Widgets.Label(ColumnRect(row, width, 3), entry.marketValue.ToStringMoney());
            Rect priceRect = ColumnRect(row, width, 2);
            Widgets.Label(priceRect, SellableStock.FormatPrice(entry.sellPrice, settlement));
            TooltipHandler.TipRegion(priceRect, "WCIS_SellPriceTip".Translate());

            Text.Anchor = TextAnchor.UpperLeft;
            Rect adjustRect = ColumnRect(row, width, 1);
            TransferableUIUtility.DoCountAdjustInterface(adjustRect, trad, index, 0, entry.Available);

            Text.Anchor = TextAnchor.MiddleRight;
            if (entry.Selected > 0)
            {
                Widgets.Label(ColumnRect(row, width, 0), SellableStock.FormatPrice(entry.sellPrice * entry.Selected, settlement));
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(ColumnRect(row, width, 0), "-");
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Columns counted from the right: 0 total, 1 count adjuster, 2 sell price, 3 market value, 4 in storage.
        private static readonly float[] ColumnWidths = { ColTotal, ColAdjust, ColPrice, ColMarket, ColStorage };

        private static float ColumnStart(float width, int column)
        {
            float x = width;
            for (int i = 0; i <= column; i++)
            {
                x -= ColumnWidths[i];
            }
            return x;
        }

        private static Rect ColumnRect(Rect row, float width, int column)
        {
            float start = row.x + ColumnStart(width, column);
            float colWidth = ColumnWidths[column];
            // Keep a little breathing room between right-aligned number columns.
            return column == 1
                ? new Rect(start, row.y, colWidth, row.height)
                : new Rect(start, row.y, colWidth - 8f, row.height);
        }

        private void DrawBottom(Rect rect)
        {
            int count = 0;
            float mass = 0f;
            float value = 0f;
            foreach (SellableEntry entry in entries)
            {
                int selected = entry.Selected;
                if (selected <= 0)
                {
                    continue;
                }
                count += selected;
                mass += entry.massEach * selected;
                value += entry.sellPrice * selected;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 26f),
                "WCIS_Summary".Translate(count.ToStringCached(), mass.ToString("0.#"), SellableStock.FormatPrice(value, settlement)));
            Text.Anchor = TextAnchor.UpperLeft;

            float buttonY = rect.yMax - BottomButtonSize.y;
            if (Widgets.ButtonText(new Rect(rect.x, buttonY, BottomButtonSize.x, BottomButtonSize.y), "CloseButton".Translate()))
            {
                Close();
            }

            float midX = rect.center.x;
            if (Widgets.ButtonText(new Rect(midX - BottomButtonSize.x - 5f, buttonY, BottomButtonSize.x, BottomButtonSize.y), "WCIS_SelectAll".Translate()))
            {
                RebuildVisible();
                foreach (SellableEntry entry in visible)
                {
                    entry.transferable.AdjustTo(entry.Available);
                }
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(new Rect(midX + 5f, buttonY, BottomButtonSize.x, BottomButtonSize.y), "WCIS_Clear".Translate()))
            {
                foreach (SellableEntry entry in entries)
                {
                    entry.transferable.AdjustTo(0);
                }
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }

            Rect formRect = new Rect(rect.xMax - BottomButtonSize.x, buttonY, BottomButtonSize.x, BottomButtonSize.y);
            if (Widgets.ButtonText(formRect, "WCIS_FormCaravan".Translate(), active: map != null))
            {
                FormCaravan();
            }
        }

        private void FormCaravan()
        {
            if (map == null)
            {
                return;
            }
            var selections = entries
                .Where(e => e.Selected > 0)
                .Select(e => (e.transferable.things.ToList(), e.Selected))
                .ToList();
            Close(doCloseSound: false);
            FormCaravanPrefill.Open(map, settlement, selections);
        }
    }
}
