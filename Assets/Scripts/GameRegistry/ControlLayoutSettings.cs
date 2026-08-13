using System;
using System.Collections.Generic;
using UnityEngine;

public static class ControlLayoutSettings
{
    public const float DefaultCameraSensitivity = 20f;
    public const float MinimumCameraSensitivity = 5f;
    public const float MaximumCameraSensitivity = 50f;

    private const string PreferenceKey = "settings.mobileControlLayout.v1";
    private const string SensitivityPreferenceKey = "settings.cameraSensitivity";
    private static float cachedCameraSensitivity = float.NaN;

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
        public List<ControlEntry> controls = new List<ControlEntry>();

        public ControlEntry Find(string id)
        {
            return controls.Find(entry => entry.id == id);
        }

        public LayoutData Copy()
        {
            var copy = new LayoutData { cameraSensitivity = cameraSensitivity };
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
            controls = new List<ControlEntry>
            {
                new ControlEntry("Move", 0.150f, 0.266f),
                new ControlEntry("Jump", 0.893f, 0.190f),
                new ControlEntry("A1", 0.906f, 0.491f),
                new ControlEntry("A2", 0.760f, 0.407f),
                new ControlEntry("A3", 0.724f, 0.167f),
                new ControlEntry("Brace", 0.885f, 0.345f)
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

        data.cameraSensitivity = Mathf.Clamp(
            data.cameraSensitivity,
            MinimumCameraSensitivity,
            MaximumCameraSensitivity);
        cachedCameraSensitivity = data.cameraSensitivity;
        PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(data));
        PlayerPrefs.SetFloat(SensitivityPreferenceKey, cachedCameraSensitivity);
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
        ApplyControl(canvas.transform, FindDirectChild(canvas.transform, "Brace"), layout.Find("Brace"), 150f);
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
        }
    }
}
