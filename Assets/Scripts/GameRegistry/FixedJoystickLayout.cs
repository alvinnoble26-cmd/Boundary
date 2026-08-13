using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// Applies the saved control layout before OnScreenStick.Start caches its rest
/// position. Without this ordering, the stick returns to its old scene origin
/// after the first touch and overwrites the player's edited position.
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(OnScreenStick))]
public sealed class FixedJoystickLayout : MonoBehaviour
{
    private void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            ControlLayoutSettings.ApplyToGameCanvas(canvas);

        OnScreenStick stick = GetComponent<OnScreenStick>();
        stick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;
        stick.useIsolatedInputActions = true;
    }
}
