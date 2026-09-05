using Chess4D.Core;
using UnityEngine;
using Color = UnityEngine.Color;
using UnityEngine.EventSystems;

namespace Chess4D.Unity
{
    /// <summary>
    /// Bootstrap and input. Owns the Core board and the view state, builds the
    /// renderer, camera rig and HUD in code, and routes mouse and keyboard input.
    /// No game rules live here.
    /// </summary>
    public sealed class Chess4DGame : MonoBehaviour
    {
        public static readonly int[][] Perspectives = { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };

        [SerializeField] private Mesh[] whiteMeshes = new Mesh[7];
        [SerializeField] private Mesh[] blackMeshes = new Mesh[7];
        [SerializeField] private Material pieceFadeMaterial;
        [SerializeField] private Material cellFadeMaterial;

        public Board Board { get; private set; }
        public ViewState State { get; private set; }
        public BoardView View { get; private set; }
        public OrbitCamera Orbit { get; private set; }
        public HudUi Hud { get; private set; }

        private bool dragging, scrubbing;
        private Vector3 dragStart, lastMouse;

        private void Awake()
        {
            Application.runInBackground = true;
            Board = new Board(4, 8);
            StartPosition.Setup(Board);
            State = new ViewState(Board);

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

            Debug.Log("Chess4D ready: " + Board.PieceCount(Chess4D.Core.Color.White) + " white and " + Board.PieceCount(Chess4D.Core.Color.Black) + " black pieces, " + State.Describe());
            DemoRunner.StartIfRequested(this);
        }

        private void Update()
        {
            State.Update(Time.deltaTime);
            HandleKeys();
            HandleMouse();
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) State.SweepTo(Perspectives[0]);
            if (Input.GetKeyDown(KeyCode.Alpha2)) State.SweepTo(Perspectives[1]);
            if (Input.GetKeyDown(KeyCode.Alpha3)) State.SweepTo(Perspectives[2]);
            if (Input.GetKeyDown(KeyCode.Alpha4)) State.SweepTo(Perspectives[3]);
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.PageDown)) State.Page(AxisView.VisibleSlots, -1);
            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.PageUp)) State.Page(AxisView.VisibleSlots, +1);
            if (Input.GetKeyDown(KeyCode.Escape)) State.SelectedCell = -1;
            if (Input.GetKeyDown(KeyCode.I)) CycleIsolateMode();
            if (Input.GetKeyDown(KeyCode.O)) CycleIsolateSlot();
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
                    State.SelectedCell = cell == State.SelectedCell ? -1 : cell;
                }
                scrubbing = false;
            }

            if (!overUi && Mathf.Abs(Input.mouseScrollDelta.y) > 0f) Orbit.Zoom(Input.mouseScrollDelta.y);

            State.HoverCell = (!dragging && !overUi && State.PickingEnabled) ? View.Pick(Camera.main.ScreenPointToRay(mouse)) : -1;
        }

        public void OnPerspectiveButton(int index)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift) State.Arm(Perspectives[index]); else State.SweepTo(Perspectives[index]);
        }

        public bool SelectTyped(string text)
        {
            if (!Coord.TryParse(text, Board.G.Dimensions, out Coord c)) return false;
            for (int a = 0; a < c.Dimensions; a++) if (c[a] >= Board.G.Side) return false;
            int cell = Board.G.CellOf(c);
            State.SelectedCell = cell;
            State.PageTo(cell);
            return true;
        }

        public void CycleIsolateMode()
        {
            State.IsolateMode = (IsolateMode)(((int)State.IsolateMode + 1) % 3);
            if (State.IsolateSlot < 0) State.IsolateSlot = 1;
        }

        public void CycleIsolateSlot() { State.IsolateSlot = (State.IsolateSlot + 1) % AxisView.VisibleSlots; }
    }
}
