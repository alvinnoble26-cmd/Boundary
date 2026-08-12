using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class SkinPreview3D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private const int PreviewLayer = 30;
    private const float DragSpeed = 0.42f;
    private static int nextPreviewIndex;

    private RenderTexture texture;
    private GameObject previewWorld;
    private GameObject modelPivot;
    private Camera previewCamera;
    private bool dragging;
    private float yaw = 18f;
    private float pitch = -4f;

    public void Initialize(string skinId)
    {
        texture = new RenderTexture(384, 512, 16, RenderTextureFormat.ARGB32)
        {
            name = "SkinPreview_" + skinId,
            antiAliasing = 2,
            useMipMap = false
        };
        texture.Create();

        RawImage image = GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = true;

        previewWorld = new GameObject("SkinPreviewWorld_" + skinId);
        previewWorld.hideFlags = HideFlags.HideAndDontSave;
        previewWorld.transform.position = new Vector3(10000f + nextPreviewIndex++ * 20f, -10000f, 10000f);
        SetLayer(previewWorld);

        modelPivot = new GameObject("RotatableSkin");
        modelPivot.transform.SetParent(previewWorld.transform, false);
        SetLayer(modelPivot);

        if (skinId == "sun_ducker") BuildSunDucker();
        else BuildBeard();

        CreateLightingAndCamera();
        ApplyRotation();
    }

    private void Update()
    {
        if (modelPivot == null) return;
        if (!dragging)
        {
            yaw += 8f * Time.unscaledDeltaTime;
            ApplyRotation();
        }
    }

    public void OnPointerDown(PointerEventData eventData) => dragging = true;

    public void OnPointerUp(PointerEventData eventData) => dragging = false;

    public void OnDrag(PointerEventData eventData)
    {
        if (modelPivot == null) return;
        yaw -= eventData.delta.x * DragSpeed;
        pitch = Mathf.Clamp(pitch - eventData.delta.y * DragSpeed * .45f, -22f, 22f);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        modelPivot.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void BuildBeard()
    {
        Material body = MakeMaterial("Beard Body", new Color(.48f, .46f, .48f), .62f, .05f);
        Material visor = MakeMaterial("Beard Visor", new Color(.035f, .038f, .045f), .26f, .15f);
        CreatePrimitive("Body", PrimitiveType.Capsule, Vector3.zero, Vector3.one, Vector3.zero, body);
        CreatePrimitive("Visor", PrimitiveType.Cube, new Vector3(0f, .5f, .45f),
            new Vector3(.5f, .2f, .2f), Vector3.zero, visor);
    }

    private void BuildSunDucker()
    {
        Material body = MakeMaterial("Sun Ducker Body", new Color(.58f, .56f, .52f), .48f, .02f);
        Material red = MakeMaterial("Sun Ducker Red", new Color(.95f, .12f, .055f), .42f, .04f);
        Material orange = MakeMaterial("Sun Ducker Orange", new Color(1f, .28f, .05f), .45f, .03f);

        CreatePrimitive("Body", PrimitiveType.Capsule, Vector3.zero, Vector3.one, Vector3.zero, body);
        CreatePrimitive("Top", PrimitiveType.Cube, new Vector3(0f, .75f, 0f),
            new Vector3(.5f, .5f, .5f), Vector3.zero, red);
        CreatePrimitive("Top Middle", PrimitiveType.Cube, new Vector3(0f, .7f, 0f),
            new Vector3(.6f, .5f, .6f), Vector3.zero, orange);
        CreatePrimitive("Top Crown", PrimitiveType.Cube, new Vector3(0f, .65f, 0f),
            new Vector3(.7f, .5f, .7f), Vector3.zero, red);
        SunDuckerDemonVisual.Build(modelPivot.transform, PreviewLayer);
    }

    private void CreateLightingAndCamera()
    {
        GameObject cameraObject = new GameObject("PreviewCamera");
        cameraObject.transform.SetParent(previewWorld.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, .25f, 4f);
        cameraObject.transform.LookAt(previewWorld.transform.position + Vector3.up * .18f);
        SetLayer(cameraObject);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(.022f, .008f, .05f, 1f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.fieldOfView = 30f;
        previewCamera.nearClipPlane = .1f;
        previewCamera.farClipPlane = 20f;
        previewCamera.targetTexture = texture;

        CreateLight("Key Light", new Vector3(-2f, 3f, 3f), 1.7f, new Color(1f, .9f, .78f));
        CreateLight("Rim Light", new Vector3(2.5f, 1.5f, -2f), 1.15f, new Color(.55f, .3f, 1f));
    }

    private void CreateLight(string lightName, Vector3 position, float intensity, Color color)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(previewWorld.transform, false);
        lightObject.transform.localPosition = position;
        lightObject.transform.LookAt(previewWorld.transform.position);
        SetLayer(lightObject);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.cullingMask = 1 << PreviewLayer;
    }

    private GameObject CreatePrimitive(string objectName, PrimitiveType type, Vector3 position,
        Vector3 scale, Vector3 euler, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = objectName;
        part.transform.SetParent(modelPivot.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localEulerAngles = euler;
        SetLayer(part);
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return part;
    }

    private static Material MakeMaterial(string materialName, Color color, float smoothness,
        float metallic, bool emission = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
        return material;
    }

    private static void SetLayer(GameObject target)
    {
        target.layer = PreviewLayer;
        foreach (Transform child in target.transform) SetLayer(child.gameObject);
    }

    private void OnEnable()
    {
        if (previewCamera != null) previewCamera.enabled = true;
    }

    private void OnDisable()
    {
        dragging = false;
        if (previewCamera != null) previewCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (previewCamera != null) previewCamera.targetTexture = null;
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
        if (previewWorld != null) Destroy(previewWorld);
    }
}
