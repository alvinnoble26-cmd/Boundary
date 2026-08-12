using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilitiesSelectUI : MonoBehaviour
{
    [SerializeField] private AbilityId abilityId;
    [SerializeField] private Button button;

    [Header("Text Highlight")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);

        if (button != null)
        {
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }
    }

    private void OnEnable()
    {
        if (LoadoutManager.I != null)
            LoadoutManager.I.OnLoadoutChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (LoadoutManager.I != null)
            LoadoutManager.I.OnLoadoutChanged -= Refresh;
    }

    private void OnClicked()
    {
        if (LoadoutManager.I == null) return;

        LoadoutManager.I.Toggle(abilityId);   
    }

    private void Refresh()
    {
        bool selected = (LoadoutManager.I != null && LoadoutManager.I.IsSelected(abilityId));
        if (label != null)
            label.color = selected ? selectedColor : normalColor;
    }
}
