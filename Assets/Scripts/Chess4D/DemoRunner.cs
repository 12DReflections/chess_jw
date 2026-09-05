using System;
using System.Collections;
using System.IO;
using Chess4D.Core;
using UnityEngine;

namespace Chess4D.Unity
{
    /// <summary>
    /// Scripted walkthrough for the Stage 3 gate: launch the player with
    /// "-chess4d-demo &lt;directory&gt;" and it rotates through all four perspectives,
    /// pages through all eight layers, exercises the strip and isolation, writes a
    /// screenshot at each step, then quits.
    /// </summary>
    public sealed class DemoRunner : MonoBehaviour
    {
        private Chess4DGame game;
        private string dir;
        private int shot;

        public static void StartIfRequested(Chess4DGame game)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-chess4d-demo")
                {
                    var runner = game.gameObject.AddComponent<DemoRunner>();
                    runner.game = game;
                    runner.dir = args[i + 1];
                    Directory.CreateDirectory(runner.dir);
                    Debug.Log("Chess4D demo mode, writing to " + runner.dir);
                    runner.StartCoroutine(runner.Run());
                    return;
                }
            }
        }

        private const int Width = 1280, Height = 800;
        private RenderTexture rt;
        private Texture2D tex;

        /// <summary>Renders the main camera (with the HUD in camera space) into a texture and saves it. Independent of window focus or occlusion.</summary>
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
            File.AppendAllText(Path.Combine(dir, "log.txt"), Path.GetFileName(path) + "  " + game.State.Describe() + "\n");
        }

        private IEnumerator Run()
        {
            var s = game.State;
            var g = game.Board.G;
            yield return null; yield return null; yield return null;
            yield return Shot("start-xyz-page-w3");

            s.SelectedCell = g.CellOf(4, 1, 3, 3);
            yield return null;
            yield return Shot("selected-4133-strip");

            s.SelectedCell = g.CellOf(2, 0, 3, 3);
            yield return null;
            yield return Shot("selected-bishop-2033-strip");

            // Scrubbed rotation toward (x,y,w): hold at 45, then release past the snap point.
            s.Arm(Chess4DGame.Perspectives[1]);
            s.Scrub(20f); yield return null;
            yield return Shot("scrub-xyz-to-xyw-phi20");
            s.Scrub(45f); yield return null;
            yield return Shot("scrub-xyz-to-xyw-phi45");
            s.Scrub(70f); yield return null;
            yield return Shot("scrub-xyz-to-xyw-phi70");
            s.Release();
            while (s.Armed) yield return null;
            yield return Shot("arrived-xyw");

            // Scrub below 45 and release: springs back.
            s.Arm(Chess4DGame.Perspectives[2]);
            s.Scrub(30f); yield return null;
            s.Release();
            while (s.Armed) yield return null;
            yield return Shot("sprang-back-xyw");

            // Timed sweeps through the remaining perspectives, one mid-sweep capture each.
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

            // Page through every layer of the hidden axis.
            for (int v = 0; v < g.Side; v++)
            {
                s.SetPage(AxisView.VisibleSlots, v);
                yield return null;
                yield return Shot("page-" + AxisView.AxisNames[s.View.AxisAtSlot(AxisView.VisibleSlots)] + v);
            }
            s.SetPage(AxisView.VisibleSlots, 3);

            // Typed coordinate selection pages the view to the cell.
            game.SelectTyped("(6,7,2,4)");
            yield return null;
            yield return Shot("typed-select-6724");

            // Layer isolation along the vertical screen axis.
            s.SelectedCell = g.CellOf(4, 1, 3, 3);
            s.PageTo(s.SelectedCell);
            s.IsolateSlot = 1;
            s.IsolateMode = IsolateMode.HideAbove;
            yield return null;
            yield return Shot("isolate-hide-above-y1");
            s.IsolateMode = IsolateMode.Off;

            // Orbit and zoom are separate from rotation.
            game.Orbit.Drag(220f, -40f);
            game.Orbit.Zoom(2f);
            yield return null;
            yield return Shot("orbited-and-zoomed");

            File.AppendAllText(Path.Combine(dir, "log.txt"), "done\n");
            yield return null;
            Application.Quit();
        }
    }
}
