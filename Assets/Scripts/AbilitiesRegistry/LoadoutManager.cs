using System;
using System.Collections.Generic;
using UnityEngine;

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
