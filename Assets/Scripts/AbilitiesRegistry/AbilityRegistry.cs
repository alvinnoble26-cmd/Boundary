using System.Collections.Generic;
using UnityEngine;

public class AbilityRegistry : MonoBehaviour
{
    private Dictionary<AbilityId, IAbility> map = new Dictionary<AbilityId, IAbility>();

    private void Awake()
    {
        map.Clear();

        var abilities = GetComponents<IAbility>();
        foreach (var a in abilities)
        {
            map[a.Id] = a;
        }
    }

    public bool TryGet(AbilityId id, out IAbility ability)
    {
        return map.TryGetValue(id, out ability);
    }
}
