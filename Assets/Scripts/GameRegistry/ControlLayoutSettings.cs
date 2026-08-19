using System;
using System.Collections.Generic;
using UnityEngine;

public static class ControlLayoutSettings
{
    public const string CrosshairControlId = "Crosshair";
    public const float CrosshairBaseSize = 42f;
    public const float DefaultCameraSensitivity = 20f;
    public const float MinimumCameraSensitivity = 5f;
    public const float MaximumCameraSensitivity = 50f;
    public const float DefaultCameraFieldOfView = 80f;
    public const float MinimumCameraFieldOfView = 60f;
    public const float MaximumCameraFieldOfView = 110f;

    private const string PreferenceKey = "settings.mobileControlLayout.v1";
    private const string SensitivityPreferenceKey = "settings.cameraSensitivity";
    private const string FieldOfViewPreferenceKey = "settings.cameraFieldOfView";
    private const string CameraDepthTuningVersionKey = "settings.cameraDepthTuning.v1";
    private static float cachedCameraSensitivity = float.NaN;
    private static float cachedCameraFieldOfView = float.NaN;

    public static bool HasSavedLayout => PlayerPrefs.HasKey(PreferenceKey);

    [Serializable]
    public class ControlEntry
    {
        public string id;
        public float x;
        public float y;
        public float scale;

        public ControlEntry(string id, float x, float y, float scale = 1f)
        {
            this.id = id;
            this.x = x;
            this.y = y;
            this.scale = scale;
        }

        public ControlEntry Copy()
        {
            return new ControlEntry(id, x, y, scale);
        }
    }

    [Serializable]
    public class LayoutData
    {
        public float cameraSensitivity = DefaultCameraSensitivity;
        public float cameraFieldOfView = DefaultCameraFieldOfView;
        public List<ControlEntry> controls = new List<ControlEntry>();

        public ControlEntry Find(string id)
        {
            return controls.Find(entry => entry.id == id);
        }

        public LayoutData Copy()
        {
            var copy = new LayoutData
            {
                cameraSensitivity = cameraSensitivity,
                cameraFieldOfView = cameraFieldOfView
            };
            foreach (ControlEntry entry in controls)
                copy.controls.Add(entry.Copy());
            return copy;
        }
    }

    public static LayoutData CreateDefault()
    {
        return new LayoutData
        {
            cameraSensitivity = DefaultCameraSensitivity,
            cameraFieldOfView = DefaultCameraFieldOfView,
            controls = new List<ControlEntry>
            {
                new ControlEntry("Move", 0.150f, 0.266f),
                new ControlEntry("Jump", 0.893f, 0.190f),
                new ControlEntry("A1", 0.906f, 0.491f),
                new ControlEntry("A2", 0.760f, 0.407f),
                new ControlEntry("A3", 0.724f, 0.167f),
                new ControlEntry(CrosshairControlId, 0.5f, 0.5f)
            }
        };
    }

    public static LayoutData Load()
    {
        if (!PlayerPrefs.HasKey(PreferenceKey))
            return CreateDefault();

        try
        {
            LayoutData data = JsonUtility.FromJson<LayoutData>(PlayerPrefs.GetString(PreferenceKey));
            if (data == null || data.controls == null)
                return CreateDefault();

            MergeMissingDefaults(data);
            data.cameraSensitivity = Mathf.Clamp(
                data.cameraSensitivity,
                MinimumCameraSensitivity,
                MaximumCameraSensitivity);
            data.cameraFieldOfView = NormalizeCameraFieldOfView(data.cameraFieldOfView);
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ControlLayout] Could not load saved controls: {exception.Message}");
            return CreateDefault();
        }
    }

    public static void Save(LayoutData data)
    {
        if (data == null)
            return;

        MergeMissingDefaults(data);
        data.cameraSensitivity = Mathf.Clamp(
            data.cameraSensitivity,
            MinimumCameraSensitivity,
            MaximumCameraSensitivity);
        data.cameraFieldOfView = NormalizeCameraFieldOfView(data.cameraFieldOfView);
        cachedCameraSensitivity = data.cameraSensitivity;
        cachedCameraFieldOfView = data.cameraFieldOfView;
        PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(data));
        PlayerPrefs.SetFloat(SensitivityPreferenceKey, cachedCameraSensitivity);
        PlayerPrefs.SetFloat(FieldOfViewPreferenceKey, cachedCameraFieldOfView);
        PlayerPrefs.Save();
    }

    public static float LoadCameraSensitivity()
    {
        if (!float.IsNaN(cachedCameraSensitivity))
            return cachedCameraSensitivity;

        float savedValue = PlayerPrefs.HasKey(SensitivityPreferenceKey)
            ? PlayerPrefs.GetFloat(SensitivityPreferenceKey)
            : Load().cameraSensitivity;
        cachedCameraSensitivity = Mathf.Clamp(
            savedValue,
            MinimumCameraSensitivity,
            MaximumCameraSensitivity);
        return cachedCameraSensitivity;
    }

    public static float LoadCameraFieldOfView()
    {
        // The original 85-degree default made close hazards look deceptively
        // distant. Migrate only people who were still on that old default;
        // players who deliberately chose another FOV keep their preference.
        if (!PlayerPrefs.HasKey(CameraDepthTuningVersionKey))
        {
            LayoutData layout = Load();
            if (Mathf.Approximately(layout.cameraFieldOfView, 85f))
            {
                layout.cameraFieldOfView = DefaultCameraFieldOfView;
                Save(layout);
            }
            PlayerPrefs.SetInt(CameraDepthTuningVersionKey, 1);
            PlayerPrefs.Save();
        }

        if (!float.IsNaN(cachedCameraFieldOfView))
            return cachedCameraFieldOfView;

        float savedValue = PlayerPrefs.HasKey(FieldOfViewPreferenceKey)
            ? PlayerPrefs.GetFloat(FieldOfViewPreferenceKey)
            : Load().cameraFieldOfView;
        cachedCameraFieldOfView = NormalizeCameraFieldOfView(savedValue);
        return cachedCameraFieldOfView;
    }

    public static float NormalizeCameraFieldOfView(float value)
    {
        // Layout JSON saved before FOV existed deserializes the new float as
        // zero. Treat that as an old save and migrate it to the intended default.
        if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            return DefaultCameraFieldOfView;

        return Mathf.Clamp(value, MinimumCameraFieldOfView, MaximumCameraFieldOfView);
    }

    public static void ApplyToGameCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        LayoutData layout = Load();
        Transform buttonRoot = canvas.transform.Find("ButtonBR");

        ApplyControl(canvas.transform, FindDirectChild(canvas.transform, "Image"), layout.Find("Move"), 175f);
        ApplyControl(canvas.transform, buttonRoot != null ? buttonRoot.Find("Button") : null, layout.Find("Jump"), 250f);
        ApplyControl(canvas.transform, buttonRoot != null ? buttonRoot.Find("A1") : null, layout.Find("A1"), 150f);
        ApplyControl(canvas.transform, buttonRoot != null ? buttonRoot.Find("A2") : null, layout.Find("A2"), 150f);
        ApplyControl(canvas.transform, buttonRoot != null ? buttonRoot.Find("A3") : null, layout.Find("A3"), 150f);
        ApplyCrosshair(canvas, layout.Find(CrosshairControlId));
    }

    private static void ApplyCrosshair(Canvas canvas, ControlEntry entry)
    {
        Transform existing = canvas.transform.Find("Aim Crosshair");
        RectTransform root;
        if (existing != null)
        {
            root = existing as RectTransform;
        }
        else
        {
            GameObject crosshair = new GameObject("Aim Crosshair", typeof(RectTransform));
            crosshair.layer = 5;
            crosshair.transform.SetParent(canvas.transform, false);
            root = (RectTransform)crosshair.transform;
            CreateCrosshairBar(root, "Horizontal", true);
            CreateCrosshairBar(root, "Vertical", false);
        }

        if (root == null)
            return;

        float scale = Mathf.Clamp(entry != null ? entry.scale : 1f, 0.55f, 1.8f);
        float size = CrosshairBaseSize * scale;
        root.anchorMin = Vector2.one * 0.5f;
        root.anchorMax = Vector2.one * 0.5f;
        root.pivot = Vector2.one * 0.5f;
        root.anchoredPosition = Vector2.zero;
        root.localScale = Vector3.one;
        root.sizeDelta = Vector2.one * size;
        SetCrosshairBar(root.Find("Horizontal") as RectTransform, size * 0.72f, size * 0.12f);
        SetCrosshairBar(root.Find("Vertical") as RectTransform, size * 0.12f, size * 0.72f);
        root.SetAsLastSibling();
    }

    private static void CreateCrosshairBar(Transform parent, string name, bool horizontal)
    {
        GameObject bar = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        bar.layer = 5;
        bar.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = bar.GetComponent<UnityEngine.UI.Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        SetCrosshairBar((RectTransform)bar.transform,
            horizontal ? CrosshairBaseSize * 0.72f : CrosshairBaseSize * 0.12f,
            horizontal ? CrosshairBaseSize * 0.12f : CrosshairBaseSize * 0.72f);
    }

    private static void SetCrosshairBar(RectTransform bar, float width, float height)
    {
        if (bar == null)
            return;
        bar.anchorMin = Vector2.one * 0.5f;
        bar.anchorMax = Vector2.one * 0.5f;
        bar.pivot = Vector2.one * 0.5f;
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(width, height);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static void ApplyControl(Transform canvasTransform, Transform control, ControlEntry entry, float baseSize)
    {
        if (control == null || entry == null || !(control is RectTransform rect))
            return;

        // A normalized anchor makes the saved layout independent of resolution,
        // aspect ratio and the CanvasScaler's current scale factor.
        rect.SetParent(canvasTransform, false);
        Vector2 anchor = new Vector2(Mathf.Clamp01(entry.x), Mathf.Clamp01(entry.y));
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        float size = baseSize * Mathf.Clamp(entry.scale, 0.55f, 1.8f);
        rect.sizeDelta = new Vector2(size, size);
        rect.SetAsLastSibling();
    }

    private static void MergeMissingDefaults(LayoutData data)
    {
        LayoutData defaults = CreateDefault();
        foreach (ControlEntry defaultEntry in defaults.controls)
        {
            ControlEntry entry = data.Find(defaultEntry.id);
            if (entry == null)
            {
                data.controls.Add(defaultEntry.Copy());
                continue;
            }

            entry.x = Mathf.Clamp01(entry.x);
            entry.y = Mathf.Clamp01(entry.y);
            entry.scale = Mathf.Clamp(entry.scale, 0.55f, 1.8f);
            if (entry.id == CrosshairControlId)
            {
                entry.x = 0.5f;
                entry.y = 0.5f;
            }
        }
    }
}
