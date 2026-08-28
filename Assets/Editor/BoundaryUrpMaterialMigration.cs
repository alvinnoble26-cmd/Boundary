using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Migrates imported built-in render pipeline materials under Assets/Items to
/// URP equivalents. It runs once in the current Editor session after scripts
/// reload, and remains available from the Tools menu for future imports.
/// </summary>
[InitializeOnLoad]
public static class BoundaryUrpMaterialMigration
{
    private const string SessionKey = "BoundaryUrpMaterialMigration.Completed";
    private const string ImportedAssetsRoot = "Assets/Items";

    static BoundaryUrpMaterialMigration()
    {
        EditorApplication.delayCall += MigrateOncePerEditorSession;
    }

    [MenuItem("Tools/Boundary/Migrate Imported Materials to URP")]
    public static void MigrateImportedMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (litShader == null || particleShader == null)
        {
            Debug.LogError("[Boundary] URP shaders are unavailable; material migration was skipped.");
            return;
        }

        int migratedCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ImportedAssetsRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!RequiresUrpMigration(material))
                continue;

            bool particleMaterial = path.Contains("/Effects/") ||
                material.shader.name.Contains("Particle") ||
                material.shader.name.Contains("WFX/");
            MigrateMaterial(material, particleMaterial ? particleShader : litShader, particleMaterial);
            EditorUtility.SetDirty(material);
            migratedCount++;
        }

        if (migratedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[Boundary] Migrated {migratedCount} imported materials to URP.");
        }
    }

    private static void MigrateOncePerEditorSession()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        MigrateImportedMaterials();
    }

    private static bool RequiresUrpMigration(Material material)
    {
        if (material == null || material.shader == null)
            return false;

        // Built-in shaders have no asset path. Project and package shaders do,
        // so this leaves already-converted URP materials untouched.
        return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material.shader));
    }

    private static void MigrateMaterial(Material material, Shader targetShader, bool transparent)
    {
        Texture mainTexture = material.HasProperty("_MainTex")
            ? material.GetTexture("_MainTex")
            : material.GetTexture("_BaseMap");
        Color color = material.HasProperty("_Color")
            ? material.GetColor("_Color")
            : material.HasProperty("_TintColor")
                ? material.GetColor("_TintColor")
                : Color.white;
        Color emission = material.HasProperty("_EmissionColor")
            ? material.GetColor("_EmissionColor")
            : Color.black;
        bool additive = material.name.Contains("Add") || material.shader.name.Contains("Additive");

        material.shader = targetShader;
        material.SetTexture("_BaseMap", mainTexture);
        material.SetColor("_BaseColor", color);

        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }

        if (!transparent)
            return;

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 1f : 0f);
        material.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
