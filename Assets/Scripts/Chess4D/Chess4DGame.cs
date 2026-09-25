using System;
using System.IO;
using System.Threading;
using Chess4D.Core;
using Chess4D.Engine;
using UnityEngine;
using UnityEngine.EventSystems;
using Color = UnityEngine.Color;
using Side = Chess4D.Core.Color;

namespace Chess4D.Unity
{
    public enum GameMode { Play, Setup }
    public enum PlayerKind { Human, Engine }

    /// <summary>
    /// Bootstrap, game management and input. Owns the Core game and the view
    /// state, builds the renderer, camera rig and HUD in code, routes mouse and
    /// keyboard input, and implements play mode, the position editor and
    /// save/load. No game rules live here.
    /// </summary>
    public sealed class Chess4DGame : MonoBehaviour
    {
        public static readonly int[][] Perspectives = { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };

        [SerializeField] private Mesh[] whiteMeshes = new Mesh[7];
        [SerializeField] private Mesh[] blackMeshes = new Mesh[7];
        [SerializeField] private Material pieceFadeMaterial;
        [SerializeField] private Material cellFadeMaterial;

        public Board Board { get; private set; }
        public Game Game { get; private set; }
        public ViewState State { get; private set; }
        public BoardView View { get; private set; }
        public OrbitCamera Orbit { get; private set; }
        public HudUi Hud { get; private set; }

        public GameMode Mode { get; private set; } = GameMode.Play;
        public string Message { get; private set; } = "";
        public bool HasPendingPromotion { get; private set; }
        public int PendingFrom { get; private set; } = -1;
        public int PendingTo { get; private set; } = -1;
        public int HiddenTargetCount { get; private set; }

        public PieceType SetupBrush = PieceType.King;
        public Side SetupColor = Side.White;
        public bool SetupErase;

        /// <summary>Always-on attack map for both sides; the UI reads threats from it.</summary>
        public AttackMap Attacks { get; private set; }
        public bool ShowThreats = true;
        /// <summary>Board orientation, the third view behaviour: turn the board so the side to move has its pieces nearest the camera. Off keeps a fixed orientation.</summary>
        public bool AutoFlip = true;
        public string OrientationNote { get; private set; } = "";
        private bool flipPending;
        public readonly PlayerKind[] Players = { PlayerKind.Human, PlayerKind.Human };
        public static readonly int[] EngineTimeChoicesMs = { 300, 1000, 3000 };
        public int EngineTimeIndex = 1;
        public bool Thinking { get; private set; }
        public string ThinkingInfo { get; private set; } = "";
        /// <summary>Fired after a move is made, undone or redone: (move, the piece that moved, wasUndo).</summary>
        public event Action<Move, byte, bool> Moved;

        private readonly SearchEngine engine = new SearchEngine();
        private Board engineBoard;
        private Thread engineThread;
        private SearchResult engineResult;
        private ulong thinkingForHash;

        private readonly MoveList fromList = new MoveList(256);
        private bool dragging, scrubbing;
        private Vector3 dragStart, lastMouse;

        public string PositionsDirectory { get { return Path.Combine(Application.persistentDataPath, "positions"); } }

        private void Awake()
        {
            Application.runInBackground = true;
            Board = new Board(4, 8);
            StartPosition.Setup(Board);
            Game = new Game(Board);
            State = new ViewState(Board);
            Attacks = new AttackMap(Board);
            engineBoard = new Board(4, 8);

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            Orbit = cam.GetComponent<OrbitCamera>() ?? cam.gameObject.AddComponent<OrbitCamera>();

            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light", typeof(Light));
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.68f);

            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var meshes = new[] { whiteMeshes, blackMeshes };
            View = gameObject.AddComponent<BoardView>();
            View.Init(State, meshes, pieceFadeMaterial, cellFadeMaterial);

            Hud = new GameObject("HUD").AddComponent<HudUi>();
            Hud.Build(State, this);
            View.IsThreatened = cell => Attacks.IsPieceAttacked(cell);
            Moved += OnMovedFeedback;
            State.ViewChanged += () => UpdateOrientation(true);
            State.TurnCompleted += note => Message = "Turn complete (" + note + "): now showing " + State.View + ", hidden axis paged at " + State.PageOfSlot(AxisView.VisibleSlots);
            gameObject.AddComponent<AxisLabels>().Init(State);
            UpdateOrientation(false);

            Debug.Log("Chess4D ready: " + Board.PieceCount(Side.White) + " white and " + Board.PieceCount(Side.Black) + " black pieces, " + State.Describe());
            DemoRunner.StartIfRequested(this);
        }

        private void Update()
        {
            State.Update(Time.deltaTime);
            Attacks.Refresh();
            View.ShowThreats = ShowThreats;
            if (flipPending && !View.IsAnimating) { flipPending = false; UpdateOrientation(true); }
            HandleKeys();
            HandleMouse();
            StepEngine();
        }

        // ------------------------------------------------------------ engine players

        public PlayerKind PlayerFor(Side side) { return Players[(int)side]; }

        public void TogglePlayer(Side side) { Players[(int)side] = Players[(int)side] == PlayerKind.Human ? PlayerKind.Engine : PlayerKind.Human; }

        public void CycleEngineTime() { EngineTimeIndex = (EngineTimeIndex + 1) % EngineTimeChoicesMs.Length; }

        private void StepEngine()
        {
            if (Thinking)
            {
                bool done = engineThread == null || !engineThread.IsAlive;
                if (!done) return;
                Thinking = false;
                engineThread = null;
                if (thinkingForHash != Board.Hash || Mode != GameMode.Play) return; // position changed under us: discard
                SearchResult r = engineResult;
                ThinkingInfo = "depth " + r.Depth + ", " + r.Nodes + " nodes, " + r.Seconds.ToString("F2") + " s, score " + r.Score;
                if (r.HasMove) Commit(r.BestMove);
                return;
            }
            if (Mode != GameMode.Play || Game.IsOver || HasPendingPromotion) return;
            if (Players[(int)Board.SideToMove] != PlayerKind.Engine) return;
            StartThinking();
        }

        private void StartThinking()
        {
            engineBoard.CopyFrom(Board); // one copy per search; the search itself uses make/unmake on that copy
            thinkingForHash = Board.Hash;
            var limits = new SearchLimits { TimeMs = EngineTimeChoicesMs[EngineTimeIndex], MaxDepth = 6 };
            Thinking = true;
            ThinkingInfo = "thinking...";
            Message = Board.SideToMove + " (engine) is thinking";
#if UNITY_WEBGL
            engineResult = engine.Search(engineBoard, limits);
            engineThread = null;
#else
            engineThread = new Thread(() => { engineResult = engine.Search(engineBoard, limits); }) { IsBackground = true, Name = "Chess4D search" };
            engineThread.Start();
#endif
        }

        // ------------------------------------------------------------ board orientation

        /// <summary>
        /// Rule 1: the side to move sees its pieces near, with y advancing away. Rule 3: suppressed when y is
        /// hidden or vertical, since there is no forwards to face. Rule 5: camera only, never a move.
        /// </summary>
        public void UpdateOrientation(bool animate)
        {
            int slot = State.View.SlotOfAxis(BoardGeometry.AdvanceAxis);
            if (slot >= AxisView.VisibleSlots) { OrientationNote = "auto-flip suppressed: y hidden, no forwards to face"; return; }
            if (slot == 2) { OrientationNote = "auto-flip suppressed: y vertical, no forwards to face"; return; }
            OrientationNote = AutoFlip ? "" : "auto-flip off: fixed orientation";
            if (!AutoFlip) return;
            int sign = State.View.SignAtSlot(slot);
            // Slot 1 is depth: +y away from the camera needs yaw 0. Slot 0 is screen X: +X away needs yaw 90.
            float yaw = slot == 1 ? (sign > 0 ? 0f : 180f) : (sign > 0 ? 90f : 270f);
            if (Board.SideToMove == Side.Black) yaw += 180f;
            Orbit.SetOrientation(yaw, animate);
        }

        public void ToggleAutoFlip()
        {
            AutoFlip = !AutoFlip;
            UpdateOrientation(true);
        }

        /// <summary>The three animation cases plus history flash and layer marks. Never moves the view.</summary>
        private void OnMovedFeedback(Move m, byte piece, bool undo)
        {
            flipPending = true; // rule 2: after the move animation finishes, not during
            View.ShowThreats = ShowThreats;
            View.AnimateMove(m.From, m.To, piece, undo);
            bool visFrom = State.IsVisibleInVolume(m.From), visTo = State.IsVisibleInVolume(m.To);
            if (!visFrom && !visTo)
            {
                Hud.FlashHistory(1.5f);
                for (int s = AxisView.VisibleSlots; s < State.View.Dimensions; s++)
                {
                    int axis = State.View.AxisAtSlot(s);
                    Hud.MarkLayers(axis, Board.G.Coord(m.From, axis), Board.G.Coord(m.To, axis), 1.5f);
                }
                Message += "   (moved in a hidden layer: see the history flash and the strip marks, or press Jump to last move)";
            }
        }

        /// <summary>Pages the view so the last move's destination is visible and selects it. Only ever called by the player.</summary>
        public void JumpToLastMove()
        {
            if (Game.History.Count == 0) { Message = "No move yet"; return; }
            Move last = Game.History[Game.History.Count - 1];
            State.PageTo(last.To);
            State.SelectedCell = last.To;
            State.MoveTargets.Clear();
            State.CaptureTargets.Clear();
            Message = "Last move: " + Game.HistoryLine(Game.History.Count - 1);
        }

        // ------------------------------------------------------------ input

        private void HandleKeys()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) State.SweepTo(Perspectives[0]);
            if (Input.GetKeyDown(KeyCode.Alpha2)) State.SweepTo(Perspectives[1]);
            if (Input.GetKeyDown(KeyCode.Alpha3)) State.SweepTo(Perspectives[2]);
            if (Input.GetKeyDown(KeyCode.Alpha4)) State.SweepTo(Perspectives[3]);
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.PageDown)) State.Page(AxisView.VisibleSlots, -1);
            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.PageUp)) State.Page(AxisView.VisibleSlots, +1);
            if (Input.GetKeyDown(KeyCode.Escape)) Deselect();
            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.Alpha0)) ResetView();
            if (Input.GetKeyDown(KeyCode.I)) CycleIsolateMode();
            if (Input.GetKeyDown(KeyCode.O)) CycleIsolateSlot();
            if (Input.GetKeyDown(KeyCode.Z)) Undo();
            if (Input.GetKeyDown(KeyCode.Y)) Redo();
        }

        private void HandleMouse()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector3 mouse = Input.mousePosition;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetMouseButtonDown(0) && !overUi)
            {
                dragging = true;
                dragStart = lastMouse = mouse;
                scrubbing = shift && State.Armed && !State.Sweeping;
            }
            if (dragging && Input.GetMouseButton(0))
            {
                Vector3 delta = mouse - lastMouse;
                lastMouse = mouse;
                if (scrubbing) State.Scrub(State.Phi + delta.x * 0.4f);
                else if (!shift) Orbit.Drag(delta.x, delta.y);
            }
            if (dragging && Input.GetMouseButtonUp(0))
            {
                dragging = false;
                if (scrubbing) State.Release();
                else if ((mouse - dragStart).magnitude < 4f)
                {
                    int cell = View.Pick(Camera.main.ScreenPointToRay(mouse));
                    if (cell >= 0) OnCellClicked(cell);
                }
                scrubbing = false;
            }

            if (!overUi && Mathf.Abs(Input.mouseScrollDelta.y) > 0f) Orbit.Zoom(Input.mouseScrollDelta.y);

            State.HoverCell = (!dragging && !overUi && State.PickingEnabled) ? View.Pick(Camera.main.ScreenPointToRay(mouse)) : -1;
        }

        /// <summary>Opening view, opening orbit, then the orientation rule for the side to move. Never a move.</summary>
        public void ResetView()
        {
            State.ResetView();
            Orbit.ResetOrbit();
            UpdateOrientation(false);
            Message = "View reset: x y z on screen, w hidden";
        }

        public void OnPerspectiveButton(int index)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift) State.Arm(Perspectives[index]); else State.SweepTo(Perspectives[index]);
        }

        // ------------------------------------------------------------ play mode

        /// <summary>Click or typed coordinate: in play mode selects an own piece or moves to a highlighted target; in setup mode paints the brush.</summary>
        public void OnCellClicked(int cell)
        {
            if (HasPendingPromotion) return;
            if (Mode == GameMode.Setup) { PlaceBrush(cell); return; }
            if (Thinking) { Select(cell); return; }
            if (State.SelectedCell >= 0 && State.MoveTargets.Contains(cell)) { MoveTo(cell); return; }
            Select(cell);
        }

        public void Select(int cell)
        {
            State.SelectedCell = cell;
            State.MoveTargets.Clear();
            State.CaptureTargets.Clear();
            HiddenTargetCount = 0;
            if (cell < 0 || Mode != GameMode.Play || Game.IsOver || Thinking) return;
            byte p = Board.GetPiece(cell);
            if (p == 0 || Piece.ColorOf(p) != Board.SideToMove || Players[(int)Board.SideToMove] == PlayerKind.Engine) return;
            Game.LegalFrom(cell, fromList);
            for (int i = 0; i < fromList.Count; i++)
            {
                Move m = fromList[i];
                State.MoveTargets.Add(m.To);
                if (m.IsCapture) State.CaptureTargets.Add(m.To);
                if (!State.IsVisibleInVolume(m.To)) HiddenTargetCount++;
            }
            Message = fromList.Count + " legal moves for " + Piece.ColorOf(p) + " " + Piece.TypeOf(p) + " at " + Board.G.CoordOf(cell)
                + (HiddenTargetCount > 0 ? ", " + HiddenTargetCount + " in other layers (page or type the target)" : "");
        }

        public void Deselect()
        {
            State.SelectedCell = -1;
            State.MoveTargets.Clear();
            State.CaptureTargets.Clear();
            HiddenTargetCount = 0;
            HasPendingPromotion = false;
        }

        private void MoveTo(int to)
        {
            int from = State.SelectedCell;
            Game.LegalFrom(from, fromList);
            Move? single = null;
            int promotions = 0;
            for (int i = 0; i < fromList.Count; i++)
            {
                Move m = fromList[i];
                if (m.To != to) continue;
                if (m.IsPromotion) promotions++;
                single = m;
            }
            if (promotions > 1)
            {
                HasPendingPromotion = true;
                PendingFrom = from;
                PendingTo = to;
                Message = "Choose a promotion piece";
                return;
            }
            if (single.HasValue) Commit(single.Value);
        }

        public void ChoosePromotion(PieceType type)
        {
            if (!HasPendingPromotion) return;
            Game.LegalFrom(PendingFrom, fromList);
            for (int i = 0; i < fromList.Count; i++)
            {
                Move m = fromList[i];
                if (m.To == PendingTo && m.Promotion == type) { HasPendingPromotion = false; Commit(m); return; }
            }
            HasPendingPromotion = false;
        }

        private void Commit(in Move m)
        {
            byte mover = Board.GetPiece(m.From);
            if (!Game.TryMove(m)) { Message = "Illegal move"; return; }
            Message = "Played " + Game.HistoryLine(Game.History.Count - 1) + StatusSuffix();
            Deselect();
            Moved?.Invoke(m, Piece.WithoutMoved(mover), false);
        }

        private string StatusSuffix()
        {
            switch (Game.Status)
            {
                case GameStatus.Checkmate: return "   CHECKMATE, " + Piece.Opposite(Board.SideToMove) + " wins";
                case GameStatus.Stalemate: return "   STALEMATE";
                case GameStatus.DrawFiftyMove: return "   draw by the fifty-move rule";
                case GameStatus.DrawRepetition: return "   draw by repetition";
                default: return Board.InCheck() ? "   CHECK" : "";
            }
        }

        public string StatusLine()
        {
            if (Mode == GameMode.Setup) return "SETUP MODE   to move: " + Board.SideToMove;
            switch (Game.Status)
            {
                case GameStatus.Checkmate: return "Checkmate. " + Piece.Opposite(Board.SideToMove) + " wins.";
                case GameStatus.Stalemate: return "Stalemate. Draw.";
                case GameStatus.DrawFiftyMove: return "Draw by the fifty-move rule.";
                case GameStatus.DrawRepetition: return "Draw by threefold repetition.";
                default:
                    return Board.SideToMove + " (" + Players[(int)Board.SideToMove] + ") to move" + (Board.InCheck() ? "   CHECK" : "") + "   (" + Game.Legal.Count + " legal moves)"
                        + (Thinking ? "   thinking..." : "");
            }
        }

        /// <summary>Typed input: a full move "(from) (to)[=X]" plays it; a single coordinate selects (or paints in setup mode).</summary>
        public bool OnTyped(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (Mode == GameMode.Play && Notation.TryParseMove(Board, Game.Legal, text, out Move m))
            {
                Commit(m);
                return true;
            }
            if (Coord.TryParse(text.Trim(), Board.G.Dimensions, out Coord c))
            {
                for (int a = 0; a < c.Dimensions; a++) if (c[a] >= Board.G.Side) { Message = "Coordinate off board"; return false; }
                int cell = Board.G.CellOf(c);
                State.PageTo(cell);
                OnCellClicked(cell);
                return true;
            }
            Message = text.Trim().IndexOf(')') < text.Trim().Length - 1
                ? "Illegal move: " + text.Trim() + (Mode == GameMode.Setup ? " (in setup mode)" : Board.InCheck() ? " (you are in check)" : "")
                : "Could not read \"" + text + "\": use (x,y,z,w) or (from) (to)";
            return false;
        }

        public bool SelectTyped(string text) { return OnTyped(text); }

        public void Undo()
        {
            if (Mode != GameMode.Play || Thinking) return;
            if (Game.History.Count == 0) return;
            Move m = Game.History[Game.History.Count - 1];
            if (Game.Undo())
            {
                Deselect();
                Message = "Undid a move";
                Moved?.Invoke(m, Piece.WithoutMoved(Board.GetPiece(m.From)), true);
            }
        }

        public void Redo()
        {
            if (Mode != GameMode.Play || Thinking) return;
            if (Game.Redo())
            {
                Move m = Game.History[Game.History.Count - 1];
                Deselect();
                Message = "Redid " + Game.HistoryLine(Game.History.Count - 1);
                Moved?.Invoke(m, Piece.WithoutMoved(Board.GetPiece(m.To)), false);
            }
        }

        public void NewGame()
        {
            StartPosition.Setup(Board);
            Game.ResetHistory();
            Deselect();
            Mode = GameMode.Play;
            Message = "New game";
            UpdateOrientation(true);
        }

        // ------------------------------------------------------------ setup mode

        public void EnterSetup()
        {
            Mode = GameMode.Setup;
            Deselect();
            Message = "Setup: click a cell or type a coordinate to place the brush";
        }

        public void ExitSetup()
        {
            Mode = GameMode.Play;
            Game.ResetHistory();
            Deselect();
            UpdateOrientation(true);
            Message = "Playing from the edited position" + (Board.KingCell(Side.White) < 0 || Board.KingCell(Side.Black) < 0 ? " (a side has no king)" : "");
        }

        private void PlaceBrush(int cell)
        {
            if (SetupErase) Board.RemovePiece(cell);
            else
            {
                bool moved = false;
                if (SetupBrush == PieceType.Pawn)
                {
                    int y = Board.G.Coord(cell, BoardGeometry.AdvanceAxis);
                    moved = SetupColor == Side.White ? y > 1 : y < Board.G.Side - 2;
                }
                Board.PlacePiece(cell, Piece.Make(SetupBrush, SetupColor, moved));
            }
            Game.ResetHistory();
            State.SelectedCell = cell;
            Message = (SetupErase ? "Cleared " : "Placed " + SetupColor + " " + SetupBrush + " at ") + Board.G.CoordOf(cell);
        }

        public void ClearBoard() { Board.Clear(); Game.ResetHistory(); Deselect(); Message = "Board cleared"; }
        public void StandardStart() { StartPosition.Setup(Board); Game.ResetHistory(); Deselect(); Message = "Standard start placed"; }
        public void ToggleSideToMove() { Board.SetSideToMove(Piece.Opposite(Board.SideToMove)); Game.ResetHistory(); UpdateOrientation(true); }

        // ------------------------------------------------------------ save / load

        public string SavePosition(string name)
        {
            string text = PositionText.Save(Board);
            Directory.CreateDirectory(PositionsDirectory);
            string file = Path.Combine(PositionsDirectory, SafeName(name) + ".txt");
            File.WriteAllText(file, text);
            GUIUtility.systemCopyBuffer = text;
            Message = "Saved to " + file + " and copied to the clipboard";
            return file;
        }

        public bool LoadPosition(string name)
        {
            string file = Path.Combine(PositionsDirectory, SafeName(name) + ".txt");
            if (!File.Exists(file)) { Message = "No file " + file; return false; }
            return LoadPositionText(File.ReadAllText(file), file);
        }

        public bool LoadClipboard() { return LoadPositionText(GUIUtility.systemCopyBuffer, "clipboard"); }

        public bool LoadPositionText(string text, string source)
        {
            try
            {
                PositionText.Load(Board, text);
            }
            catch (FormatException e)
            {
                Message = "Load failed: " + e.Message;
                return false;
            }
            Game.ResetHistory();
            Deselect();
            Mode = GameMode.Play;
            Message = "Loaded " + source;
            UpdateOrientation(true);
            return true;
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "position";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        // ------------------------------------------------------------ view helpers

        public void CycleIsolateMode()
        {
            State.IsolateMode = (IsolateMode)(((int)State.IsolateMode + 1) % 3);
            if (State.IsolateSlot < 0) State.IsolateSlot = 1;
        }

        public void CycleIsolateSlot() { State.IsolateSlot = (State.IsolateSlot + 1) % AxisView.VisibleSlots; }
    }
}
