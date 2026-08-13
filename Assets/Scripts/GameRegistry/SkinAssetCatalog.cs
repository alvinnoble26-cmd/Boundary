using UnityEngine;

[CreateAssetMenu(menuName = "Entropy/Skin Asset Catalog")]
public sealed class SkinAssetCatalog : ScriptableObject
{
    public GameObject turtleModel;
    public Material turtleBodyMaterial;
    public Material turtleAccentMaterial;

    private static SkinAssetCatalog instance;

    public static SkinAssetCatalog Load()
    {
        if (instance == null)
            instance = Resources.Load<SkinAssetCatalog>("SkinAssetCatalog");
        return instance;
    }
}
