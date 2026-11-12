using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhatCanISell
{
    public partial class Dialog_ThingsYouCanSell : Window
    {
        private readonly Settlement settlement;
        private readonly List<Map> colonyMaps;
        private Map map;
        private Pawn negotiator;
        private string negotiatorLabel;
        private List<SellableEntry> entries = new List<SellableEntry>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scrollPosition;
        private bool sortByValue = true;
        private Tab tab = Tab.All;
        private readonly Dictionary<string, string> truncated = new Dictionary<string, string>();

        // Rebuilt by Refresh() only when the list, tab, search, sort or prices change.
        private bool dirty = true;
        private readonly List<SellableEntry> visible = new List<SellableEntry>();
        private readonly List<TabRecord> tabs = new List<TabRecord>();
        private string buysLabel;
        private string wontBuyLabel;

        private enum Tab
        {
            All,
            Items,
            Captives,
            Animals,
        }

        private static readonly (Tab tab, string key)[] TabKeys =
        {
            (Tab.All, "WCIS_TabAll"),
            (Tab.Items, "WCIS_TabItems"),
            (Tab.Captives, "WCIS_TabCaptives"),
            (Tab.Animals, "WCIS_TabAnimals"),
        };

        private const float RowHeight = 30f;
        private const float TabsHeight = 32f;
        private const float ModesHeight = 32f;
        private const float HeaderHeight = 24f;
        private const float BottomAreaHeight = 78f;
        private const float ControlsHeight = 28f;
        private const float SearchWidth = 220f;
        private const float IconX = 30f;
        private const float LabelX = 60f;
        private static readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);
        private static readonly Color DisabledButtonColor = new Color(1f, 1f, 1f, 0.35f);

        // Right to left: total, count adjuster, sell price, market value, available.
        private static readonly Columns SellColumns = new Columns(90f, 240f, 100f, 100f, 90f);
        private const int ColTotal = 0, ColAdjust = 1, ColPrice = 2, ColMarket = 3, ColAvailable = 4;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth, 980f), Mathf.Min(UI.screenHeight, 900f));

        private List<SellableEntry> CurrentList => showElsewhere ? elsewhere : entries;

        public Dialog_ThingsYouCanSell(Settlement settlement)
        {
            this.settlement = settlement;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;

            colonyMaps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            Map current = Find.CurrentMap;
            SetMap(current != null && current.IsPlayerHome ? current : colonyMaps.FirstOrDefault());
        }

        private void SetMap(Map newMap)
        {
            map = newMap;
            // Travel times are measured from the colony, so they don't carry over to another one.
            travelTimes.Clear();
            negotiator = SellableStock.NegotiatorCandidates(map, settlement).FirstOrDefault();
            negotiatorLabel = null;
            entries = SellableStock.Gather(map, settlement, negotiator, elsewhereCandidates);
            UpdateBuyers();
            scrollPosition = Vector2.zero;
        }

        private void SetNegotiator(Pawn pawn)
        {
            negotiator = pawn;
            negotiatorLabel = null;
            SellableStock.UpdatePrices(entries, settlement, negotiator);
            SellableStock.UpdatePrices(elsewhere, settlement, negotiator);
            dirty = true;
        }

        private void Refresh()
        {
            dirty = false;

            visible.Clear();
            foreach (SellableEntry entry in CurrentList)
            {
                if (entry.transferable.HasAnyThing && InTab(entry, tab) && MatchesSearch(entry))
                {
                    visible.Add(entry);
                }
            }
            if (sortByValue)
            {
                visible.SortByDescending(e => e.TotalValueAvailable);
            }
            else
            {
                visible.SortBy(e => e.Label);
            }
            search.noResultsMatched = search.filter.Active && visible.Count == 0;

            tabs.Clear();
            foreach ((Tab which, string key) in TabKeys)
            {
                int count = CurrentList.Count(e => InTab(e, which));
                tabs.Add(new TabRecord(key.Translate() + " (" + count + ")", () =>
                {
                    tab = which;
                    scrollPosition = Vector2.zero;
                    dirty = true;
                }, tab == which));
            }

            buysLabel = "WCIS_ModeBuys".Translate(settlement.LabelCap, entries.Count);
            wontBuyLabel = "WCIS_ModeWontBuy".Translate(settlement.LabelCap, elsewhere.Count);
        }

        private static bool InTab(SellableEntry entry, Tab which)
        {
            switch (which)
            {
                case Tab.Items: return entry.kind == SellableKind.Item;
                case Tab.Captives: return entry.kind == SellableKind.Captive;
                case Tab.Animals: return entry.kind == SellableKind.Animal;
                default: return true;
            }
        }

        private bool MatchesSearch(SellableEntry entry)
        {
            return !search.filter.Active || search.filter.Matches(entry.Label) || search.filter.Matches(entry.transferable.ThingDef.defName);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (dirty)
            {
                Refresh();
            }

            float y = 0f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(0f, y, inRect.width - 30f, 35f), "WCIS_Title".Translate(settlement.LabelCap));
            y += 36f;

            Text.Font = GameFont.Small;
            string subtitle = settlement.Faction.Name;
            string traderLabel = settlement.TraderKind.LabelCap;
            if (!traderLabel.NullOrEmpty())
            {
                subtitle += " · " + traderLabel;
            }
            DrawLine(ref y, inRect.width, subtitle, Color.gray);
            if (!SellableStock.CanSendCaravanTo(settlement))
            {
                DrawLine(ref y, inRect.width, "WCIS_HostileBanner".Translate(), ColorLibrary.RedReadable);
            }
            RoyalTitleDef titleRequired = settlement.TraderKind.TitleRequiredToTrade;
            if (titleRequired != null)
            {
                DrawLine(ref y, inRect.width, "WCIS_RequiresTitle".Translate(titleRequired.GetLabelCapForBothGenders()),
                    negotiator == null ? ColorLibrary.RedReadable : Color.gray);
            }

            y += 6f;
            DrawControls(new Rect(0f, y, inRect.width, ControlsHeight));
            y += ControlsHeight + 10f;
            DrawModes(new Rect(0f, y, inRect.width, ModesHeight));
            y += ModesHeight + 8f + TabsHeight;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - BottomAreaHeight);
            Widgets.DrawMenuSection(listRect);
            TabDrawer.DrawTabs(listRect, tabs);
            Rect inner = listRect.ContractedBy(4f);
            if (map == null)
            {
                Widgets.Label(inner, "WCIS_NoColony".Translate());
            }
            else if (showElsewhere)
            {
                DrawWontBuyList(inner);
            }
            else
            {
                DrawSellList(inner);
            }

            DrawBottom(new Rect(0f, inRect.height - BottomAreaHeight, inRect.width, BottomAreaHeight));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawLine(ref float y, float width, string text, Color color)
        {
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, width, 24f), text);
            GUI.color = Color.white;
            y += 24f;
        }

        private void DrawControls(Rect rect)
        {
            float x = rect.x;
            if (colonyMaps.Count > 1 && map != null)
            {
                Rect colonyRect = new Rect(x, rect.y, 230f, rect.height);
                if (Widgets.ButtonText(colonyRect, "WCIS_Colony".Translate(map.Parent.LabelCap)))
                {
                    Find.WindowStack.Add(new FloatMenu(colonyMaps.Select(m => new FloatMenuOption(m.Parent.LabelCap, () => SetMap(m))).ToList()));
                }
                x += colonyRect.width + 8f;
            }

            Rect negotiatorRect = new Rect(x, rect.y, 290f, rect.height);
            negotiatorLabel ??= negotiator != null
                ? "WCIS_Negotiator".Translate(NegotiatorLabel(negotiator)).ToString()
                : "WCIS_NoNegotiator".Translate().ToString();
            if (Widgets.ButtonText(negotiatorRect, negotiatorLabel))
            {
                List<Pawn> candidates = SellableStock.NegotiatorCandidates(map, settlement);
                if (candidates.Any())
                {
                    Find.WindowStack.Add(new FloatMenu(candidates.Select(p => new FloatMenuOption(NegotiatorLabel(p), () => SetNegotiator(p))).ToList()));
                }
            }
            if (Mouse.IsOver(negotiatorRect))
            {
                TooltipHandler.TipRegion(negotiatorRect, negotiator != null
                    ? "WCIS_NegotiatorTip".Translate()
                    : SellableStock.NoNegotiatorReason(settlement));
            }
            x += negotiatorRect.width + 8f;

            if (Widgets.ButtonText(new Rect(x, rect.y, 170f, rect.height), sortByValue ? "WCIS_SortByValue".Translate() : "WCIS_SortByName".Translate()))
            {
                sortByValue = !sortByValue;
                dirty = true;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            // Inset from the window edge: a text field ending exactly on it loses its right border.
            Rect searchRect = new Rect(rect.xMax - SearchWidth - 4f, rect.y + (rect.height - QuickSearchWidget.WidgetHeight) / 2f, SearchWidth, QuickSearchWidget.WidgetHeight);
            search.OnGUI(searchRect, () =>
            {
                scrollPosition = Vector2.zero;
                dirty = true;
            });
        }

        private static string NegotiatorLabel(Pawn pawn)
        {
            return pawn.LabelShortCap + " (" + pawn.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent() + ")";
        }

        // Two lists rather than more tabs: what this settlement buys, and what it won't (with who will).
        private void DrawModes(Rect rect)
        {
            float width = Mathf.Min(320f, (rect.width - 8f) / 2f);
            DrawMode(new Rect(rect.x, rect.y, width, rect.height), false, buysLabel, "WCIS_ModeBuysTip");
            DrawMode(new Rect(rect.x + width + 8f, rect.y, width, rect.height), true, wontBuyLabel, "WCIS_ModeWontBuyTip");
        }

        private void DrawMode(Rect rect, bool elsewhereMode, string label, string tipKey)
        {
            bool selected = showElsewhere == elsewhereMode;
            if (Widgets.ButtonText(rect, label.Truncate(rect.width - 16f, truncated)) && !selected)
            {
                showElsewhere = elsewhereMode;
                scrollPosition = Vector2.zero;
                dirty = true;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
            if (selected)
            {
                Widgets.DrawHighlightSelected(rect);
                Widgets.DrawBox(rect);
            }
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, tipKey.Translate(settlement.LabelCap));
            }
        }

        private void DrawSellList(Rect rect)
        {
            float width = rect.width - 16f;
            Rect header = new Rect(rect.x, rect.y, width, HeaderHeight);
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + LabelX, header.y, SellColumns.Start(width, ColAvailable) - LabelX, HeaderHeight), "WCIS_ColItem".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(SellColumns.Rect(header, width, ColAvailable), "WCIS_ColInStorage".Translate());
            Widgets.Label(SellColumns.Rect(header, width, ColMarket), "WCIS_ColMarketValue".Translate());
            Rect priceHeader = SellColumns.Rect(header, width, ColPrice);
            Widgets.Label(priceHeader, "WCIS_ColSellPrice".Translate());
            TooltipHandler.TipRegion(priceHeader, "WCIS_SellPriceTip".Translate());
            Widgets.Label(SellColumns.Rect(header, width, ColTotal), "WCIS_ColTotal".Translate());
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(SellColumns.Rect(header, width, ColAdjust, gap: 0f), "WCIS_ColSell".Translate());
            GUI.color = Color.white;

            DrawRows(new Rect(rect.x, header.yMax, rect.width, rect.height - HeaderHeight), DrawSellRow, "WCIS_Nothing".Translate());
        }

        private void DrawSellRow(Rect row, SellableEntry entry, int index)
        {
            float width = row.width;
            DrawRowStart(row, entry, index, SellColumns.Start(width, ColAvailable), settlement);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(SellColumns.Rect(row, width, ColAvailable), entry.availableText);
            Widgets.Label(SellColumns.Rect(row, width, ColMarket), entry.marketText);
            Rect priceRect = SellColumns.Rect(row, width, ColPrice);
            Widgets.Label(priceRect, entry.priceText);
            if (Mouse.IsOver(priceRect))
            {
                TooltipHandler.TipRegion(priceRect, "WCIS_SellPriceTip".Translate());
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Rect adjustRect = SellColumns.Rect(row, width, ColAdjust, gap: 0f);
            if (entry.IsPawn && entry.available == 1)
            {
                // Vanilla draws single pawns as an off-centre checkbox; centre it in the column instead.
                bool on = entry.Selected > 0;
                Widgets.Checkbox(new Vector2(adjustRect.center.x - 12f, adjustRect.center.y - 12f), ref on, 24f, paintable: true);
                if (on != entry.Selected > 0)
                {
                    entry.transferable.AdjustTo(on ? 1 : 0);
                }
            }
            else
            {
                TransferableUIUtility.DoCountAdjustInterface(adjustRect, entry.transferable, index, 0, entry.available);
            }

            Text.Anchor = TextAnchor.MiddleRight;
            Rect totalRect = SellColumns.Rect(row, width, ColTotal);
            if (entry.Selected > 0)
            {
                Widgets.Label(totalRect, SellableStock.FormatPrice(entry.sellPrice * entry.Selected, settlement));
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(totalRect, "-");
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Header line, empty state and the scrolled, culled rows shared by both lists.
        private void DrawRows(Rect rect, Action<Rect, SellableEntry, int> drawRow, string emptyText)
        {
            // Buttons drawn earlier in this frame (mode switch, tabs) can change the list mid-frame.
            if (dirty)
            {
                Refresh();
            }
            float width = rect.width - 16f;
            Widgets.DrawLineHorizontal(rect.x, rect.y + 1f, width);
            Rect outRect = new Rect(rect.x, rect.y + 4f, rect.width, rect.height - 4f);
            Text.Anchor = TextAnchor.UpperLeft;

            if (visible.Count == 0)
            {
                if (!search.filter.Active)
                {
                    // A tab can be empty while its list isn't, e.g. Animals in the "won't buy" list.
                    string text = CurrentList.Count > 0 ? "WCIS_NothingInTab".Translate().ToString() : emptyText;
                    GUI.color = Color.gray;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, 60f), text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
                return;
            }

            Widgets.BeginScrollView(outRect, ref scrollPosition, new Rect(0f, 0f, width, visible.Count * RowHeight));
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / RowHeight));
            int last = Mathf.Min(visible.Count - 1, Mathf.CeilToInt((scrollPosition.y + outRect.height) / RowHeight));
            for (int i = first; i <= last; i++)
            {
                drawRow(new Rect(0f, i * RowHeight, width, RowHeight), visible[i], i);
            }
            Widgets.EndScrollView();
        }

        // Highlight, info card, icon, pawn icons and the label up to labelEnd.
        private void DrawRowStart(Rect row, SellableEntry entry, int index, float labelEnd, Settlement trader)
        {
            TransferableOneWay trad = entry.transferable;
            Thing thing = trad.AnyThing;

            if (entry.Selected > 0)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(row);
            }
            Widgets.DrawHighlightIfMouseover(row);

            Widgets.InfoCardButton(row.x + 2f, row.y + (row.height - 24f) / 2f, thing);
            Widgets.ThingIcon(new Rect(IconX, row.y + (row.height - 27f) / 2f, 27f, 27f), thing);

            // These vanilla helpers draw right-to-left from curX in row-local coordinates.
            float curX = labelEnd - 6f;
            if (entry.IsPawn)
            {
                Widgets.BeginGroup(row);
                Rect local = row.AtZero();
                TransferableUIUtility.DoExtraIcons(trad, local, ref curX);
                TransferableUIUtility.DrawCaptiveTradeInfo(trad, trader, local, ref curX);
                Widgets.EndGroup();
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(LabelX, row.y, curX - LabelX - 4f, row.height);
            Widgets.Label(labelRect, entry.Label.Truncate(labelRect.width, truncated));
            if (Mouse.IsOver(labelRect))
            {
                TooltipHandler.TipRegion(labelRect, entry.Tooltip);
            }
        }

        private void DrawBottom(Rect rect)
        {
            int itemCount = 0;
            int pawnCount = 0;
            float mass = 0f;
            float value = 0f;
            foreach (SellableEntry entry in entries)
            {
                int selected = entry.Selected;
                if (selected <= 0)
                {
                    continue;
                }
                if (entry.IsPawn)
                {
                    pawnCount += selected;
                }
                else
                {
                    itemCount += selected;
                }
                mass += entry.massEach * selected;
                value += entry.sellPrice * selected;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 26f),
                "WCIS_Summary".Translate(itemCount.ToStringCached(), pawnCount.ToStringCached(), mass.ToString("0.#"), SellableStock.FormatPrice(value, settlement)));
            Text.Anchor = TextAnchor.UpperLeft;

            float buttonY = rect.yMax - BottomButtonSize.y;
            if (Widgets.ButtonText(new Rect(rect.x, buttonY, BottomButtonSize.x, BottomButtonSize.y), "CloseButton".Translate()))
            {
                Close();
            }

            float midX = rect.center.x;
            if (!showElsewhere
                && Widgets.ButtonText(new Rect(midX - BottomButtonSize.x - 5f, buttonY, BottomButtonSize.x, BottomButtonSize.y), "WCIS_SelectAll".Translate()))
            {
                foreach (SellableEntry entry in visible)
                {
                    entry.transferable.AdjustTo(entry.available);
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

            string cantSendReason = CantSendReason();
            Rect formRect = new Rect(rect.xMax - BottomButtonSize.x, buttonY, BottomButtonSize.x, BottomButtonSize.y);
            // Vanilla's inactive button barely changes, so dim the whole thing.
            GUI.color = cantSendReason == null ? Color.white : DisabledButtonColor;
            if (Widgets.ButtonText(formRect, "WCIS_FormCaravan".Translate(), active: cantSendReason == null) && cantSendReason == null)
            {
                FormCaravan();
            }
            GUI.color = Color.white;
            if (cantSendReason != null)
            {
                TooltipHandler.TipRegion(formRect, cantSendReason);
            }
        }

        private string CantSendReason()
        {
            if (map == null)
            {
                return "WCIS_NoColony".Translate();
            }
            // A caravan sent to a hostile settlement takes the first available arrival action there, which is "attack".
            if (!SellableStock.CanSendCaravanTo(settlement))
            {
                return "WCIS_HostileNoCaravan".Translate();
            }
            if (negotiator == null)
            {
                return SellableStock.NoNegotiatorReason(settlement);
            }
            return null;
        }

        private void FormCaravan()
        {
            var selections = entries
                .Where(e => e.Selected > 0)
                .Select(e => (e.transferable.things.ToList(), e.Selected))
                .ToList();
            selections.Add((new List<Thing> { negotiator }, 1));
            Close(doCloseSound: false);
            FormCaravanPrefill.Open(map, settlement, selections);
        }

        // Fixed-width columns laid out from the right edge of a row.
        private sealed class Columns
        {
            private readonly float[] widths;

            public Columns(params float[] widths)
            {
                this.widths = widths;
            }

            public float Start(float rowWidth, int column)
            {
                float x = rowWidth;
                for (int i = 0; i <= column; i++)
                {
                    x -= widths[i];
                }
                return x;
            }

            public Rect Rect(Rect row, float rowWidth, int column, float gap = 8f)
            {
                return new Rect(row.x + Start(rowWidth, column), row.y, widths[column] - gap, row.height);
            }
        }
    }
}
