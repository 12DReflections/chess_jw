using System;
using System.Collections;
using System.IO;
using Chess4D.Core;
using UnityEngine;
using Side = Chess4D.Core.Color;

namespace Chess4D.Unity
{
    /// <summary>
    /// Scripted walkthroughs for the stage gates. "-chess4d-demo &lt;dir&gt;" runs the
    /// Stage 3 view walkthrough; "-chess4d-demo-game &lt;dir&gt;" runs the Stage 4
    /// game walkthrough. Each writes numbered screenshots and log.txt, then quits.
    /// Captures go through the camera into a render texture so they do not depend
    /// on window focus or occlusion.
    /// </summary>
    public sealed class DemoRunner : MonoBehaviour
    {
        private Chess4DGame game;
        private string dir;
        private int shot;
        private const int Width = 1280, Height = 800;
        private RenderTexture rt;
        private Texture2D tex;

        public static void StartIfRequested(Chess4DGame game)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-chess4d-demo" || args[i] == "-chess4d-demo-game" || args[i] == "-chess4d-demo-engine")
                {
                    var runner = game.gameObject.AddComponent<DemoRunner>();
                    runner.game = game;
                    runner.dir = args[i + 1];
                    Directory.CreateDirectory(runner.dir);
                    Debug.Log("Chess4D demo mode, writing to " + runner.dir);
                    runner.StartCoroutine(args[i] == "-chess4d-demo" ? runner.RunView() : args[i] == "-chess4d-demo-game" ? runner.RunGame() : runner.RunEngine());
                    return;
                }
            }
        }

        private void Log(string line) { File.AppendAllText(Path.Combine(dir, "log.txt"), line + "\n"); }

        private IEnumerator Shot(string name)
        {
            yield return null;
            if (rt == null)
            {
                rt = new RenderTexture(Width, Height, 24);
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var canvas = game.Hud.Canvas;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }
            Camera cam = Camera.main;
            game.View.SubmitCells();
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string path = Path.Combine(dir, (++shot).ToString("D2") + "-" + name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Log(Path.GetFileName(path) + "  " + game.State.Describe() + "  |  " + game.StatusLine() + "  |  " + game.Message);
        }

        private IEnumerator Finish()
        {
            Log("done");
            yield return null;
            Application.Quit();
        }

        // ------------------------------------------------------------ Stage 3 view walkthrough

        private IEnumerator RunView()
        {
            var s = game.State;
            var g = game.Board.G;
            yield return null; yield return null; yield return null;
            yield return Shot("start-xyz-page-w3");
            s.SelectedCell = g.CellOf(4, 1, 3, 3);
            yield return Shot("selected-4133-strip");
            s.SelectedCell = g.CellOf(2, 0, 3, 3);
            yield return Shot("selected-bishop-2033-strip");
            s.Arm(Chess4DGame.Perspectives[1]);
            s.Scrub(20f); yield return Shot("scrub-xyz-to-xyw-phi20");
            s.Scrub(45f); yield return Shot("scrub-xyz-to-xyw-phi45");
            s.Scrub(70f); yield return Shot("scrub-xyz-to-xyw-phi70");
            s.Release();
            while (s.Armed) yield return null;
            yield return Shot("arrived-xyw");
            s.Arm(Chess4DGame.Perspectives[2]);
            s.Scrub(30f); yield return null;
            s.Release();
            while (s.Armed) yield return null;
            yield return Shot("sprang-back-xyw");
            s.SweepTo(Chess4DGame.Perspectives[2]);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("sweep-to-xzw-mid");
            while (s.Armed) yield return null;
            yield return Shot("arrived-xzw");
            s.SweepTo(Chess4DGame.Perspectives[3]);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("sweep-to-yzw-mid");
            while (s.Armed) yield return null;
            yield return Shot("arrived-yzw");
            s.SweepTo(Chess4DGame.Perspectives[0]);
            while (s.Armed) yield return null;
            yield return Shot("back-to-xyz");
            for (int v = 0; v < g.Side; v++)
            {
                s.SetPage(AxisView.VisibleSlots, v);
                yield return Shot("page-" + AxisView.AxisNames[s.View.AxisAtSlot(AxisView.VisibleSlots)] + v);
            }
            s.SetPage(AxisView.VisibleSlots, 3);
            game.OnTyped("(6,7,2,4)");
            yield return Shot("typed-select-6724");
            s.SelectedCell = g.CellOf(4, 1, 3, 3);
            s.PageTo(s.SelectedCell);
            s.IsolateSlot = 1;
            s.IsolateMode = IsolateMode.HideAbove;
            yield return Shot("isolate-hide-above-y1");
            s.IsolateMode = IsolateMode.Off;
            game.Orbit.Drag(220f, -40f);
            game.Orbit.Zoom(2f);
            yield return Shot("orbited-and-zoomed");
            yield return Finish();
        }

        // ------------------------------------------------------------ Stage 5 engine, threats and animation walkthrough

        private IEnumerator RunEngine()
        {
            var s = game.State;
            var b = game.Board;
            var g = b.G;
            yield return null; yield return null; yield return null;

            // 1. Threats along a hidden axis: a black rook at w=0 attacks a white knight at w=5 through the strip.
            game.EnterSetup();
            game.ClearBoard();
            game.SetupColor = Side.White; game.SetupBrush = PieceType.King; game.OnTyped("4033");
            game.SetupColor = Side.Black; game.SetupBrush = PieceType.King; game.OnTyped("4733");
            game.SetupBrush = PieceType.Rook; game.OnTyped("2230");
            game.SetupColor = Side.White; game.SetupBrush = PieceType.Knight; game.OnTyped("2235");
            game.SetupBrush = PieceType.Pawn; game.OnTyped("2133");
            if (b.SideToMove != Side.White) game.ToggleSideToMove();
            game.ExitSetup();
            game.OnTyped("2235");
            yield return Shot("threats-strip-knight-attacked-along-w");
            Log("knight at 2235 attacked: " + game.Attacks.IsPieceAttacked(g.CellOf(2, 2, 3, 5)) + "; rook at 2230 attacked: " + game.Attacks.IsPieceAttacked(g.CellOf(2, 2, 3, 0)));

            // 2. Animation, case both visible. NewGame never moves the view, so page back to w=3 by hand.
            game.NewGame();
            s.SetPage(AxisView.VisibleSlots, 3);
            yield return null;
            game.OnTyped("2133-2333");
            yield return new WaitForSeconds(0.12f);
            Log("both visible mid: piece animations " + game.View.AnimCount + ", ghosts " + game.View.GhostCount + ", progress of 2333: " + game.View.AnimProgress(g.CellOf(2, 3, 3, 3)).ToString("F2"));
            yield return Shot("anim-both-visible-mid");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("anim-both-visible-done");

            // 3. Case one visible: Black knight leaves the visible layer (w=3 to w=1).
            game.OnTyped("N1733-1631");
            yield return new WaitForSeconds(0.15f);
            Log("from visible mid: piece animations " + game.View.AnimCount + ", ghosts " + game.View.GhostCount);
            yield return Shot("anim-from-visible-ghost-mid");
            Log("black knight moved to 1631: " + Piece.TypeOf(b.GetPiece(g.CellOf(1, 6, 3, 1))) + ", visible? " + s.IsVisibleInVolume(g.CellOf(1, 6, 3, 1)));
            yield return new WaitForSeconds(0.6f);

            // 4. Case neither visible: a white pawn moves entirely inside layer w=2.
            game.OnTyped("0122-0222");
            yield return new WaitForSeconds(0.1f);
            Log("neither visible: piece animations " + game.View.AnimCount + ", ghosts " + game.View.GhostCount + " (expected 0 and 0)");
            yield return Shot("anim-neither-visible-history-flash-and-strip-mark");
            Log("history after hidden move: " + game.Game.HistoryLine(game.Game.History.Count - 1) + "; message: " + game.Message);
            yield return new WaitForSeconds(0.4f);

            // 5. Jump to last move pages the view to w=2.
            game.JumpToLastMove();
            yield return Shot("jump-to-last-move");
            Log("after jump: " + s.Describe());

            // 6. Case to visible only: the black knight comes back into the visible layer (w=2 now visible).
            game.OnTyped("N1631-1632"); // not a knight move; expect refusal
            Log("bogus knight move refused: " + game.Message);
            game.OnTyped("N1631-1432");
            yield return new WaitForSeconds(0.15f);
            Log("to visible mid: piece animations " + game.View.AnimCount + ", ghosts " + game.View.GhostCount + ", progress of 1432: " + game.View.AnimProgress(g.CellOf(1, 4, 3, 2)).ToString("F2"));
            yield return Shot("anim-to-visible-fade-in-mid");
            yield return new WaitForSeconds(0.6f);

            // 7. Engine plays White with a 300 ms budget.
            game.EngineTimeIndex = 0;
            game.TogglePlayer(Side.White);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("engine-thinking");
            float waited = 0f;
            while ((game.Thinking || game.Board.SideToMove == Side.White) && waited < 10f) { waited += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);
            yield return Shot("engine-moved");
            Log("engine move: " + game.Game.HistoryLine(game.Game.History.Count - 1) + "; info: " + game.ThinkingInfo);

            // 8. Engine vs engine for a few plies.
            game.TogglePlayer(Side.Black);
            int target = game.Game.History.Count + 6;
            waited = 0f;
            while (game.Game.History.Count < target && waited < 30f) { waited += Time.deltaTime; yield return null; }
            yield return Shot("engine-vs-engine");
            Log("engine vs engine reached " + game.Game.History.Count + " plies; last: " + game.Game.HistoryLine(game.Game.History.Count - 1));
            game.TogglePlayer(Side.White); game.TogglePlayer(Side.Black);
            yield return Finish();
        }

        // ------------------------------------------------------------ Stage 4 game walkthrough

        private IEnumerator RunGame()
        {
            var s = game.State;
            var b = game.Board;
            var g = b.G;
            yield return null; yield return null; yield return null;

            // 0. Board orientation: White to move sees White near; after a move the board turns to face Black.
            yield return Shot("orientation-white-to-move-white-near");
            Log("orientation yaw at start: " + game.Orbit.OrientationYaw + " (White to move, expected 0)");
            game.OnTyped("2133-2333");
            yield return new WaitForSeconds(0.55f);
            yield return Shot("orientation-flip-mid");
            Log("flipping mid: " + game.Orbit.Flipping + ", yaw " + game.Orbit.OrientationYaw.ToString("F0"));
            yield return new WaitForSeconds(0.5f);
            yield return Shot("orientation-black-to-move-black-near");
            Log("orientation yaw after White's move: " + game.Orbit.OrientationYaw.ToString("F0") + " (expected 180); history " + game.Game.History.Count + " move, flip is not a move");
            s.SweepTo(Chess4DGame.Perspectives[2]);
            while (s.Armed) yield return null;
            yield return null;
            yield return Shot("orientation-suppressed-y-hidden");
            Log("perspective without y: note = " + game.OrientationNote);
            s.SweepTo(Chess4DGame.Perspectives[0]);
            while (s.Armed) yield return null;
            while (game.Orbit.Flipping) yield return null; // the orientation adjusts to the new view; let it settle
            game.ToggleAutoFlip();
            Log("auto-flip off; yaw before Black's move: " + game.Orbit.OrientationYaw.ToString("F0"));
            game.OnTyped("3633-3433");
            yield return new WaitForSeconds(1.1f);
            yield return Shot("orientation-autoflip-off-stays");
            Log("auto-flip off after Black's move: yaw " + game.Orbit.OrientationYaw.ToString("F0") + " (expected unchanged from before the move: 0, since y is reflected in this view), note = " + game.OrientationNote);
            game.ToggleAutoFlip();
            yield return new WaitForSeconds(0.6f);
            game.NewGame();
            yield return new WaitForSeconds(0.6f);

            // 1. Click-driven play: select a pawn, see targets, move; the ply-3 check line.
            game.OnCellClicked(g.CellOf(2, 1, 3, 3));
            yield return Shot("play-pawn-selected-targets");
            game.OnCellClicked(g.CellOf(2, 3, 3, 3));
            game.OnCellClicked(g.CellOf(3, 6, 3, 3));
            game.OnCellClicked(g.CellOf(3, 4, 3, 3));
            game.OnCellClicked(g.CellOf(3, 0, 3, 3));
            yield return Shot("play-queen-selected-targets");
            game.OnCellClicked(g.CellOf(0, 3, 3, 3));
            yield return Shot("play-queen-gives-check");
            Log("in check after ply 3: " + b.InCheck() + ", status " + game.Game.Status + ", history: " + string.Join(" / ", game.Game.HistoryText));

            // 2. Undo and redo.
            game.Undo(); game.Undo();
            yield return Shot("undo-twice");
            Log("after two undos: " + game.Game.History.Count + " moves, redo " + game.Game.RedoCount);
            game.Redo(); game.Redo();
            yield return Shot("redo-twice");
            Log("after two redos: " + game.Game.History.Count + " moves, in check " + b.InCheck());

            // 3. Typed moves, including a knight reply that blocks nothing but is legal.
            bool typed = game.OnTyped("(4,7,3,3) (4,6,3,3)");
            Log("typed king move accepted: " + typed + " (expected False: (4,6,3,3) is occupied)");
            typed = game.OnTyped("(1,6,3,3)-(1,5,3,3)");
            Log("typed pawn move while in check accepted: " + typed + " (expected False) message: " + game.Message);
            Move reply = game.Game.Legal[0];
            typed = game.OnTyped(g.CoordOf(reply.From) + " " + g.CoordOf(reply.To));
            Log("typed legal reply accepted: " + typed + " -> " + (typed ? game.Game.HistoryText[game.Game.History.Count - 1] : "-"));
            yield return Shot("typed-move-played");

            // 4. Promotion dialog.
            game.EnterSetup();
            game.ClearBoard();
            game.SetupColor = Side.White; game.SetupBrush = PieceType.King; game.OnTyped("(4,0,3,3)");
            game.SetupColor = Side.Black; game.SetupBrush = PieceType.King; game.OnTyped("(4,7,0,0)");
            game.SetupColor = Side.White; game.SetupBrush = PieceType.Pawn; game.OnTyped("(0,6,3,3)");
            if (b.SideToMove != Side.White) game.ToggleSideToMove();
            game.ExitSetup();
            s.PageTo(g.CellOf(0, 6, 3, 3));
            game.OnCellClicked(g.CellOf(0, 6, 3, 3));
            game.OnCellClicked(g.CellOf(0, 7, 3, 3));
            yield return Shot("promotion-dialog");
            Log("promotion pending: " + game.HasPendingPromotion);
            game.ChoosePromotion(PieceType.Knight);
            yield return Shot("promoted-to-knight");
            Log("promotion result: " + game.Game.HistoryText[0] + ", piece at (0,7,3,3): " + Piece.TypeOf(b.GetPiece(g.CellOf(0, 7, 3, 3))));

            // 5. Endgame setup: K+Q vs K, played on, saved, reloaded.
            game.EnterSetup();
            yield return Shot("setup-mode");
            game.ClearBoard();
            game.SetupColor = Side.White; game.SetupBrush = PieceType.King; game.OnTyped("(4,0,3,3)");
            game.SetupBrush = PieceType.Queen; game.OnTyped("(3,0,3,3)");
            game.SetupColor = Side.Black; game.SetupBrush = PieceType.King; game.OnTyped("(4,7,3,3)");
            if (b.SideToMove != Side.White) game.ToggleSideToMove();
            yield return Shot("setup-kqk-placed");
            game.ExitSetup();
            var rng = new System.Random(7);
            for (int i = 0; i < 8 && !game.Game.IsOver; i++)
            {
                MoveList legal = game.Game.Legal;
                Move m = legal[rng.Next(legal.Count)];
                s.PageTo(m.From);
                game.OnCellClicked(m.From);
                if (i == 0) yield return Shot("kqk-queen-targets");
                s.PageTo(m.To);
                game.OnCellClicked(m.To);
            }
            yield return Shot("kqk-played-8-plies");
            Log("K+Q vs K after 8 plies: " + string.Join(" / ", game.Game.HistoryText) + " | status " + game.Game.Status);
            string file = game.SavePosition("demo-kqk");
            ulong savedHash = b.Hash;
            string savedText = File.ReadAllText(file);
            game.NewGame();
            bool loaded = game.LoadPosition("demo-kqk");
            Log("saved to " + file + "; loaded back: " + loaded + "; hash equal: " + (b.Hash == savedHash));
            Log("saved text:\n" + savedText);
            yield return Shot("kqk-reloaded-from-file");

            // 6. A game played to checkmate: smothered corner king, knight delivers mate.
            game.EnterSetup();
            game.ClearBoard();
            game.SetupColor = Side.Black; game.SetupBrush = PieceType.King; game.OnTyped("(0,0,0,0)");
            game.SetupBrush = PieceType.Pawn;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++) for (int w = 0; w < 2; w++)
                if (x + y + z + w > 0) game.OnTyped("(" + x + "," + y + "," + z + "," + w + ")");
            game.SetupColor = Side.White; game.SetupBrush = PieceType.King; game.OnTyped("(7,7,7,7)");
            game.SetupBrush = PieceType.Knight; game.OnTyped("(4,2,0,0)");
            if (b.SideToMove != Side.White) game.ToggleSideToMove();
            game.ExitSetup();
            s.PageTo(g.CellOf(4, 2, 0, 0));
            game.OnCellClicked(g.CellOf(4, 2, 0, 0));
            yield return Shot("mate-knight-selected");
            game.OnCellClicked(g.CellOf(2, 1, 0, 0));
            yield return Shot("checkmate");
            Log("final status: " + game.Game.Status + ", history: " + string.Join(" / ", game.Game.HistoryText));
            yield return Finish();
        }
    }
}
