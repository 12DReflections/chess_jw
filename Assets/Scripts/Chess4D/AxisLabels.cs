using Chess4D.Core;
using UnityEngine;

namespace Chess4D.Unity
{
    /// <summary>
    /// Three floating letters at the positive ends of the visible axes: across, depth and up.
    /// They name the board axis each screen direction currently shows, with its sign, and
    /// during a turn the rotating one reads "z → w" so a completed turn is visible even when
    /// the position looks the same from both sides (the opening is symmetric in z and w).
    /// Camera-facing, view only, never picked.
    /// </summary>
    public sealed class AxisLabels : MonoBehaviour
    {
        private ViewState state;
        private readonly TextMesh[] labels = new TextMesh[AxisView.VisibleSlots];
        private Camera cam;
        private const float Margin = 0.8f;

        public void Init(ViewState s)
        {
            state = s;
            cam = Camera.main;
            for (int slot = 0; slot < labels.Length; slot++)
            {
                var go = new GameObject("axis label " + slot, typeof(MeshRenderer), typeof(TextMesh));
                go.transform.SetParent(transform, false);
                var tm = go.GetComponent<TextMesh>();
                tm.font = UiKit.Font;
                tm.fontSize = 64;
                tm.characterSize = 0.085f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.richText = true;
                go.GetComponent<MeshRenderer>().material = UiKit.Font.material;
                labels[slot] = tm;
            }
        }

        private void LateUpdate()
        {
            if (state == null) return;
            if (cam == null) cam = Camera.main;
            float side = state.G.Side;
            for (int slot = 0; slot < labels.Length; slot++)
            {
                double s0 = -Margin, s1 = -Margin, s2 = -Margin;
                if (slot == 0) s0 = side - 1 + Margin + 0.6; else if (slot == 1) s1 = side - 1 + Margin + 0.6; else s2 = side - 1 + Margin + 0.6;
                var t = labels[slot].transform;
                t.position = state.SlotsToWorld(s0, s1, s2);
                if (cam != null) t.rotation = cam.transform.rotation;

                int axis = state.View.AxisAtSlot(slot);
                string text = UiKit.AxisRich(axis, state.View.SignAtSlot(slot) < 0);
                if (state.Armed && slot == state.RotatingSlot)
                    text += " → " + UiKit.AxisRich(state.View.AxisAtSlot(state.HiddenSlot), false);
                labels[slot].text = text;
            }
        }
    }
}
