using UnityEngine;

// Runtime-only local camera presentation. This is deliberately not a networked
// object and does not need a prefab reference, keeping it out of third-person
// skin synchronization and Player.prefab serialization.
public sealed class FirstPersonArmPresentation : MonoBehaviour
{
    private const float ThrowDuration = 0.16f;
    private const float TeleportDuration = 0.5f;
    private const float GrappleLaunchDuration = 0.12f;
    private const float GrapplePullDuration = 0.18f;

    private Transform arm;
    private Renderer armRenderer;
    private Transform secondArm;
    private Renderer secondArmRenderer;
    private float throwEndsAt;
    private float teleportStartedAt = -1f;
    private float grappleLaunchStartedAt = -1f;
    private float grapplePullStartedAt = -1f;
    private Quaternion grappleAimRotation = Quaternion.identity;
    private bool grapplePullActive;
    private bool movementActive;
    private bool grappleActive;
    private bool hollowActive;
    private Vector3 hollowWorldTarget;
    private string skinId = "beard";
    private GameObject heldBullseyeKnife;
    private Transform bullseyeKnifeGrip;
    private Vector3 bullseyeKnifeLongAxis = Vector3.up;
    private GameObject heldChargeSword;
    private Transform chargeSwordGrip;
    private Vector3 chargeSwordLongAxis = Vector3.up;
    private bool chargeSwordActive;
    private bool bullseyeKnifeActive;
    private GameObject heldSliceSword;
    private Transform sliceSwordGrip;
    private Vector3 sliceSwordLongAxis = Vector3.up;
    private Quaternion sliceSwordHeldRotation = Quaternion.identity;
    private bool sliceSwordActive;
    private float sliceSwingStartedAt = -1f;

    public void SetSkin(string value)
    {
        skinId = value == "turtle" || value == "sun_ducker" ? value : "beard";
        EnsureArm();
        Color color = skinId == "turtle"
            ? new Color(0.16f, 0.55f, 0.20f)
            : skinId == "sun_ducker" ? Color.black : Color.white;
        armRenderer.material.color = color;
        armRenderer.material.SetColor("_Color", color);
        EnsureSecondArm();
        secondArmRenderer.material.color = color;
        secondArmRenderer.material.SetColor("_Color", color);
    }

    public void ShowThrow(Vector3 worldDirection)
    {
        EnsureArm();
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Vector3.forward;

        arm.localPosition = new Vector3(0.16f, -0.20f, 0.58f);
        arm.localRotation = Quaternion.FromToRotation(Vector3.right, localDirection.normalized);
        arm.gameObject.SetActive(true);
        throwEndsAt = Time.time + ThrowDuration;
    }

    public void SetBullseyeKnifeActive(bool active, GameObject knifePrefab)
    {
        EnsureArm();
        bullseyeKnifeActive = active;
        if (active && heldBullseyeKnife == null && knifePrefab != null)
        {
            // MagicSword_Iron is included in PurrNet's prefab catalog, but this
            // copy is camera-only presentation and must never be network-spawned.
            GameObject gripObject = new GameObject("Bullseye Right Hand Knife Grip");
            bullseyeKnifeGrip = gripObject.transform;
            bullseyeKnifeGrip.SetParent(transform, false);
            heldBullseyeKnife = PurrNet.UnityProxy.InstantiateDirectly(knifePrefab, bullseyeKnifeGrip);
            heldBullseyeKnife.name = "Held Bullseye Knife";
            heldBullseyeKnife.transform.localPosition = Vector3.zero;
            // Point the blade inward from the lower-right hand. Rotating local
            // Y toward local -X makes the pose read unmistakably right-handed.
            bullseyeKnifeLongAxis = BullseyeAbility.GetVisualLongAxisLocal(heldBullseyeKnife);
            heldBullseyeKnife.transform.localRotation =
                Quaternion.FromToRotation(bullseyeKnifeLongAxis, Vector3.left);
            heldBullseyeKnife.transform.localScale = Vector3.one;
            BullseyeAbility.PrepareKnifeVisual(heldBullseyeKnife);
            if (BullseyeAbility.TryGetVisualBounds(heldBullseyeKnife, out Bounds bounds))
            {
                const float desiredLength = 0.72f;
                float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (largestDimension > 0.001f)
                    heldBullseyeKnife.transform.localScale *= desiredLength / largestDimension;
                // Re-read after scaling, then put the rendered sword itself in
                // view rather than relying on the model's imported pivot.
                BullseyeAbility.TryGetVisualBounds(heldBullseyeKnife, out bounds);
                Vector3 desiredCenter = bullseyeKnifeGrip.TransformPoint(new Vector3(-0.30f, 0f, 0f));
                heldBullseyeKnife.transform.position += desiredCenter - bounds.center;
            }
            BullseyeKnifeEffects.AttachRedFlames(heldBullseyeKnife, false);
            foreach (Collider collider in heldBullseyeKnife.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            Rigidbody body = heldBullseyeKnife.GetComponent<Rigidbody>();
            if (body != null)
                body.isKinematic = true;
        }
        if (heldBullseyeKnife != null)
            heldBullseyeKnife.SetActive(active);
        if (bullseyeKnifeGrip != null)
            bullseyeKnifeGrip.gameObject.SetActive(active);
        if (active)
            arm.gameObject.SetActive(true);
    }

    public void ThrowBullseyeKnife(Vector3 worldDirection, GameObject knifePrefab)
    {
        SetBullseyeKnifeActive(false, knifePrefab);
        ShowThrow(worldDirection);
    }

    public void SetChargeSwordActive(bool active, GameObject swordPrefab, GameObject lightningAuraPrefab)
    {
        EnsureArm();
        chargeSwordActive = active;
        if (active && heldChargeSword == null && chargeSwordGrip != null)
        {
            Destroy(chargeSwordGrip.gameObject);
            chargeSwordGrip = null;
        }
        if (active && heldChargeSword == null && swordPrefab == null)
            Debug.LogError("[Charge] Frost staff prefab reference is missing on PlayerAbilities.");
        if (active && heldChargeSword == null && swordPrefab != null)
        {
            GameObject gripObject = new GameObject("Charge Right Hand Sword Grip");
            chargeSwordGrip = gripObject.transform;
            chargeSwordGrip.SetParent(transform, false);
            heldChargeSword = PurrNet.UnityProxy.InstantiateDirectly(swordPrefab, chargeSwordGrip);
            if (heldChargeSword == null)
            {
                Debug.LogError("[Charge] Failed to instantiate the held Frost staff.");
                Destroy(gripObject);
                chargeSwordGrip = null;
                return;
            }
            heldChargeSword.name = "Held Charge Frost Sword";
            SetLayerRecursively(heldChargeSword.transform, arm.gameObject.layer);
            heldChargeSword.transform.localPosition = Vector3.zero;
            heldChargeSword.transform.localRotation = Quaternion.identity;
            heldChargeSword.transform.localScale = Vector3.one;
            BullseyeAbility.PrepareKnifeVisual(heldChargeSword);
            chargeSwordLongAxis = BullseyeAbility.GetVisualLongAxisLocal(heldChargeSword);
            heldChargeSword.transform.localRotation =
                Quaternion.FromToRotation(chargeSwordLongAxis, Vector3.up);
            if (BullseyeAbility.TryGetVisualBounds(heldChargeSword, out Bounds bounds))
            {
                float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (largest > 0.001f)
                    heldChargeSword.transform.localScale *= 0.92f / largest;
                BullseyeAbility.TryGetVisualBounds(heldChargeSword, out bounds);
                // The hand sits at the weapon's rendered midpoint so this long
                // sword reads as a staff instead of a sword held by its hilt.
                heldChargeSword.transform.position +=
                    chargeSwordGrip.TransformPoint(new Vector3(0.04f, 0f, 0f)) - bounds.center;
            }
            if (lightningAuraPrefab != null)
            {
                GameObject aura = PurrNet.UnityProxy.InstantiateDirectly(
                    lightningAuraPrefab, heldChargeSword.transform);
                aura.name = "Hovl Lightning Aura Around Charge Sword";
                aura.transform.localPosition = Vector3.zero;
                aura.transform.localRotation = Quaternion.identity;
                aura.transform.localScale = Vector3.one * 0.72f;
                foreach (ParticleSystem particles in aura.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = particles.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.55f, 0.92f, 1f), new Color(0.02f, 0.34f, 1f));
                    particles.Play();
                }
            }
            ChargeSwordElectricity.Attach(heldChargeSword.transform);
            SetLayerRecursively(heldChargeSword.transform, arm.gameObject.layer);
            foreach (Renderer renderer in heldChargeSword.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }
        if (heldChargeSword != null)
            heldChargeSword.SetActive(active);
        if (chargeSwordGrip != null)
            chargeSwordGrip.gameObject.SetActive(active);
        if (active)
            arm.gameObject.SetActive(true);
    }

    public Vector3 GetChargeBallWorldPosition()
    {
        if (heldChargeSword != null && heldChargeSword.activeInHierarchy &&
            BullseyeAbility.TryGetVisualBounds(heldChargeSword, out Bounds bounds))
        {
            // Sword15_Frost has a forked opening close to the upper end. The
            // point is inset from the absolute tip so the charge begins inside
            // that opening, then moved slightly camera-forward to avoid depth
            // fighting with the staff mesh.
            return ChargeAbility.GetStaffChargeOrigin(bounds, transform.up, transform.forward);
        }

        return transform.TransformPoint(new Vector3(0.27f, 0.47f, 0.75f));
    }

    public void SetSliceSwordActive(bool active, GameObject swordPrefab,
        System.Action<Vector3, Vector3> airFractureEmitter = null)
    {
        EnsureArm();
        sliceSwordActive = active;
        if (active && heldSliceSword == null && swordPrefab == null)
            Debug.LogError("[Slice] KatsunesiSword prefab reference is missing on PlayerAbilities.");
        if (active && heldSliceSword == null && swordPrefab != null)
        {
            sliceSwordGrip = new GameObject("Slice Right Hand Sword Grip").transform;
            sliceSwordGrip.SetParent(transform, false);
            heldSliceSword = PurrNet.UnityProxy.InstantiateDirectly(swordPrefab, sliceSwordGrip);
            heldSliceSword.name = "Held Slice Electricity Sword";
            SetLayerRecursively(heldSliceSword.transform, arm.gameObject.layer);
            heldSliceSword.transform.localPosition = Vector3.zero;
            heldSliceSword.transform.localScale = Vector3.one;
            // SliceSwordFlames installs explicit opaque URP materials for this
            // model. Running the generic legacy-material converter first can
            // leave KatsunesiSword on an unsupported magenta shader.
            SliceSwordFlames.PrepareMaterials(heldSliceSword);
            sliceSwordLongAxis = BullseyeAbility.GetVisualLongAxisLocal(heldSliceSword);
            sliceSwordHeldRotation = GetScreenFacingSwordRotation(heldSliceSword);
            heldSliceSword.transform.localRotation = sliceSwordHeldRotation;
            if (BullseyeAbility.TryGetVisualBounds(heldSliceSword, out Bounds bounds))
            {
                float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                const float fittedSwordLength = 0.94f;
                if (largest > 0.001f) heldSliceSword.transform.localScale *= fittedSwordLength / largest;
                BullseyeAbility.TryGetVisualBounds(heldSliceSword, out bounds);
                // Positive blade axis points left in this pose. Put the center
                // left of the grip so the right-side endpoint (the handle) is
                // seated in the hand and the full tip remains on screen.
                Vector3 desiredCenter = sliceSwordGrip.TransformPoint(new Vector3(-0.39f, 0.02f, 0f));
                heldSliceSword.transform.position += desiredCenter - bounds.center;
            }
            foreach (Collider collider in heldSliceSword.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            // Attach presentation only after fitting the mesh. The generated
            // ribbons must not participate in the bounds used to size and
            // place KatsunesiSword, otherwise the model can be pushed out of
            // the camera view or appear much smaller than intended.
            SliceSwordFlames.Attach(heldSliceSword);
        }
        if (heldSliceSword != null && airFractureEmitter != null)
            heldSliceSword.GetComponent<SliceSwordFlames>()?.ConfigureAirFractureEmitter(airFractureEmitter);
        if (heldSliceSword != null) heldSliceSword.SetActive(active);
        if (heldSliceSword != null)
            heldSliceSword.GetComponent<SliceSwordFlames>()?.SetAirFractureEmission(active);
        if (sliceSwordGrip != null) sliceSwordGrip.gameObject.SetActive(active);
        if (active) arm.gameObject.SetActive(true);
    }

    public void SwingSliceSword(Vector3 worldDirection, GameObject swordPrefab)
    {
        SetSliceSwordActive(true, swordPrefab);
        // The suspended two-second scars belong to the held/charging state.
        // The release has its own large dimensional crescent presentation.
        heldSliceSword?.GetComponent<SliceSwordFlames>()?.SetAirFractureEmission(false);
        sliceSwingStartedAt = Time.time;
        StartCoroutine(HideSliceAfterSwing());
    }

    private System.Collections.IEnumerator HideSliceAfterSwing()
    {
        yield return new WaitForSeconds(SliceAbility.SwingDuration);
        sliceSwingStartedAt = -1f;
        SetSliceSwordActive(false, heldSliceSword);
    }

    public void ShowTeleport()
    {
        EnsureArm();
        teleportStartedAt = Time.time;
        arm.gameObject.SetActive(true);
    }

    public void SetHollowActive(bool active, Vector3 worldTarget)
    {
        EnsureArm();
        EnsureSecondArm();
        hollowActive = active;
        if (!active)
        {
            secondArm.gameObject.SetActive(false);
            return;
        }

        hollowWorldTarget = worldTarget;
        arm.gameObject.SetActive(true);
        secondArm.gameObject.SetActive(true);
    }

    public void SetMovementActive(bool active)
    {
        movementActive = active;
    }

    public void SetGrappleActive(bool active, Vector3 worldDirection)
    {
        EnsureArm();
        grappleActive = active;
        if (!active)
        {
            grappleLaunchStartedAt = -1f;
            grapplePullStartedAt = -1f;
            grapplePullActive = false;
            return;
        }
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Vector3.forward;
        grappleAimRotation = Quaternion.FromToRotation(Vector3.right, localDirection.normalized);
        grappleLaunchStartedAt = Time.time;
        grapplePullStartedAt = -1f;
        grapplePullActive = false;
        arm.gameObject.SetActive(true);
    }

    public void PlayGrappleYank()
    {
        EnsureArm();
        grapplePullStartedAt = Time.time;
        grapplePullActive = true;
        arm.gameObject.SetActive(true);
    }

    public Vector3 GrappleOrigin
    {
        get
        {
            EnsureArm();
            return arm.position + arm.right * 0.18f;
        }
    }

    public void Hide()
    {
        throwEndsAt = 0f;
        teleportStartedAt = -1f;
        grappleLaunchStartedAt = -1f;
        grapplePullStartedAt = -1f;
        grapplePullActive = false;
        movementActive = false;
        grappleActive = false;
        hollowActive = false;
        bullseyeKnifeActive = false;
        chargeSwordActive = false;
        if (heldBullseyeKnife != null)
            heldBullseyeKnife.SetActive(false);
        if (bullseyeKnifeGrip != null)
            bullseyeKnifeGrip.gameObject.SetActive(false);
        if (heldChargeSword != null)
            heldChargeSword.SetActive(false);
        if (chargeSwordGrip != null)
            chargeSwordGrip.gameObject.SetActive(false);
        if (arm != null)
            arm.gameObject.SetActive(false);
        if (secondArm != null)
            secondArm.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (arm == null)
            return;

        if (hollowActive)
        {
            float pulse = Mathf.Sin(Time.time * 8f) * 0.018f;
            arm.localPosition = new Vector3(0.31f + pulse, -0.27f, 0.57f);
            secondArm.localPosition = new Vector3(-0.31f - pulse, -0.27f, 0.57f);
            Vector3 localTarget = transform.InverseTransformPoint(hollowWorldTarget);
            Vector3 rightDirection = localTarget - arm.localPosition;
            Vector3 leftDirection = localTarget - secondArm.localPosition;
            if (rightDirection.sqrMagnitude < 0.0001f)
                rightDirection = Vector3.forward;
            if (leftDirection.sqrMagnitude < 0.0001f)
                leftDirection = Vector3.forward;
            arm.localRotation = Quaternion.FromToRotation(Vector3.right, rightDirection.normalized) *
                Quaternion.Euler(0f, 0f, -5f);
            secondArm.localRotation = Quaternion.FromToRotation(Vector3.right, leftDirection.normalized) *
                Quaternion.Euler(0f, 0f, 5f);
        }
        else if (teleportStartedAt >= 0f)
        {
            float progress = Mathf.Clamp01((Time.time - teleportStartedAt) / TeleportDuration);
            arm.localPosition = Vector3.Lerp(
                new Vector3(0.50f, -0.16f, 0.55f),
                new Vector3(-0.38f, -0.30f, 0.55f), progress);
            arm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 35f, progress));
            if (progress >= 1f)
                teleportStartedAt = -1f;
        }
        else if (grappleActive)
        {
            Vector3 restingPosition = new Vector3(0.18f, -0.20f, 0.62f);
            Vector3 extendedPosition = new Vector3(0.70f, -0.16f, 0.82f);
            Vector3 pullingPosition = new Vector3(-0.24f, -0.34f, 0.46f);
            if (!grapplePullActive)
            {
                float launchProgress = Mathf.Clamp01((Time.time - grappleLaunchStartedAt) / GrappleLaunchDuration);
                arm.localPosition = Vector3.Lerp(restingPosition, extendedPosition, launchProgress);
                arm.localRotation = Quaternion.Slerp(Quaternion.identity, grappleAimRotation, launchProgress);
            }
            else
            {
                float pullProgress = Mathf.Clamp01((Time.time - grapplePullStartedAt) / GrapplePullDuration);
                arm.localPosition = Vector3.Lerp(extendedPosition, pullingPosition, pullProgress);
                arm.localRotation = Quaternion.Slerp(grappleAimRotation, Quaternion.Euler(0f, 0f, 52f), pullProgress);
            }
        }
        else if (bullseyeKnifeActive)
        {
            // The forearm enters from the player's lower-right while the
            // knife crosses toward bottom-center, like a right-handed ready pose.
            arm.localPosition = new Vector3(0.31f, -0.39f, 0.70f);
            arm.localRotation = Quaternion.Euler(-5f, -7f, -12f);
        }
        else if (sliceSwordActive)
        {
            float progress = sliceSwingStartedAt < 0f ? 0f :
                Mathf.Clamp01((Time.time - sliceSwingStartedAt) / SliceAbility.SwingDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            // The right arm stays on the lower-right edge. It begins parallel
            // to the bottom of the screen and pivots 90 degrees until its
            // length points forward into the screen.
            arm.localPosition = Vector3.Lerp(new Vector3(0.53f, -0.15f, 0.50f),
                new Vector3(0.53f, -0.07f, 0.58f), eased);
            arm.localRotation = sliceSwingStartedAt < 0f
                ? Quaternion.identity
                : Quaternion.Slerp(Quaternion.identity,
                    Quaternion.FromToRotation(Vector3.right, Vector3.back), eased);
        }
        else if (chargeSwordActive)
        {
            arm.localPosition = new Vector3(0.27f, -0.22f, 0.70f);
            arm.localRotation = Quaternion.Euler(-12f, -8f, 48f);
        }
        else if (movementActive)
        {
            arm.localPosition = new Vector3(0.46f, -0.42f, 0.64f);
            arm.localRotation = Quaternion.Euler(12f, 0f, -18f);
        }

        bool visible = hollowActive || teleportStartedAt >= 0f || grappleActive || bullseyeKnifeActive ||
            chargeSwordActive || sliceSwordActive || movementActive || Time.time < throwEndsAt;
        if (arm.gameObject.activeSelf != visible)
            arm.gameObject.SetActive(visible);
        if (secondArm != null && secondArm.gameObject.activeSelf != hollowActive)
            secondArm.gameObject.SetActive(hollowActive);
        if (bullseyeKnifeGrip != null && bullseyeKnifeActive)
        {
            bullseyeKnifeGrip.localPosition = arm.localPosition;
            bullseyeKnifeGrip.localRotation = arm.localRotation;
            bullseyeKnifeGrip.localScale = Vector3.one;
            // Reassert the fixed blade/forearm relationship after every arm
            // animation update. Local Y is MagicSword_Iron's blade axis.
            heldBullseyeKnife.transform.localRotation =
                Quaternion.FromToRotation(bullseyeKnifeLongAxis, Vector3.left);
        }
        if (chargeSwordGrip != null && chargeSwordActive)
        {
            // Keep the staff vertical in camera space. Only the arm uses the
            // diagonal reaching pose; inheriting that rotation tilted the staff.
            chargeSwordGrip.localPosition = new Vector3(0.27f, -0.20f, 0.75f);
            chargeSwordGrip.localRotation = Quaternion.identity;
            chargeSwordGrip.localScale = Vector3.one;
            heldChargeSword.transform.localRotation =
                Quaternion.FromToRotation(chargeSwordLongAxis, Vector3.up);
        }
        if (sliceSwordGrip != null && sliceSwordActive)
        {
            sliceSwordGrip.localPosition = arm.localPosition +
                arm.localRotation * (Vector3.left * 0.17f) + Vector3.back * 0.08f;
            sliceSwordGrip.localRotation = arm.localRotation;
            sliceSwordGrip.localScale = Vector3.one;
            heldSliceSword.transform.localRotation = sliceSwordHeldRotation;
        }
    }

    private void EnsureArm()
    {
        if (arm != null)
            return;

        GameObject armObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armObject.name = "LocalFirstPersonArm";
        Destroy(armObject.GetComponent<Collider>());
        arm = armObject.transform;
        arm.SetParent(transform, false);
        arm.localScale = new Vector3(0.34f, 0.11f, 0.11f);
        armRenderer = armObject.GetComponent<Renderer>();
        armRenderer.material = CreateMobileSafeMaterial();
        arm.gameObject.SetActive(false);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int index = 0; index < root.childCount; index++)
            SetLayerRecursively(root.GetChild(index), layer);
    }

    private static Quaternion GetScreenFacingSwordRotation(GameObject sword)
    {
        Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (Renderer renderer in sword.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.localBounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 local = sword.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                localMin = Vector3.Min(localMin, local);
                localMax = Vector3.Max(localMax, local);
            }
        }

        Vector3 size = localMax - localMin;
        Vector3 longAxis;
        Vector3 faceWidthAxis;
        if (size.x >= size.y && size.x >= size.z)
        {
            longAxis = Vector3.right;
            faceWidthAxis = size.y >= size.z ? Vector3.up : Vector3.forward;
        }
        else if (size.y >= size.z)
        {
            longAxis = Vector3.up;
            faceWidthAxis = size.x >= size.z ? Vector3.right : Vector3.forward;
        }
        else
        {
            longAxis = Vector3.forward;
            faceWidthAxis = size.x >= size.y ? Vector3.right : Vector3.up;
        }

        Quaternion alignLength = Quaternion.FromToRotation(longAxis, Vector3.left);
        Vector3 alignedWidth = alignLength * faceWidthAxis;
        Quaternion faceCamera = Quaternion.FromToRotation(alignedWidth, Vector3.up);
        return faceCamera * alignLength;
    }

    private void EnsureSecondArm()
    {
        if (secondArm != null)
            return;

        GameObject armObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armObject.name = "LocalFirstPersonArmLeft";
        Destroy(armObject.GetComponent<Collider>());
        secondArm = armObject.transform;
        secondArm.SetParent(transform, false);
        secondArm.localScale = new Vector3(0.34f, 0.11f, 0.11f);
        secondArmRenderer = armObject.GetComponent<Renderer>();
        secondArmRenderer.material = CreateMobileSafeMaterial();
        secondArm.gameObject.SetActive(false);
    }

    private static Material CreateMobileSafeMaterial()
    {
        // Sprite/UI shaders are already retained by the mobile player because
        // the game uses Unity UI. This avoids a runtime-only URP shader being
        // stripped from an iOS build and rendering the arm pink.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        return shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
    }
}
