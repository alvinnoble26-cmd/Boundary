#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuUIVerificationHarness
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (!args.Contains("--ez-ui-capture")) return;
        GameObject host = new GameObject("EZ UI Verification Harness");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Runner>().Configure(args);
    }

    private sealed class Runner : MonoBehaviour
    {
        private string panelId = "main";
        private string outputPath;
        private bool simulateSafeArea;
        private float delay = 1.5f;

        public void Configure(string[] args)
        {
            panelId = ReadArg(args, "--panel", panelId);
            outputPath = ReadArg(args, "--output", Path.Combine(Path.GetTempPath(), "ez-ui.png"));
            simulateSafeArea = args.Contains("--safe-area");
            float.TryParse(ReadArg(args, "--delay", "1.5"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out delay);
            delay = Mathf.Max(0.1f, delay);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            Canvas canvas = null;
            while (canvas == null)
            {
                canvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.name == "Canvas");
                yield return null;
            }

            SafeAreaFitter.SimulatedSafeArea = simulateSafeArea
                ? new Rect(132f, 63f, Screen.width - 264f, Screen.height - 126f)
                : new Rect(0f, 0f, Screen.width, Screen.height);
            ShowPanel(canvas, panelId);
            yield return new WaitForSecondsRealtime(delay);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            ScreenCapture.CaptureScreenshot(outputPath);
            yield return new WaitForSecondsRealtime(0.75f);
            Application.Quit();
        }

        private static void ShowPanel(Canvas canvas, string id)
        {
            string[] roots = { "MainMenu", "StartMenu", "MuiltiplayerMenu", "JoinLobbyPanel", "HostLobbyPanel", "Lost", "Won", "OptionsMenu", "AbilitiesMenu", "Ability Information Panel", "ControlLayoutEditor" };
            foreach (string root in roots)
            {
                Transform candidate = canvas.transform.Find(root);
                if (candidate != null) candidate.gameObject.SetActive(false);
            }

            string target = id switch
            {
                "main" => "MainMenu",
                "start" => "StartMenu",
                "multiplayer" => "MuiltiplayerMenu",
                "join" => "JoinLobbyPanel",
                "host" => "HostLobbyPanel",
                "lost" => "Lost",
                "won" => "Won",
                "options" => "OptionsMenu",
                "abilities" => "AbilitiesMenu",
                "ability-info" => "Ability Information Panel",
                "controls" => "ControlLayoutEditor",
                _ => null
            };
            if (target != null)
            {
                Transform panel = canvas.transform.Find(target);
                if (panel != null) panel.gameObject.SetActive(true);
                if (id == "host") MenuLobbyUI.I?.ShowHostLobby("4071");
            }
            else if (id == "skins")
            {
                Transform skin = canvas.transform.Find("MainMenu/SkinButton");
                skin?.GetComponent<Button>()?.onClick.Invoke();
            }
            else if (id == "practice")
            {
                Transform multiplayer = canvas.transform.Find("MuiltiplayerMenu");
                if (multiplayer != null) multiplayer.gameObject.SetActive(true);
                Button practice = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.name == "PracticeButton");
                practice?.onClick.Invoke();
            }
            else if (id == "other")
            {
                Transform options = canvas.transform.Find("OptionsMenu");
                if (options != null) options.gameObject.SetActive(true);
                Button button = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.name == "OtherInformationButton");
                button?.onClick.Invoke();
            }
        }

        private static string ReadArg(string[] args, string key, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return fallback;
        }
    }
}
#endif
