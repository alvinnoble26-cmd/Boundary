#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public static class EntropyZeroMenuVerification
{
    private const string ScenePath = "Assets/Game/Scenes/Menu.unity";
    private const string AppPath = "/tmp/EntropyZeroMenuVerification.app";
    private static readonly string[] Panels =
    {
        "main", "start", "multiplayer", "join", "host", "lost", "won", "options",
        "abilities", "ability-info", "controls", "skins", "practice", "other"
    };

    [MenuItem("Entropy Zero/UI/Run Full Verification")]
    public static void RunFromMenu() => Run();

    public static void Run()
    {
        string root = NextIterationFolder();
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "command.txt"),
            "Unity -batchmode -nographics -projectPath <project> -executeMethod EntropyZeroMenuVerification.Run -logFile /tmp/ez-ui-verify.log\n");

        bool reusePlayer = Environment.GetEnvironmentVariable("EZ_UI_REUSE_PLAYER") == "1" && Directory.Exists(AppPath);
        if (!reusePlayer)
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = AppPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };
            BuildReport build = BuildPipeline.BuildPlayer(options);
            if (build.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Verification player build failed: " + build.summary.result);
        }

        string executable = Directory.GetFiles(Path.Combine(AppPath, "Contents/MacOS"))
            .First(path => (File.GetAttributes(path) & FileAttributes.Directory) == 0);
        var resolutions = new[]
        {
            (1920, 1080, false), (2532, 1170, true), (2048, 1536, false), (1400, 1750, false)
        };
        foreach ((int width, int height, bool safe) in resolutions)
        {
            string resolutionFolder = Path.Combine(root, $"{width}x{height}");
            Directory.CreateDirectory(resolutionFolder);
            foreach (string panel in Panels)
            {
                string output = Path.Combine(resolutionFolder, panel + ".png");
                RunPlayer(executable, panel, output, width, height, safe, 1.5f);
            }
            if (width == 1920 && height == 1080)
            {
                RunPlayer(executable, "main", Path.Combine(resolutionFolder, "main-t0.3.png"), width, height, safe, 0.3f);
                RunPlayer(executable, "main", Path.Combine(resolutionFolder, "main-t4.0.png"), width, height, safe, 4f);
            }
            WriteContactSheet(resolutionFolder, width, height);
        }
        WriteAudit(root);
        Debug.Log("[EZ UI Verify] Complete: " + root);
        EditorApplication.Exit(0);
    }

    private static void WriteContactSheet(string folder, int sourceWidth, int sourceHeight)
    {
        const int columns = 4;
        const int cellWidth = 480;
        int cellHeight = Mathf.RoundToInt(cellWidth * sourceHeight / (float)sourceWidth);
        int rows = Mathf.CeilToInt(Panels.Length / (float)columns);
        Texture2D sheet = new Texture2D(columns * cellWidth, rows * cellHeight, TextureFormat.RGB24, false);
        Color32[] clear = Enumerable.Repeat(new Color32(5, 7, 15, 255), sheet.width * sheet.height).ToArray();
        sheet.SetPixels32(clear);

        for (int index = 0; index < Panels.Length; index++)
        {
            string path = Path.Combine(folder, Panels[index] + ".png");
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGB24, false);
            source.LoadImage(File.ReadAllBytes(path));
            Texture2D scaled = new Texture2D(cellWidth, cellHeight, TextureFormat.RGB24, false);
            for (int y = 0; y < cellHeight; y++)
            for (int x = 0; x < cellWidth; x++)
                scaled.SetPixel(x, y, source.GetPixelBilinear(x / (float)(cellWidth - 1), y / (float)(cellHeight - 1)));
            scaled.Apply();

            int column = index % columns;
            int row = rows - 1 - index / columns;
            sheet.SetPixels(column * cellWidth, row * cellHeight, cellWidth, cellHeight, scaled.GetPixels());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(scaled);
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(folder, "contact-sheet.png"), sheet.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(sheet);
    }

    private static void RunPlayer(string executable, string panel, string output, int width, int height, bool safe, float delay)
    {
        string log = Path.ChangeExtension(output, ".log");
        string safeArgument = safe ? " --safe-area" : string.Empty;
        string arguments = $"-n -W -a \"{AppPath}\" --args --ez-ui-capture --panel {panel} --output \"{output}\" --delay {delay.ToString(System.Globalization.CultureInfo.InvariantCulture)}{safeArgument} -screen-width {width} -screen-height {height} -screen-fullscreen 0 -logFile \"{log}\"";
        using Process process = Process.Start(new ProcessStartInfo("/usr/bin/open", arguments) { UseShellExecute = false });
        process.WaitForExit(30000);
        System.Threading.Thread.Sleep(350);
        if (!File.Exists(output))
            throw new IOException("Capture was not produced: " + output);
    }

    private static void WriteAudit(string root)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<Canvas>(true))
            .First(value => value.name == "Canvas");
        List<string> failures = new List<string>();
        List<string> warnings = new List<string>();
        int undersizedButtons = 0;
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            RectTransform rect = button.transform as RectTransform;
            if (rect != null && rect.rect.height > 0f && rect.rect.height < 88f && button.gameObject.activeSelf)
            {
                undersizedButtons++;
                failures.Add("Touch target under 88px: " + HierarchyPath(button.transform));
            }
        }
        int activeLegacyText = canvas.GetComponentsInChildren<Text>(false).Length;
        if (activeLegacyText > 0) failures.Add("Active legacy Text components: " + activeLegacyText);
        int overflow = canvas.GetComponentsInChildren<TMP_Text>(true).Count(text => text.isTextOverflowing);
        if (overflow > 0) failures.Add("TMP overflow components: " + overflow);
        MenuLobbyUI lobby = canvas.GetComponent<MenuLobbyUI>();
        SerializedObject serializedLobby = lobby != null ? new SerializedObject(lobby) : null;
        bool lobbyWired = serializedLobby != null &&
            serializedLobby.FindProperty("mainMenuPanel").objectReferenceValue != null &&
            serializedLobby.FindProperty("hostLobbyPanel").objectReferenceValue != null &&
            serializedLobby.FindProperty("hostCodeText").objectReferenceValue != null;
        if (!lobbyWired) failures.Add("MenuLobbyUI serialized fields are not fully wired.");

        string text = $"Entropy Zero Menu UI Audit\nScene: {ScenePath}\nPanels captured: {Panels.Length}\nActive legacy Text: {activeLegacyText}\nTMP overflow: {overflow}\nUndersized active buttons: {undersizedButtons}\nMenuLobbyUI wired: {lobbyWired}\nFailures: {failures.Count}\n" +
                      string.Join("\n", failures.Select(value => "- " + value)) + "\n";
        File.WriteAllText(Path.Combine(root, "audit.txt"), text);
        string json = JsonUtility.ToJson(new AuditData
        {
            scene = ScenePath,
            panelsCaptured = Panels.Length,
            activeLegacyText = activeLegacyText,
            tmpOverflow = overflow,
            undersizedButtons = undersizedButtons,
            menuLobbyUiWired = lobbyWired,
            failures = failures.ToArray(),
            warnings = warnings.ToArray()
        }, true);
        File.WriteAllText(Path.Combine(root, "audit.json"), json);
    }

    private static string NextIterationFolder()
    {
        const string basePath = "/tmp/ez-ui";
        Directory.CreateDirectory(basePath);
        int iteration = 1;
        while (Directory.Exists(Path.Combine(basePath, "iter-" + iteration))) iteration++;
        return Path.Combine(basePath, "iter-" + iteration);
    }

    private static string HierarchyPath(Transform transform)
    {
        StringBuilder result = new StringBuilder(transform.name);
        while (transform.parent != null)
        {
            transform = transform.parent;
            result.Insert(0, transform.name + "/");
        }
        return result.ToString();
    }

    [Serializable]
    private sealed class AuditData
    {
        public string scene;
        public int panelsCaptured;
        public int activeLegacyText;
        public int tmpOverflow;
        public int undersizedButtons;
        public bool menuLobbyUiWired;
        public string[] failures;
        public string[] warnings;
    }
}
#endif
