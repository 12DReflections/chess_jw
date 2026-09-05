using System.Collections.Generic;
using System.Text;
using Chess4D.Core;
using UnityEngine;
using Color = UnityEngine.Color;
using Side = Chess4D.Core.Color;
using UnityEngine.UI;

namespace Chess4D.Unity
{
    /// <summary>
    /// Left: perspective buttons, phi scrubber, layer isolation, typed input, status.
    /// Right: game status, undo/redo, move history, setup editor, save/load.
    /// Bottom right: the picture-in-picture list of hidden-axis widgets
    /// (occupancy strip plus density bar per hidden slot; occupancy only).
    /// Centre: the promotion dialog when a promotion is pending.
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
        private Text viewText, statusText, isolateText, gameStatusText, historyText, messageText;
        private InputField coordInput, moveInput, fileInput;
        private RectTransform setupPanel, promotionPanel;
        private Button[] brushButtons;
        private Button eraseButton, colourButton, sideButton, setupToggle;
        private readonly List<HiddenAxisWidget> widgets = new List<HiddenAxisWidget>();
        private readonly int[] densityCounts = new int[8];
        private readonly List<int> cellsScratch = new List<int>(300);
        private readonly StringBuilder sb = new StringBuilder();
        private bool suppressSlider;
        public Canvas Canvas { get; private set; }

        private static readonly Color StripEmpty = new Color(0.28f, 0.28f, 0.32f);
        private static readonly Color StripWhite = new Color(0.93f, 0.9f, 0.7f);
        private static readonly Color StripBlack = new Color(0.45f, 0.55f, 0.95f);
        private static readonly Color StripNone = new Color(0.18f, 0.18f, 0.2f);
        private static readonly Color BorderCurrent = new Color(1f, 0.6f, 0.15f);
        private static readonly Color BorderNone = new Color(0f, 0f, 0f, 0f);
        private static readonly PieceType[] Brushes = { PieceType.King, PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight, PieceType.Pawn };

        public void Build(ViewState s, Chess4DGame g)
        {
            state = s;
            game = g;
            var canvas = UiKit.Canvas("HUD");
            canvas.transform.SetParent(transform, false);
            Canvas = canvas;

            BuildLeft(canvas.transform);
            BuildRight(canvas.transform);
            BuildPip(canvas.transform);
            BuildPromotion(canvas.transform);
        }

        // ------------------------------------------------------------ left: view controls

        private void BuildLeft(Transform root)
        {
            var left = UiKit.Panel(root, "left", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(270, 520), UiKit.PanelColor);
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

            UiKit.Label(col, "Coordinate  (x,y,z,w): select, or place in setup", 12, TextAnchor.MiddleLeft, 18);
            var coordRow = UiKit.HorizontalGroup(col, "coord", 6, 26);
            coordInput = UiKit.InputField(coordRow, "(4,1,3,3)");
            coordInput.onEndEdit.AddListener(t => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) game.OnTyped(t); });
            var selBtn = UiKit.Button(coordRow, "Go", () => game.OnTyped(coordInput.text), 26);
            selBtn.GetComponent<LayoutElement>().preferredWidth = 50;
            coordInput.GetComponent<LayoutElement>().flexibleWidth = 1;

            UiKit.Label(col, "Left-drag: orbit   Scroll: zoom   Click: select / move\n[ ]: page hidden axis   Esc: clear   Z / Y: undo / redo", 11, TextAnchor.UpperLeft, 34);
            statusText = UiKit.Label(col, "", 12, TextAnchor.UpperLeft, 60);
        }

        // ------------------------------------------------------------ right: game and editor

        private void BuildRight(Transform root)
        {
            var right = UiKit.Panel(root, "right", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(310, 600), UiKit.PanelColor);
            var col = UiKit.VerticalGroup(right, "col", 6, new RectOffset(10, 10, 10, 10));
            gameStatusText = UiKit.Label(col, "", 14, TextAnchor.MiddleLeft, 22);

            var row = UiKit.HorizontalGroup(col, "game", 6, 26);
            UiKit.Button(row, "New game", () => game.NewGame(), 26, 13);
            UiKit.Button(row, "Undo", () => game.Undo(), 26, 13);
            UiKit.Button(row, "Redo", () => game.Redo(), 26, 13);
            setupToggle = UiKit.Button(row, "Setup", () => { if (game.Mode == GameMode.Setup) game.ExitSetup(); else game.EnterSetup(); }, 26, 13);

            UiKit.Label(col, "Move  (from) (to)[=Q]   long tuple notation, see docs/NOTATION.md", 11, TextAnchor.MiddleLeft, 16);
            var moveRow = UiKit.HorizontalGroup(col, "move", 6, 26);
            moveInput = UiKit.InputField(moveRow, "(from) (to)");
            moveInput.onEndEdit.AddListener(t => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { if (game.OnTyped(t)) moveInput.text = ""; } });
            var moveBtn = UiKit.Button(moveRow, "Play", () => { if (game.OnTyped(moveInput.text)) moveInput.text = ""; }, 26);
            moveBtn.GetComponent<LayoutElement>().preferredWidth = 56;
            moveInput.GetComponent<LayoutElement>().flexibleWidth = 1;

            messageText = UiKit.Label(col, "", 12, TextAnchor.UpperLeft, 34);
            messageText.color = new Color(1f, 0.85f, 0.5f);

            UiKit.Label(col, "History", 12, TextAnchor.MiddleLeft, 16);
            historyText = UiKit.Label(col, "", 12, TextAnchor.UpperLeft, 190);

            // Setup editor, shown only in setup mode.
            setupPanel = UiKit.VerticalGroup(col, "setup", 4, new RectOffset(0, 0, 4, 0));
            var img = setupPanel.gameObject.AddComponent<Image>();
            img.color = new Color(0.14f, 0.14f, 0.18f, 0.9f);
            UiKit.Label(setupPanel, "Brush: click a cell or type a coordinate and press Go", 11, TextAnchor.MiddleLeft, 16);
            var brushRow = UiKit.HorizontalGroup(setupPanel, "brush", 3, 26);
            brushButtons = new Button[Brushes.Length];
            for (int i = 0; i < Brushes.Length; i++)
            {
                PieceType t = Brushes[i];
                brushButtons[i] = UiKit.Button(brushRow, t.ToString().Substring(0, 1), () => { game.SetupBrush = t; game.SetupErase = false; }, 26, 13);
            }
            eraseButton = UiKit.Button(brushRow, "Erase", () => game.SetupErase = true, 26, 12);
            var row2 = UiKit.HorizontalGroup(setupPanel, "colour", 3, 26);
            colourButton = UiKit.Button(row2, "White", () => game.SetupColor = Piece.Opposite(game.SetupColor), 26, 12);
            sideButton = UiKit.Button(row2, "To move: White", () => game.ToggleSideToMove(), 26, 12);
            var row3 = UiKit.HorizontalGroup(setupPanel, "board", 3, 26);
            UiKit.Button(row3, "Clear board", () => game.ClearBoard(), 26, 12);
            UiKit.Button(row3, "Standard start", () => game.StandardStart(), 26, 12);
            UiKit.Button(row3, "Done, play", () => game.ExitSetup(), 26, 12);

            UiKit.Label(col, "Position file  (also copied to / read from the clipboard)", 11, TextAnchor.MiddleLeft, 16);
            var fileRow = UiKit.HorizontalGroup(col, "file", 4, 26);
            fileInput = UiKit.InputField(fileRow, "name");
            fileInput.GetComponent<LayoutElement>().flexibleWidth = 1;
            fileInput.GetComponent<LayoutElement>().minWidth = 90;
            UiKit.Button(fileRow, "Save", () => game.SavePosition(fileInput.text), 26, 12).GetComponent<LayoutElement>().preferredWidth = 48;
            UiKit.Button(fileRow, "Load", () => game.LoadPosition(fileInput.text), 26, 12).GetComponent<LayoutElement>().preferredWidth = 48;
            UiKit.Button(fileRow, "Paste", () => game.LoadClipboard(), 26, 12).GetComponent<LayoutElement>().preferredWidth = 52;
        }

        // ------------------------------------------------------------ promotion dialog

        private void BuildPromotion(Transform root)
        {
            promotionPanel = UiKit.Panel(root, "promotion", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 90), new Color(0.1f, 0.1f, 0.14f, 0.96f));
            var col = UiKit.VerticalGroup(promotionPanel, "col", 6, new RectOffset(10, 10, 8, 8));
            UiKit.Label(col, "Promote the pawn to:", 14, TextAnchor.MiddleCenter, 22);
            var row = UiKit.HorizontalGroup(col, "choices", 6, 34);
            UiKit.Button(row, "Queen", () => game.ChoosePromotion(PieceType.Queen), 34);
            UiKit.Button(row, "Rook", () => game.ChoosePromotion(PieceType.Rook), 34);
            UiKit.Button(row, "Bishop", () => game.ChoosePromotion(PieceType.Bishop), 34);
            UiKit.Button(row, "Knight", () => game.ChoosePromotion(PieceType.Knight), 34);
            promotionPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ picture in picture

        private void BuildPip(Transform root)
        {
            int hidden = state.View.HiddenSlots;
            float pipHeight = 30 + hidden * 118;
            var pip = UiKit.Panel(root, "pip", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 10), new Vector2(310, pipHeight), UiKit.PanelColor);
            var pipCol = UiKit.VerticalGroup(pip, "col", 4, new RectOffset(10, 10, 8, 8));
            UiKit.Label(pipCol, "Hidden axes   (strip: occupancy through the selected or hovered cell; click to page)", 11, TextAnchor.MiddleLeft, 18);
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
                UiKit.Stretch(inner.GetComponent<RectTransform>(), 2.5f);
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

        // ------------------------------------------------------------ per-frame refresh

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

            gameStatusText.text = game.StatusLine();
            gameStatusText.color = game.Board.InCheck() || game.Game.IsOver ? new Color(1f, 0.55f, 0.35f) : UiKit.TextColor;
            messageText.text = game.Message;
            RefreshHistory();

            bool setup = game.Mode == GameMode.Setup;
            if (setupPanel.gameObject.activeSelf != setup) setupPanel.gameObject.SetActive(setup);
            setupToggle.GetComponentInChildren<Text>().text = setup ? "Exit setup" : "Setup";
            setupToggle.GetComponent<Image>().color = setup ? UiKit.ActiveColor : UiKit.ButtonColor;
            if (setup)
            {
                for (int i = 0; i < brushButtons.Length; i++)
                    brushButtons[i].GetComponent<Image>().color = !game.SetupErase && game.SetupBrush == Brushes[i] ? UiKit.ActiveColor : UiKit.ButtonColor;
                eraseButton.GetComponent<Image>().color = game.SetupErase ? UiKit.ActiveColor : UiKit.ButtonColor;
                colourButton.GetComponentInChildren<Text>().text = "Brush: " + game.SetupColor;
                sideButton.GetComponentInChildren<Text>().text = "To move: " + game.Board.SideToMove;
            }
            if (promotionPanel.gameObject.activeSelf != game.HasPendingPromotion) promotionPanel.gameObject.SetActive(game.HasPendingPromotion);

            int focus = state.SelectedCell >= 0 ? state.SelectedCell : state.HoverCell;
            foreach (var w in widgets) RefreshWidget(w, focus);
        }

        private void RefreshHistory()
        {
            var g = game.Game;
            int n = g.History.Count;
            const int maxLines = 12;
            sb.Clear();
            if (n == 0) sb.Append("(no moves yet)");
            int start = Mathf.Max(0, n - maxLines);
            if (start > 0) sb.Append("... ").Append(start).Append(" earlier\n");
            for (int i = start; i < n; i++) sb.Append(g.HistoryLine(i)).Append('\n');
            if (g.RedoCount > 0) sb.Append("(").Append(g.RedoCount).Append(" move").Append(g.RedoCount == 1 ? "" : "s").Append(" available to redo)");
            historyText.text = sb.ToString();
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
            for (int v = 0; v < side; v++) w.Bars[v].anchorMax = new Vector2(1, Mathf.Max(0.02f, densityCounts[v] / (float)max));
            w.Footer.text = "pieces per " + AxisView.AxisNames[axis] + " layer: " + string.Join(" ", densityCounts);
        }
    }
}
