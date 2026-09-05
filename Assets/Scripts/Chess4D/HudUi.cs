using System.Collections.Generic;
using Chess4D.Core;
using UnityEngine;
using Color = UnityEngine.Color;
using Side = Chess4D.Core.Color;
using UnityEngine.UI;

namespace Chess4D.Unity
{
    /// <summary>
    /// Perspective buttons, the phi scrubber, layer isolation, typed coordinate
    /// entry, and the picture-in-picture list of hidden-axis widgets (occupancy
    /// strip plus density bar per hidden slot). Occupancy only; threats are Stage 5.
    /// </summary>
    public sealed class HudUi : MonoBehaviour
    {
        private sealed class HiddenAxisWidget
        {
            public int Slot;
            public Text Title;
            public Image[] Borders;
            public Image[] Cells;
            public RectTransform[] Bars;
            public Text Footer;
        }

        private ViewState state;
        private Chess4DGame game;
        private Button[] perspectiveButtons;
        private Slider phiSlider;
        private Text viewText, statusText, isolateText;
        private InputField coordInput;
        private readonly List<HiddenAxisWidget> widgets = new List<HiddenAxisWidget>();
        private readonly int[] densityCounts = new int[8];
        private readonly List<int> cellsScratch = new List<int>(300);
        private bool suppressSlider;
        public Canvas Canvas { get; private set; }

        private static readonly Color StripEmpty = new Color(0.28f, 0.28f, 0.32f);
        private static readonly Color StripWhite = new Color(0.93f, 0.9f, 0.7f);
        private static readonly Color StripBlack = new Color(0.45f, 0.55f, 0.95f);
        private static readonly Color StripNone = new Color(0.18f, 0.18f, 0.2f);
        private static readonly Color BorderCurrent = new Color(1f, 0.6f, 0.15f);
        private static readonly Color BorderNone = new Color(0f, 0f, 0f, 0f);

        public void Build(ViewState s, Chess4DGame g)
        {
            state = s;
            game = g;
            var canvas = UiKit.Canvas("HUD");
            canvas.transform.SetParent(transform, false);
            Canvas = canvas;

            // ---- left panel
            var left = UiKit.Panel(canvas.transform, "left", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(270, 560), UiKit.PanelColor);
            var col = UiKit.VerticalGroup(left, "col", 6, new RectOffset(10, 10, 10, 10));
            UiKit.Label(col, "4D Chess", 20, TextAnchor.MiddleLeft, 26);
            viewText = UiKit.Label(col, "", 13, TextAnchor.MiddleLeft, 20);

            UiKit.Label(col, "Perspective   (1-4: sweep, shift+click: arm)", 12, TextAnchor.MiddleLeft, 18);
            perspectiveButtons = new Button[Chess4DGame.Perspectives.Length];
            var row1 = UiKit.HorizontalGroup(col, "row1", 6, 28);
            var row2 = UiKit.HorizontalGroup(col, "row2", 6, 28);
            for (int i = 0; i < Chess4DGame.Perspectives.Length; i++)
            {
                int idx = i;
                int[] axes = Chess4DGame.Perspectives[i];
                string label = AxisView.AxisNames[axes[0]] + " " + AxisView.AxisNames[axes[1]] + " " + AxisView.AxisNames[axes[2]];
                perspectiveButtons[i] = UiKit.Button(i < 2 ? row1 : row2, label, () => game.OnPerspectiveButton(idx), 28);
            }

            UiKit.Label(col, "Rotation phi   (shift+drag scrubs when armed)", 12, TextAnchor.MiddleLeft, 18);
            phiSlider = UiKit.Slider(col, 0f, 90f, v => { if (!suppressSlider) state.Scrub(v); }, () => state.Release());

            UiKit.Label(col, "Layer isolation   (I: mode, O: axis)", 12, TextAnchor.MiddleLeft, 18);
            var isoRow = UiKit.HorizontalGroup(col, "iso", 6, 26);
            UiKit.Button(isoRow, "mode", () => game.CycleIsolateMode(), 26);
            UiKit.Button(isoRow, "axis", () => game.CycleIsolateSlot(), 26);
            isolateText = UiKit.Label(col, "", 12, TextAnchor.MiddleLeft, 18);

            UiKit.Label(col, "Select by coordinate  (x,y,z,w)", 12, TextAnchor.MiddleLeft, 18);
            var coordRow = UiKit.HorizontalGroup(col, "coord", 6, 26);
            coordInput = UiKit.InputField(coordRow, "(4,1,3,3)");
            coordInput.onEndEdit.AddListener(t => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) game.SelectTyped(t); });
            var selBtn = UiKit.Button(coordRow, "Select", () => game.SelectTyped(coordInput.text), 26);
            selBtn.GetComponent<LayoutElement>().preferredWidth = 70;
            coordInput.GetComponent<LayoutElement>().flexibleWidth = 1;

            UiKit.Label(col, "Left-drag: orbit   Scroll: zoom   Click: select   Esc: clear\n[ ]: page hidden axis", 11, TextAnchor.UpperLeft, 34);
            statusText = UiKit.Label(col, "", 12, TextAnchor.UpperLeft, 60);

            // ---- picture-in-picture, bottom right: one widget per hidden slot
            int hidden = state.View.HiddenSlots;
            float pipHeight = 30 + hidden * 118;
            var pip = UiKit.Panel(canvas.transform, "pip", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 10), new Vector2(300, pipHeight), UiKit.PanelColor);
            var pipCol = UiKit.VerticalGroup(pip, "col", 4, new RectOffset(10, 10, 8, 8));
            UiKit.Label(pipCol, "Hidden axes   (strip: occupancy of selected/hovered cell; click to page)", 11, TextAnchor.MiddleLeft, 18);
            for (int slot = AxisView.VisibleSlots; slot < state.View.Dimensions; slot++)
                widgets.Add(BuildWidget(pipCol, slot));
        }

        private HiddenAxisWidget BuildWidget(Transform parent, int slot)
        {
            var w = new HiddenAxisWidget { Slot = slot };
            w.Title = UiKit.Label(parent, "", 13, TextAnchor.MiddleLeft, 20);
            int side = state.G.Side;
            var stripRow = UiKit.HorizontalGroup(parent, "strip", 3, 30);
            var prev = UiKit.Button(stripRow, "<", () => state.Page(slot, -1), 30);
            prev.GetComponent<LayoutElement>().preferredWidth = 22;
            w.Borders = new Image[side];
            w.Cells = new Image[side];
            for (int v = 0; v < side; v++)
            {
                int value = v;
                var border = UiKit.Box(stripRow, BorderNone);
                var btn = border.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => state.SetPage(slot, value));
                var inner = UiKit.Box(border.transform, StripEmpty);
                var irt = inner.GetComponent<RectTransform>();
                UiKit.Stretch(irt, 2.5f);
                inner.raycastTarget = false;
                w.Borders[v] = border;
                w.Cells[v] = inner;
            }
            var next = UiKit.Button(stripRow, ">", () => state.Page(slot, +1), 30);
            next.GetComponent<LayoutElement>().preferredWidth = 22;

            var barRow = UiKit.HorizontalGroup(parent, "density", 3, 34);
            var padL = UiKit.Box(barRow, BorderNone); padL.GetComponent<LayoutElement>().preferredWidth = 22; padL.GetComponent<LayoutElement>().flexibleWidth = 0;
            w.Bars = new RectTransform[side];
            for (int v = 0; v < side; v++)
            {
                var holder = UiKit.Box(barRow, new Color(0.15f, 0.15f, 0.17f));
                var bar = UiKit.Box(holder.transform, new Color(0.6f, 0.75f, 0.9f));
                var brt = bar.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.offsetMin = new Vector2(1, 0); brt.offsetMax = new Vector2(-1, 0);
                bar.raycastTarget = false;
                w.Bars[v] = brt;
            }
            var padR = UiKit.Box(barRow, BorderNone); padR.GetComponent<LayoutElement>().preferredWidth = 22; padR.GetComponent<LayoutElement>().flexibleWidth = 0;
            w.Footer = UiKit.Label(parent, "", 11, TextAnchor.MiddleLeft, 16);
            return w;
        }

        private void Update()
        {
            if (state == null) return;
            viewText.text = state.Describe();
            for (int i = 0; i < perspectiveButtons.Length; i++)
            {
                int[] axes = Chess4DGame.Perspectives[i];
                bool active = state.View.ShowsAxes(axes);
                bool armed = state.Armed && state.Target.ShowsAxes(axes);
                perspectiveButtons[i].GetComponent<Image>().color = active ? UiKit.ActiveColor : armed ? UiKit.ArmedColor : UiKit.ButtonColor;
            }
            suppressSlider = true;
            phiSlider.SetValueWithoutNotify(state.Phi);
            phiSlider.interactable = state.Armed && !state.Sweeping;
            suppressSlider = false;

            isolateText.text = state.IsolateMode == IsolateMode.Off
                ? "off"
                : (state.IsolateMode == IsolateMode.HideBelow ? "hide below" : "hide above") + " selected cell along screen " + SlotName(state.IsolateSlot)
                  + (state.SelectedCell < 0 ? "  (select a cell)" : "");

            statusText.text = DescribeCell("selected", state.SelectedCell) + "\n" + DescribeCell("hover", state.HoverCell)
                + (state.PickingEnabled ? "" : "\npicking disabled while rotating");

            int focus = state.SelectedCell >= 0 ? state.SelectedCell : state.HoverCell;
            foreach (var w in widgets) RefreshWidget(w, focus);
        }

        private static string SlotName(int slot) { return slot == 0 ? "X" : slot == 1 ? "Y (up)" : slot == 2 ? "Z" : "?"; }

        private string DescribeCell(string label, int cell)
        {
            if (cell < 0) return label + ": none";
            byte p = state.Board.GetPiece(cell);
            string what = p == 0 ? "empty" : Piece.ColorOf(p) + " " + Piece.TypeOf(p);
            return label + ": " + state.G.CoordOf(cell) + " " + what;
        }

        private void RefreshWidget(HiddenAxisWidget w, int focus)
        {
            int axis = state.View.AxisAtSlot(w.Slot);
            int page = state.Pages[axis];
            int side = state.G.Side;
            w.Title.text = "hidden " + AxisView.AxisNames[axis] + "   page " + page + (focus >= 0 ? "   strip through " + state.G.CoordOf(focus) : "   (no cell)");

            for (int v = 0; v < side; v++)
            {
                Color c = StripNone;
                if (focus >= 0)
                {
                    int cell = state.G.WithCoord(focus, axis, v);
                    byte p = state.Board.GetPiece(cell);
                    c = p == 0 ? StripEmpty : Piece.ColorOf(p) == Side.White ? StripWhite : StripBlack;
                }
                w.Cells[v].color = c;
                w.Borders[v].color = v == page ? BorderCurrent : BorderNone;
            }

            System.Array.Clear(densityCounts, 0, densityCounts.Length);
            int max = 1;
            for (int col = 0; col < 2; col++)
            {
                state.Board.GetPieceCells((Side)col, cellsScratch);
                foreach (int cell in cellsScratch)
                {
                    int v = state.G.Coord(cell, axis);
                    densityCounts[v]++;
                    if (densityCounts[v] > max) max = densityCounts[v];
                }
            }
            for (int v = 0; v < side; v++)
            {
                float f = densityCounts[v] / (float)max;
                w.Bars[v].anchorMax = new Vector2(1, Mathf.Max(0.02f, f));
            }
            w.Footer.text = "pieces per " + AxisView.AxisNames[axis] + " layer: " + string.Join(" ", densityCounts);
        }
    }
}
