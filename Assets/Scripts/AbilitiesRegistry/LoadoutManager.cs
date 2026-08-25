using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadoutManager : MonoBehaviour
{
    public static LoadoutManager I { get; private set; }

    [SerializeField] private int maxSlots = 3;

    public List<AbilityId> selectedAbilities = new List<AbilityId>();

    public event Action OnLoadoutChanged;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (I == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Menu")
        {
            EnsureGrappleSelector();
            EnsureHollowSelector();
            EnsureVoidSelector();
            EnsureAbilityInformationUI();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Menu")
            return;

        EnsureGrappleSelector();
        EnsureHollowSelector();
        EnsureVoidSelector();
        EnsureAbilityInformationUI();
    }

    public static AbilityInformationUI EnsureAbilityInformationUI()
    {
        GameObject abilitiesMenu = GameObject.Find("AbilitiesMenu");
        if (abilitiesMenu == null)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform candidate in transforms)
            {
                if (candidate != null && candidate.name == "AbilitiesMenu" && candidate.gameObject.scene.IsValid())
                {
                    abilitiesMenu = candidate.gameObject;
                    break;
                }
            }
        }
        if (abilitiesMenu == null)
            return null;

        AbilityInformationUI information = abilitiesMenu.GetComponent<AbilityInformationUI>();
        if (information == null)
            information = abilitiesMenu.AddComponent<AbilityInformationUI>();
        information.EnsureBuilt();
        return information;
    }

    public static AbilitiesSelectUI EnsureGrappleSelector()
    {
        AbilitiesSelectUI[] selectors = FindObjectsByType<AbilitiesSelectUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AbilitiesSelectUI selector in selectors)
        {
            if (selector.AbilityId == AbilityId.Grapple)
                return selector;
        }
        AbilitiesSelectUI teleport = null;
        foreach (AbilitiesSelectUI selector in selectors)
        {
            if (selector.AbilityId == AbilityId.Teleport)
            {
                teleport = selector;
                break;
            }
        }
        if (teleport == null)
            return null;

        AbilitiesSelectUI grapple = Instantiate(teleport, teleport.transform.parent);
        grapple.gameObject.name = "Grapple";
        RectTransform rect = grapple.transform as RectTransform;
        RectTransform teleportRect = teleport.transform as RectTransform;
        if (rect != null && teleportRect != null)
        {
            // The panel has no third column: moving Teleport left overlaps Attract,
            // while moving Grapple right goes off-screen. Keep Teleport unchanged
            // and use the open space immediately above it for Grapple.
            const float verticalAbilitySpacing = 46f;
            Vector2 teleportPosition = teleportRect.anchoredPosition;
            rect.anchoredPosition = teleportPosition + new Vector2(0f, verticalAbilitySpacing);
        }

        grapple.transform.SetAsLastSibling();
        grapple.Configure(AbilityId.Grapple, "Grapple");
        return grapple;
    }

    public static AbilitiesSelectUI EnsureHollowSelector()
    {
        AbilitiesSelectUI[] selectors = FindObjectsByType<AbilitiesSelectUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AbilitiesSelectUI selector in selectors)
        {
            if (selector.AbilityId == AbilityId.Hollow)
                return selector;
        }

        AbilitiesSelectUI template = EnsureGrappleSelector();
        if (template == null)
            return null;

        AbilitiesSelectUI hollow = Instantiate(template, template.transform.parent);
        hollow.gameObject.name = "Hollow";
        RectTransform rect = hollow.transform as RectTransform;
        RectTransform templateRect = template.transform as RectTransform;
        if (rect != null && templateRect != null)
            rect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, 46f);

        hollow.transform.SetAsLastSibling();
        hollow.Configure(AbilityId.Hollow, "Hollow");
        return hollow;
    }

    public static AbilitiesSelectUI EnsureVoidSelector()
    {
        AbilitiesSelectUI[] selectors = FindObjectsByType<AbilitiesSelectUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AbilitiesSelectUI selector in selectors)
        {
            if (selector.AbilityId == AbilityId.Void)
                return selector;
        }

        AbilitiesSelectUI template = EnsureHollowSelector();
        if (template == null)
            return null;

        AbilitiesSelectUI voidSelector = Instantiate(template, template.transform.parent);
        voidSelector.gameObject.name = "Void";
        RectTransform rect = voidSelector.transform as RectTransform;
        RectTransform templateRect = template.transform as RectTransform;
        if (rect != null && templateRect != null)
            rect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, 46f);

        voidSelector.transform.SetAsLastSibling();
        voidSelector.Configure(AbilityId.Void, "Void");
        return voidSelector;
    }

    public bool IsSelected(AbilityId id)
    {
        return selectedAbilities.Contains(id);
    }

    public void Toggle(AbilityId id)
    {
        if (selectedAbilities.Contains(id))
        {
            selectedAbilities.Remove(id);
            OnLoadoutChanged?.Invoke();
            return;
        }

        if (selectedAbilities.Count >= maxSlots)
        {
            selectedAbilities.RemoveAt(0); // FIFO remove oldest
        }

        selectedAbilities.Add(id);
        OnLoadoutChanged?.Invoke();
    }

    public void Clear()
    {
        selectedAbilities.Clear();
        OnLoadoutChanged?.Invoke();
    }
}
