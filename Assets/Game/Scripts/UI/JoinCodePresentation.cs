using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class JoinCodePresentation : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_InputField source;
    [SerializeField] private TMP_Text[] digits = new TMP_Text[4];
    [SerializeField] private Image[] borders = new Image[4];

    public void Configure(TMP_InputField input, TMP_Text[] digitLabels, Image[] digitBorders)
    {
        if (source != null) source.onValueChanged.RemoveListener(Refresh);
        source = input;
        digits = digitLabels;
        borders = digitBorders;
        if (source != null) source.onValueChanged.AddListener(Refresh);
        Refresh(source != null ? source.text : string.Empty);
    }

    private void OnEnable()
    {
        if (source != null)
        {
            source.onValueChanged.RemoveListener(Refresh);
            source.onValueChanged.AddListener(Refresh);
            Refresh(source.text);
        }
    }

    private void OnDestroy()
    {
        if (source != null) source.onValueChanged.RemoveListener(Refresh);
    }

    private void Update()
    {
        int active = source != null && source.isFocused ? Mathf.Min(source.text.Length, 3) : -1;
        UITheme theme = UITheme.Current;
        for (int i = 0; i < borders.Length; i++)
            if (borders[i] != null) borders[i].color = i == active && theme != null ? theme.accent : theme != null ? theme.border : Color.gray;
    }

    private void Refresh(string value)
    {
        for (int i = 0; i < digits.Length; i++)
            if (digits[i] != null) digits[i].text = i < value.Length ? value[i].ToString() : "—";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (source == null) return;
        source.ActivateInputField();
        source.Select();
    }
}
