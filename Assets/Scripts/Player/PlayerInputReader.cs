using UnityEngine;
using UnityEngine.InputSystem;
using PurrNet;
using System.Collections;

public class PlayerInputReader : NetworkBehaviour
{
    public InputActionReference move;
    public InputActionReference jump;

    public Vector2 Move { get; private set; }
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
}
