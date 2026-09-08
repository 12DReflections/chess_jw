using UnityEngine;

namespace Chess4D.Unity
{
    /// <summary>Camera orbit and zoom around the visible volume. Changes the viewing angle only, never which axes are in view.</summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Vector3 Centre = Vector3.zero;
        public float Yaw = 25f;
        public float Pitch = 24f;
        /// <summary>Framed for 10 cells across at 45 degrees vertical field of view so mid-rotation growth does not lurch (spec Stage 3).</summary>
        public float Distance = 16.5f;
        public float MinDistance = 5f;
        public float MaxDistance = 45f;

        /// <summary>Board orientation: a yaw offset that turns the board to face the side to move. Camera only; coordinates never change.</summary>
        public float OrientationYaw { get; private set; }
        public float TargetOrientationYaw { get; private set; }
        public const float FlipSeconds = 0.5f;
        private float flipFrom, flipT = 1f;
        public bool Flipping { get { return flipT < 1f; } }

        private void Start() { Apply(); }

        /// <summary>Sets the orientation target. A 180 degree change always turns the same way; smaller changes take the short arc.</summary>
        public void SetOrientation(float targetYaw, bool animate)
        {
            targetYaw = Mathf.Repeat(targetYaw, 360f);
            if (Mathf.Abs(Mathf.DeltaAngle(TargetOrientationYaw, targetYaw)) < 0.01f) return;
            TargetOrientationYaw = targetYaw;
            if (!animate) { OrientationYaw = targetYaw; flipT = 1f; return; }
            flipFrom = OrientationYaw;
            flipT = 0f;
        }

        private void Update()
        {
            if (flipT >= 1f) return;
            flipT = Mathf.Min(1f, flipT + Time.deltaTime / FlipSeconds);
            float delta = Mathf.DeltaAngle(flipFrom, TargetOrientationYaw);
            if (Mathf.Abs(Mathf.Abs(delta) - 180f) < 0.01f) delta = 180f; // a half turn always goes the same way
            float e = flipT * flipT * (3f - 2f * flipT);
            OrientationYaw = Mathf.Repeat(flipFrom + delta * e, 360f);
        }

        public void Drag(float dx, float dy)
        {
            Yaw += dx * 0.3f;
            Pitch = Mathf.Clamp(Pitch - dy * 0.3f, -85f, 85f);
        }

        public void Zoom(float delta) { Distance = Mathf.Clamp(Distance - delta * 1.5f, MinDistance, MaxDistance); }

        private void LateUpdate() { Apply(); }

        private void Apply()
        {
            transform.rotation = Quaternion.Euler(Pitch, Yaw + OrientationYaw, 0f);
            transform.position = Centre - transform.forward * Distance;
        }
    }
}
