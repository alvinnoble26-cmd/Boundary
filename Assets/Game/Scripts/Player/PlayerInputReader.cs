using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using PurrNet;
using System.Collections;

public class PlayerInputReader : NetworkBehaviour
{
    public InputActionReference move;
    public InputActionReference jump;

    public Vector2 Move { get; private set; }
    public bool IsJumpHeld => isOwner && jump != null && jump.action.IsPressed();
    bool jumpQueued;
    bool bound;

    protected override void OnSpawned()
    {
        StartCoroutine(SetupLocalInput());
    }

    IEnumerator SetupLocalInput()
    {
        // Wait until ownership is actually assigned
        yield return new WaitUntil(() => isOwner);

        // Enable + bind once
        move?.action.Enable();
        jump?.action.Enable();

        if (jump != null && !bound)
        {
            jump.action.performed += OnJump;
            bound = true;
        }

        ConfigureJumpLookDrag();

        Debug.Log("[Input] Enabled for owner.");
    }

    void OnDisable()
    {
        if (jump != null && bound)
        {
            jump.action.performed -= OnJump;
            bound = false;
        }

        move?.action.Disable();
        jump?.action.Disable();
    }

    void Update()
{
    if (!isOwner) return;

    Move = move != null ? move.action.ReadValue<Vector2>() : Vector2.zero;

}


    void OnJump(InputAction.CallbackContext ctx)
    {
        if (!isOwner) return;
        jumpQueued = true;
    }

    public bool ConsumeJump()
    {
        if (!jumpQueued) return false;
        jumpQueued = false;
        return true;
    }

    private static void ConfigureJumpLookDrag()
    {
        foreach (OnScreenButton button in Object.FindObjectsByType<OnScreenButton>(FindObjectsSortMode.None))
        {
            if (button.controlPath != "<Gamepad>/rightTrigger")
                continue;

            if (button.GetComponent<JumpLookButton>() == null)
                button.gameObject.AddComponent<JumpLookButton>();
            if (button.GetComponent<AbilityTouchTransferSource>() == null)
                button.gameObject.AddComponent<AbilityTouchTransferSource>();
        }

        foreach (OnScreenStick stick in Object.FindObjectsByType<OnScreenStick>(FindObjectsSortMode.None))
        {
            if (stick.controlPath != "<Gamepad>/leftStick")
                continue;
            if (stick.GetComponent<AbilityTouchTransferSource>() == null)
                stick.gameObject.AddComponent<AbilityTouchTransferSource>();
        }
    }
}
