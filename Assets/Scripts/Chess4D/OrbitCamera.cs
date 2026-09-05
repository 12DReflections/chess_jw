using UnityEngine;

namespace Chess4D.Unity
{
    /// <summary>Camera orbit and zoom around the visible volume. Changes the viewing angle only, never which axes are in view.</summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Vector3 Centre = Vector3.zero;
        public float Yaw = 35f;
        public float Pitch = 24f;
        /// <summary>Framed for 10 cells across at 45 degrees vertical field of view so mid-rotation growth does not lurch (spec Stage 3).</summary>
        public float Distance = 16.5f;
        public float MinDistance = 5f;
        public float MaxDistance = 45f;

        private void Start() { Apply(); }

        public void Drag(float dx, float dy)
        {
            Yaw += dx * 0.3f;
            Pitch = Mathf.Clamp(Pitch - dy * 0.3f, -85f, 85f);
        }

        public void Zoom(float delta) { Distance = Mathf.Clamp(Distance - delta * 1.5f, MinDistance, MaxDistance); }

        private void LateUpdate() { Apply(); }

        private void Apply()
        {
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = Centre - transform.forward * Distance;
        }
    }
}
