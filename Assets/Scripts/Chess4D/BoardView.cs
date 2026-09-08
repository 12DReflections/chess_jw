using System.Collections.Generic;
using Chess4D.Core;
using UnityEngine;
using Color = UnityEngine.Color;
using Side = Chess4D.Core.Color;
using UnityEngine.Rendering;

namespace Chess4D.Unity
{
    /// <summary>
    /// Draws the 288 pieces as GameObjects whose positions and opacity follow the
    /// view each frame, and the visible 8x8x8 lattice as GPU-instanced cubes:
    /// occupied cells as translucent team-coloured cubes, empty cells as faint markers.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private struct InstanceData { public Matrix4x4 objectToWorld; }

        private sealed class Anim
        {
            public int Cell;          // the cell whose piece is animating (arrival / full move)
            public Vector3 From, To;
            public float T, Duration;
            public bool FadeIn;
        }

        private sealed class Ghost
        {
            public GameObject Go;
            public MeshRenderer Renderer;
            public Vector3 From, To;
            public float T, Duration;
            public Color Tint;
        }

        public const float MoveSeconds = 0.35f;
        public const float HalfSeconds = 0.45f;
        private readonly Dictionary<int, Anim> anims = new Dictionary<int, Anim>();
        private readonly List<Ghost> ghosts = new List<Ghost>();
        private readonly List<int> finished = new List<int>();
        public bool ShowThreats = true;
        public System.Func<int, bool> IsThreatened;
        public Color ThreatMarker = new Color(1f, 0.2f, 0.15f, 0.9f);
        private Material threatMat;
        private InstanceData[] threatInst = new InstanceData[512];

        private sealed class PieceObj
        {
            public GameObject Go;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public byte Piece;
            public Vector3 MeshOffset;
            public float MeshScale;
        }

        public Color WhiteTint = new Color(1f, 0.97f, 0.9f);
        public Color BlackTint = new Color(0.55f, 0.55f, 0.6f);
        public Color WhiteCell = new Color(0.95f, 0.9f, 0.6f, 0.14f);
        public Color BlackCell = new Color(0.45f, 0.55f, 1.0f, 0.16f);
        public Color EmptyCell = new Color(0.6f, 0.6f, 0.65f, 0.35f);
        public Color SelectedCell = new Color(1f, 0.55f, 0.1f, 0.45f);
        public Color HoverCell = new Color(1f, 1f, 1f, 0.3f);
        public Color MoveTarget = new Color(0.3f, 1f, 0.4f, 0.75f);
        public Color CaptureTarget = new Color(1f, 0.25f, 0.2f, 0.8f);
        public float DistanceFadeNear = 6f;
        public float DistanceFadeFar = 34f;

        private ViewState state;
        private Mesh[][] meshes;
        private Material pieceMaterial;
        private Material cellEmptyMat, cellWhiteMat, cellBlackMat, cellSelectedMat, cellHoverMat, targetMat, captureMat;
        private InstanceData[] targetInst = new InstanceData[512];
        private InstanceData[] captureInst = new InstanceData[512];
        private Mesh cubeMesh;
        private readonly Dictionary<int, PieceObj> live = new Dictionary<int, PieceObj>();
        private readonly Stack<PieceObj> pool = new Stack<PieceObj>();
        private readonly List<int> cellsScratch = new List<int>(512);
        private readonly List<int> toRemove = new List<int>();
        private MaterialPropertyBlock mpb;
        private InstanceData[] emptyInst = new InstanceData[512];
        private InstanceData[] whiteInst = new InstanceData[512];
        private InstanceData[] blackInst = new InstanceData[512];
        private InstanceData[] oneInst = new InstanceData[1];
        private Camera cam;

        public void Init(ViewState s, Mesh[][] pieceMeshes, Material pieceFade, Material cellFade)
        {
            state = s;
            meshes = pieceMeshes;
            pieceMaterial = new Material(pieceFade);
            pieceMaterial.enableInstancing = true;
            cellEmptyMat = Tinted(cellFade, EmptyCell);
            cellWhiteMat = Tinted(cellFade, WhiteCell);
            cellBlackMat = Tinted(cellFade, BlackCell);
            cellSelectedMat = Tinted(cellFade, SelectedCell);
            cellHoverMat = Tinted(cellFade, HoverCell);
            targetMat = Tinted(cellFade, MoveTarget);
            captureMat = Tinted(cellFade, CaptureTarget);
            threatMat = Tinted(cellFade, ThreatMarker);
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            mpb = new MaterialPropertyBlock();
            cam = Camera.main;
        }

        private static Material Tinted(Material baseMat, Color c)
        {
            var m = new Material(baseMat);
            m.color = c;
            m.enableInstancing = true;
            return m;
        }

        private void LateUpdate()
        {
            if (state == null) return;
            StepAnimations(Time.deltaTime);
            SyncPieces();
            DrawCells();
        }

        // ------------------------------------------------------------ animation (spec Stage 5, three cases)

        /// <summary>Called after a move (or undo). Decides the case from which cells are in the visible volume.</summary>
        public void AnimateMove(int from, int to, byte piece, bool reverse)
        {
            if (reverse) { int t = from; from = to; to = t; }
            bool visFrom = state.IsVisibleInVolume(from) && !state.Rotating;
            bool visTo = state.IsVisibleInVolume(to) && !state.Rotating;
            Vector3 wFrom = state.WorldOf(from);
            Vector3 wTo = state.WorldOf(to);
            Vector3 dv = wTo - wFrom;
            if (dv.sqrMagnitude < 1e-4f) dv = Vector3.up * 0.8f; // pure hidden-axis move: no visible displacement, lift instead
            if (visFrom && visTo)
            {
                anims[to] = new Anim { Cell = to, From = wFrom, To = wTo, T = 0f, Duration = MoveSeconds, FadeIn = false };
            }
            else if (visFrom)
            {
                SpawnGhost(piece, wFrom, wFrom + dv * 0.5f);
            }
            else if (visTo)
            {
                anims[to] = new Anim { Cell = to, From = wTo - dv * 0.5f, To = wTo, T = 0f, Duration = HalfSeconds, FadeIn = true };
            }
            // neither visible: nothing to draw here; the HUD flashes the history line and marks the layer.
        }

        public bool IsAnimating { get { return anims.Count > 0 || ghosts.Count > 0; } }
        public int AnimCount { get { return anims.Count; } }
        public int GhostCount { get { return ghosts.Count; } }
        /// <summary>Progress 0..1 of the animation on <paramref name="cell"/>, or -1.</summary>
        public float AnimProgress(int cell) { return anims.TryGetValue(cell, out Anim a) ? Mathf.Clamp01(a.T / a.Duration) : -1f; }

        private void SpawnGhost(byte piece, Vector3 from, Vector3 to)
        {
            var go = new GameObject("ghost");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            Mesh mesh = meshes[(int)Piece.ColorOf(piece)][(int)Piece.TypeOf(piece)];
            mf.sharedMesh = mesh;
            mr.sharedMaterial = pieceMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Bounds bd = mesh.bounds;
            float horiz = Mathf.Max(bd.size.x, bd.size.z, 1e-4f);
            float scale = Mathf.Min(0.72f / Mathf.Max(bd.size.y, 1e-4f), 0.55f / horiz);
            go.transform.localScale = Vector3.one * scale;
            Vector3 offset = -bd.center * scale;
            ghosts.Add(new Ghost { Go = go, Renderer = mr, From = from + offset, To = to + offset, T = 0f, Duration = HalfSeconds, Tint = Piece.ColorOf(piece) == Side.White ? WhiteTint : BlackTint });
        }

        private void StepAnimations(float dt)
        {
            finished.Clear();
            foreach (var kv in anims) { kv.Value.T += dt; if (kv.Value.T >= kv.Value.Duration) finished.Add(kv.Key); }
            foreach (int c in finished) anims.Remove(c);
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost gh = ghosts[i];
                gh.T += dt;
                float u = Mathf.Clamp01(gh.T / gh.Duration);
                float e = u * u * (3f - 2f * u);
                gh.Go.transform.position = Vector3.Lerp(gh.From, gh.To, e);
                Color c = gh.Tint; c.a = 1f - u;
                mpb.SetColor("_Color", c);
                gh.Renderer.SetPropertyBlock(mpb);
                if (u >= 1f) { Destroy(gh.Go); ghosts.RemoveAt(i); }
            }
        }

        /// <summary>Re-submits the instanced cell draws, for a manual camera render in the same frame.</summary>
        public void SubmitCells() { DrawCells(); }

        // ------------------------------------------------------------ pieces

        private void SyncPieces()
        {
            Board b = state.Board;
            toRemove.Clear();
            foreach (var kv in live) if (b.GetPiece(kv.Key) != kv.Value.Piece) toRemove.Add(kv.Key);
            foreach (int cell in toRemove) Release(cell);
            for (int c = 0; c < 2; c++)
            {
                b.GetPieceCells((Side)c, cellsScratch);
                foreach (int cell in cellsScratch)
                {
                    byte p = b.GetPiece(cell);
                    if (!live.TryGetValue(cell, out PieceObj obj)) { obj = Acquire(cell, p); live[cell] = obj; }
                    Place(cell, obj);
                }
            }
        }

        private PieceObj Acquire(int cell, byte piece)
        {
            PieceObj obj = pool.Count > 0 ? pool.Pop() : Create();
            obj.Go.SetActive(true);
            obj.Piece = piece;
            Mesh mesh = meshes[(int)Piece.ColorOf(piece)][(int)Piece.TypeOf(piece)];
            obj.Filter.sharedMesh = mesh;
            Bounds bd = mesh.bounds;
            float horiz = Mathf.Max(bd.size.x, bd.size.z, 1e-4f);
            obj.MeshScale = Mathf.Min(0.72f / Mathf.Max(bd.size.y, 1e-4f), 0.55f / horiz);
            obj.MeshOffset = -bd.center * obj.MeshScale;
            obj.Go.transform.localScale = Vector3.one * obj.MeshScale;
            obj.Go.name = Piece.ToChar(piece) + " " + state.G.CoordOf(cell);
            return obj;
        }

        private PieceObj Create()
        {
            var go = new GameObject("piece");
            go.transform.SetParent(transform, false);
            var obj = new PieceObj
            {
                Go = go,
                Filter = go.AddComponent<MeshFilter>(),
                Renderer = go.AddComponent<MeshRenderer>(),
            };
            obj.Renderer.sharedMaterial = pieceMaterial;
            obj.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            obj.Renderer.receiveShadows = false;
            return obj;
        }

        private void Release(int cell)
        {
            PieceObj obj = live[cell];
            live.Remove(cell);
            obj.Go.SetActive(false);
            pool.Push(obj);
        }

        private void Place(int cell, PieceObj obj)
        {
            Vector3 pos = state.WorldOf(cell) + obj.MeshOffset;
            float animAlpha = 1f;
            if (anims.TryGetValue(cell, out Anim anim))
            {
                float u = Mathf.Clamp01(anim.T / anim.Duration);
                float e = u * u * (3f - 2f * u);
                pos = Vector3.Lerp(anim.From, anim.To, e) + obj.MeshOffset;
                if (anim.FadeIn) animAlpha = u;
            }
            obj.Go.transform.position = pos;

            float baseAlpha = state.InCurrentLayer(cell) ? 1f : 0f;
            float alpha = Mathf.Max(baseAlpha, Mathf.Sin(state.Phi * Mathf.Deg2Rad)) * animAlpha;
            if (state.IsolatedAway(cell)) alpha = 0f;

            float depth = cam != null ? Vector3.Dot(pos - cam.transform.position, cam.transform.forward) : 0f;
            float fade = Mathf.InverseLerp(DistanceFadeNear, DistanceFadeFar, depth);
            Color tint = Piece.ColorOf(obj.Piece) == Side.White ? WhiteTint : BlackTint;
            Color c = Color.Lerp(tint, new Color(0.12f, 0.12f, 0.15f), fade * 0.5f);
            c.a = alpha * Mathf.Lerp(1f, 0.45f, fade);

            bool visible = c.a > 0.005f;
            if (obj.Renderer.enabled != visible) obj.Renderer.enabled = visible;
            if (!visible) return;
            mpb.SetColor("_Color", c);
            obj.Renderer.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------ cells

        private void DrawCells()
        {
            int side = state.G.Side;
            int nEmpty = 0, nWhite = 0, nBlack = 0;
            bool onLattice = !state.Rotating;
            float ctr = state.Center;
            Board b = state.Board;
            for (int i = 0; i < side; i++)
                for (int j = 0; j < side; j++)
                    for (int k = 0; k < side; k++)
                    {
                        Vector3 pos = state.LatticeToWorld(i, j, k);
                        byte p = 0;
                        int cell = -1;
                        if (onLattice)
                        {
                            cell = state.CellAtLattice(i, j, k);
                            if (state.IsolatedAway(cell)) continue;
                            p = b.GetPiece(cell);
                        }
                        if (p == 0)
                        {
                            emptyInst[nEmpty++].objectToWorld = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.08f);
                        }
                        else
                        {
                            var m = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.92f);
                            if (Piece.ColorOf(p) == Side.White) whiteInst[nWhite++].objectToWorld = m; else blackInst[nBlack++].objectToWorld = m;
                        }
                    }
            Render(cellEmptyMat, emptyInst, nEmpty);
            Render(cellWhiteMat, whiteInst, nWhite);
            Render(cellBlackMat, blackInst, nBlack);

            if (onLattice)
            {
                int nT = 0, nC = 0;
                foreach (int cell in state.MoveTargets)
                {
                    if (!state.IsVisibleInVolume(cell) || state.IsolatedAway(cell)) continue;
                    var m = Matrix4x4.TRS(state.WorldOf(cell), Quaternion.identity, Vector3.one * 0.34f);
                    if (state.CaptureTargets.Contains(cell)) captureInst[nC++].objectToWorld = m; else targetInst[nT++].objectToWorld = m;
                }
                Render(targetMat, targetInst, nT);
                Render(captureMat, captureInst, nC);
                if (ShowThreats && IsThreatened != null)
                {
                    int nTh = 0;
                    for (int c = 0; c < 2; c++)
                    {
                        b.GetPieceCells((Side)c, cellsScratch);
                        foreach (int cell in cellsScratch)
                        {
                            if (!state.IsVisibleInVolume(cell) || state.IsolatedAway(cell) || !IsThreatened(cell)) continue;
                            threatInst[nTh++].objectToWorld = Matrix4x4.TRS(state.WorldOf(cell) + new Vector3(0.36f, 0.36f, -0.36f), Quaternion.identity, Vector3.one * 0.16f);
                        }
                    }
                    Render(threatMat, threatInst, nTh);
                }
                if (state.SelectedCell >= 0 && state.IsVisibleInVolume(state.SelectedCell))
                {
                    oneInst[0].objectToWorld = Matrix4x4.TRS(state.WorldOf(state.SelectedCell), Quaternion.identity, Vector3.one * 1.0f);
                    Render(cellSelectedMat, oneInst, 1);
                }
                if (state.HoverCell >= 0 && state.HoverCell != state.SelectedCell && state.IsVisibleInVolume(state.HoverCell))
                {
                    oneInst[0].objectToWorld = Matrix4x4.TRS(state.WorldOf(state.HoverCell), Quaternion.identity, Vector3.one * 1.0f);
                    Render(cellHoverMat, oneInst, 1);
                }
            }
        }

        private void Render(Material mat, InstanceData[] data, int count)
        {
            if (count == 0) return;
            var rp = new RenderParams(mat)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 64f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshInstanced(rp, cubeMesh, 0, data, count);
        }

        // ------------------------------------------------------------ picking

        /// <summary>Nearest lattice cell hit by <paramref name="ray"/>, or -1. Only valid when the view is on the lattice.</summary>
        public int Pick(Ray ray)
        {
            if (!state.PickingEnabled) return -1;
            int side = state.G.Side;
            float ctr = state.Center;
            float best = float.MaxValue;
            int bestCell = -1;
            Vector3 half = Vector3.one * (0.5f * ViewState.CellSize);
            for (int i = 0; i < side; i++)
                for (int j = 0; j < side; j++)
                    for (int k = 0; k < side; k++)
                    {
                        Vector3 centre = state.LatticeToWorld(i, j, k);
                        var bounds = new Bounds(centre, half * 2f);
                        if (!bounds.IntersectRay(ray, out float dist) || dist >= best) continue;
                        int cell = state.CellAtLattice(i, j, k);
                        if (state.IsolatedAway(cell)) continue;
                        // Prefer occupied cells: an empty marker should not shadow a piece behind it.
                        if (state.Board.GetPiece(cell) == 0 && bestCell >= 0 && state.Board.GetPiece(bestCell) != 0 && dist > best - 1.5f) continue;
                        best = dist;
                        bestCell = cell;
                    }
            return bestCell;
        }
    }
}
