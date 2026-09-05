using System;
using Chess4D.Core;
using UnityEngine;

namespace Chess4D.Unity
{
    public enum IsolateMode { Off, HideBelow, HideAbove }

    /// <summary>
    /// Everything about how the board is being looked at: the axis view, the
    /// per-axis page for hidden axes, the armed/scrubbed/swept rotation, the
    /// selection, and layer isolation. None of this is a move (spec Stage 4).
    /// </summary>
    public sealed class ViewState
    {
        public const float SnapDegrees = 45f;
        public const float SweepSeconds = 0.6f;
        public const float CellSize = 1f;

        public readonly Board Board;
        public BoardGeometry G { get { return Board.G; } }
        public AxisView View;
        public readonly int[] Pages;

        public int SelectedCell = -1;
        public int HoverCell = -1;

        public bool Armed { get; private set; }
        public int RotatingSlot { get; private set; } = -1;
        public int HiddenSlot { get; private set; } = -1;
        public float Phi { get; private set; }
        public bool Sweeping { get; private set; }
        private float sweepFrom, sweepTo, sweepT;
        public AxisView Target { get; private set; }

        public int IsolateSlot = -1;
        public IsolateMode IsolateMode = IsolateMode.Off;

        public event Action ViewChanged;

        private readonly double[] slotValues;
        private readonly int[] coordScratch;

        public ViewState(Board board)
        {
            Board = board;
            View = new AxisView(board.G.Dimensions, board.G.Side);
            Pages = new int[board.G.Dimensions];
            for (int a = 0; a < Pages.Length; a++) Pages[a] = StartPosition.CenterCoordinate(board.G);
            Pages[0] = board.G.Side / 2; // the king file
            Pages[BoardGeometry.AdvanceAxis] = 0;
            slotValues = new double[board.G.Dimensions];
            coordScratch = new int[board.G.Dimensions];
        }

        public float Center { get { return (float)View.Center; } }

        /// <summary>Picking is only meaningful on the lattice.</summary>
        public bool PickingEnabled { get { return !Armed || Phi <= 0f; } }
        public bool Rotating { get { return Armed && Phi > 0f; } }

        // ------------------------------------------------------------ rotation

        /// <summary>Arms a rotation toward the perspective that shows <paramref name="targetAxes"/>. Returns false if already there or a rotation is in progress.</summary>
        public bool Arm(int[] targetAxes)
        {
            if (Armed || View.ShowsAxes(targetAxes)) return false;
            int outgoing = -1, incoming = -1;
            for (int s = 0; s < AxisView.VisibleSlots; s++)
            {
                int axis = View.AxisAtSlot(s);
                if (Array.IndexOf(targetAxes, axis) < 0) { outgoing = axis; break; }
            }
            for (int s = AxisView.VisibleSlots; s < View.Dimensions; s++)
            {
                int axis = View.AxisAtSlot(s);
                if (Array.IndexOf(targetAxes, axis) >= 0) { incoming = axis; break; }
            }
            if (outgoing < 0 || incoming < 0) return false;
            RotatingSlot = View.SlotOfAxis(outgoing);
            HiddenSlot = View.SlotOfAxis(incoming);
            Target = View.AfterQuarterTurn(RotatingSlot, HiddenSlot);
            Armed = true;
            Phi = 0f;
            Sweeping = false;
            return true;
        }

        /// <summary>Button or keyboard: arm and run the timed sweep.</summary>
        public bool SweepTo(int[] targetAxes)
        {
            if (!Arm(targetAxes)) return false;
            StartSweep(90f);
            return true;
        }

        /// <summary>Scrub: set phi directly while armed and not sweeping.</summary>
        public void Scrub(float phiDegrees)
        {
            if (!Armed || Sweeping) return;
            Phi = Mathf.Clamp(phiDegrees, 0f, 90f);
            if (Phi >= 90f) Commit();
        }

        /// <summary>Release after a scrub: past 45 snaps forward, below springs back.</summary>
        public void Release()
        {
            if (!Armed || Sweeping) return;
            StartSweep(Phi >= SnapDegrees ? 90f : 0f);
        }

        private void StartSweep(float to)
        {
            sweepFrom = Phi;
            sweepTo = to;
            sweepT = 0f;
            Sweeping = true;
        }

        public void Update(float dt)
        {
            if (!Sweeping) return;
            float span = Mathf.Abs(sweepTo - sweepFrom) / 90f;
            float duration = Mathf.Max(0.05f, SweepSeconds * span);
            sweepT += dt / duration;
            if (sweepT >= 1f)
            {
                Sweeping = false;
                if (sweepTo >= 90f) Commit(); else Disarm();
                return;
            }
            float e = sweepT * sweepT * (3f - 2f * sweepT); // smoothstep
            Phi = Mathf.Lerp(sweepFrom, sweepTo, e);
        }

        private void Commit()
        {
            View = Target;
            Disarm();
        }

        private void Disarm()
        {
            Armed = false;
            Sweeping = false;
            Phi = 0f;
            RotatingSlot = HiddenSlot = -1;
            Target = null;
            ViewChanged?.Invoke();
        }

        // ------------------------------------------------------------ paging

        public int PageOfSlot(int hiddenSlot) { return Pages[View.AxisAtSlot(hiddenSlot)]; }

        public void SetPage(int hiddenSlot, int value)
        {
            int axis = View.AxisAtSlot(hiddenSlot);
            Pages[axis] = Mathf.Clamp(value, 0, G.Side - 1);
            ViewChanged?.Invoke();
        }

        public void Page(int hiddenSlot, int delta) { SetPage(hiddenSlot, PageOfSlot(hiddenSlot) + delta); }

        /// <summary>Pages every hidden axis so that <paramref name="cell"/> lies in the visible volume.</summary>
        public void PageTo(int cell)
        {
            for (int s = AxisView.VisibleSlots; s < View.Dimensions; s++)
            {
                int axis = View.AxisAtSlot(s);
                Pages[axis] = G.Coord(cell, axis);
            }
            ViewChanged?.Invoke();
        }

        // ------------------------------------------------------------ projection

        /// <summary>The view whose layers matter right now: the far endpoint once phi passes 45.</summary>
        public AxisView NearerView { get { return Armed && Phi >= SnapDegrees ? Target : View; } }

        public bool InCurrentLayer(int cell)
        {
            AxisView v = NearerView;
            for (int s = AxisView.VisibleSlots; s < v.Dimensions; s++)
            {
                int axis = v.AxisAtSlot(s);
                if (G.Coord(cell, axis) != Pages[axis]) return false;
            }
            return true;
        }

        /// <summary>World position of a cell under the current (possibly mid-rotation) view.</summary>
        public Vector3 WorldOf(int cell)
        {
            Coord c = G.CoordOf(cell);
            if (Armed) View.Project(c, RotatingSlot, HiddenSlot, Phi, slotValues);
            else View.Project(c, 0, AxisView.VisibleSlots, 0.0, slotValues);
            return SlotsToWorld(slotValues[0], slotValues[1], slotValues[2]);
        }

        public Vector3 SlotsToWorld(double s0, double s1, double s2)
        {
            double ctr = View.Center;
            return new Vector3((float)(s0 - ctr), (float)(s1 - ctr), (float)(s2 - ctr)) * CellSize;
        }

        /// <summary>The board cell shown at visible lattice position (i, j, k) with the hidden axes at their pages. Only meaningful when not rotating.</summary>
        public int CellAtLattice(int i, int j, int k)
        {
            int side = G.Side;
            for (int s = 0; s < View.Dimensions; s++)
            {
                int axis = View.AxisAtSlot(s);
                if (s < AxisView.VisibleSlots)
                {
                    int v = s == 0 ? i : s == 1 ? j : k;
                    coordScratch[axis] = View.SignAtSlot(s) > 0 ? v : side - 1 - v;
                }
                else coordScratch[axis] = Pages[axis];
            }
            return G.CellOf(coordScratch);
        }

        /// <summary>Exact lattice value of <paramref name="cell"/> in a visible slot under the non-rotating view.</summary>
        public int LatticeValue(int cell, int slot) { return View.ProjectExact(G.CoordOf(cell), slot); }

        public bool IsolatedAway(int cell)
        {
            if (IsolateMode == IsolateMode.Off || IsolateSlot < 0 || SelectedCell < 0 || Rotating) return false;
            int v = LatticeValue(cell, IsolateSlot);
            int sel = LatticeValue(SelectedCell, IsolateSlot);
            return IsolateMode == IsolateMode.HideBelow ? v < sel : v > sel;
        }

        public bool IsVisibleInVolume(int cell)
        {
            AxisView v = View;
            for (int s = AxisView.VisibleSlots; s < v.Dimensions; s++)
            {
                int axis = v.AxisAtSlot(s);
                if (G.Coord(cell, axis) != Pages[axis]) return false;
            }
            return true;
        }

        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("view ").Append(View.ToString());
            for (int s = AxisView.VisibleSlots; s < View.Dimensions; s++)
                sb.Append("   ").Append(AxisView.AxisNames[View.AxisAtSlot(s)]).Append('=').Append(PageOfSlot(s));
            if (Armed) sb.Append("   rotating ").Append(Phi.ToString("F1")).Append("° toward ").Append(Target.ToString());
            return sb.ToString();
        }
    }
}
