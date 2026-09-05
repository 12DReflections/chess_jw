using System.IO;
using Chess4D.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// Builds the Chess4D scene from code so nothing is hand-edited in YAML, wires
/// the piece meshes and the fade materials into the bootstrap, strips the
/// missing-script components left by the Stage 3 deletion from Main.unity and
/// the piece prefabs, and can build a standalone player for the scripted demo.
/// Run from the menu or in batch mode via -executeMethod.
/// </summary>
public static class Chess4DSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Chess4D.unity";
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string MaterialDir = "Assets/Chess4D/Materials";
    private const string ModelDir = "Assets/3rdParty/LowpolyChessPack/Models/";
    private const string ChessMaterialPath = "Assets/3rdParty/LowpolyChessPack/Materials/Chess Material.mat";
    private static readonly string[] TypeNames = { null, "Pawn", "Knight", "Bishop", "Rook", "Queen", "King" };

    [MenuItem("Chess4D/Build Scene")]
    public static void BuildScene()
    {
        Directory.CreateDirectory(MaterialDir);
        var chessMat = AssetDatabase.LoadAssetAtPath<Material>(ChessMaterialPath);
        Material pieceFade = FadeMaterial(MaterialDir + "/PieceFade.mat", chessMat != null ? chessMat.mainTexture : null);
        Material cellFade = FadeMaterial(MaterialDir + "/CellFade.mat", null);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(OrbitCamera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
        cam.fieldOfView = 45f;

        var lightGo = new GameObject("Directional Light", typeof(Light));
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.None;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var gameGo = new GameObject("Chess4D", typeof(Chess4DGame));
        var so = new SerializedObject(gameGo.GetComponent<Chess4DGame>());
        var white = so.FindProperty("whiteMeshes");
        var black = so.FindProperty("blackMeshes");
        white.arraySize = black.arraySize = 7;
        for (int t = 1; t <= 6; t++)
        {
            white.GetArrayElementAtIndex(t).objectReferenceValue = LoadMesh("White " + TypeNames[t]);
            black.GetArrayElementAtIndex(t).objectReferenceValue = LoadMesh("Black " + TypeNames[t]);
        }
        so.FindProperty("pieceFadeMaterial").objectReferenceValue = pieceFade;
        so.FindProperty("cellFadeMaterial").objectReferenceValue = cellFade;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(MainScenePath, true) };

        CleanMissingScripts();
        AssetDatabase.SaveAssets();
        Debug.Log("Chess4D scene built at " + ScenePath);
    }

    private static Mesh LoadMesh(string name)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ModelDir + name + ".dae");
        if (mesh == null) Debug.LogError("Missing mesh " + name);
        return mesh;
    }

    /// <summary>Standard shader in Fade mode, saved as an asset so its keyword variant ships in the build.</summary>
    private static Material FadeMaterial(string path, Texture texture)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_Mode", 2f);
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetFloat("_Glossiness", 0.25f);
        m.mainTexture = texture;
        m.color = Color.white;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>The Stage 3 deletion left dangling script references on Main.unity and the piece prefabs. Remove them so the kept assets load cleanly.</summary>
    private static void CleanMissingScripts()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            int removed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        int total = 0;
        foreach (var rootGo in main.GetRootGameObjects())
            foreach (var t in rootGo.GetComponentsInChildren<Transform>(true))
                total += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (total > 0) EditorSceneManager.SaveScene(main);
        Debug.Log("Removed " + total + " missing-script components from Main.unity");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Chess4D/Build macOS Player")]
    public static void BuildPlayer()
    {
        Directory.CreateDirectory("Builds");
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.resizableWindow = true;
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Chess4D.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        Debug.Log("Player build: " + report.summary.result + ", " + report.summary.totalErrors + " errors, " + report.summary.totalSize + " bytes");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void BuildAll()
    {
        BuildScene();
        BuildPlayer();
    }
}
