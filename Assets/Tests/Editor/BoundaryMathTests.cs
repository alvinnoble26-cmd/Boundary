#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BoundaryMathTests
{
    [Test]
    public void TransitionRadius_IsSmoothAndHitsBothEndpoints()
    {
        Assert.That(BoundaryMath.TransitionRadius(106f, 68f, 0f, 7f), Is.EqualTo(106f).Within(0.001f));
        Assert.That(BoundaryMath.TransitionRadius(106f, 68f, 7f, 7f), Is.EqualTo(68f).Within(0.001f));
        Assert.That(BoundaryMath.TransitionRadius(106f, 68f, 3.5f, 7f), Is.EqualTo(87f).Within(0.001f));
    }

    [Test]
    public void StableGrounding_MeaningfullyReducesPull()
    {
        Vector3 player = Vector3.zero;
        Vector3 singularity = new Vector3(0f, 28f, 0f);
        Vector3 center = Vector3.zero;
        Vector3 airborne = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 106f, 5.5f, false);
        Vector3 grounded = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 106f, 5.5f, true);

        Assert.That(grounded.magnitude, Is.LessThan(airborne.magnitude * 0.2f));
    }

    [Test]
    public void ClosedRingPressure_PushesOutsidePlayerInwardAndUpward()
    {
        Vector3 acceleration = BoundaryMath.PlayerPullAcceleration(
            new Vector3(16f, 0f, 0f),
            new Vector3(0f, 28f, 0f),
            Vector3.zero,
            -1f,
            10f,
            12f,
            true);

        Assert.That(acceleration.x, Is.LessThan(-5f));
        Assert.That(acceleration.y, Is.GreaterThan(7f));
    }

    [Test]
    public void GravityPulse_HasWarningPeakAndRecovery()
    {
        Assert.That(BoundaryMath.RhythmicPulse(0.5f, 4f, 1f, 1f), Is.Zero);
        Assert.That(BoundaryMath.RhythmicPulse(1.5f, 4f, 1f, 1f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(BoundaryMath.RhythmicPulse(2.5f, 4f, 1f, 1f), Is.Zero);
    }

    [Test]
    public void PlatformVoid_HasAnImmediateKillPlaneWithoutARescueCurrent()
    {
        Vector3 acceleration = BoundaryMath.PlayerPullAcceleration(
            new Vector3(22f, -8f, 0f),
            new Vector3(0f, 32f, 0f),
            Vector3.zero,
            -0.9f,
            68f,
            2.1f,
            false);

        Assert.That(BoundaryMath.IsBelowVoidKillPlane(-5f, -0.9f, 4f), Is.True);
        Assert.That(BoundaryMath.IsBelowVoidKillPlane(-4f, -0.9f, 4f), Is.False);
        Assert.That(acceleration.y, Is.LessThan(10f));
    }

    [Test]
    public void StableHash_IsRepeatableAndVariesByIndex()
    {
        int first = BoundaryMath.StableHash(90210, 4);
        Assert.That(BoundaryMath.StableHash(90210, 4), Is.EqualTo(first));
        Assert.That(BoundaryMath.StableHash(90210, 5), Is.Not.EqualTo(first));
    }

    [TestCase(BoundaryDisaster.BlackRain, "BLACK RAIN")]
    [TestCase(BoundaryDisaster.UnstableMass, "UNSTABLE MASS")]
    public void EveryDisasterHasReadablePresentation(BoundaryDisaster disaster, string expectedName)
    {
        Assert.That(BoundaryMath.DisasterName(disaster), Is.EqualTo(expectedName));
        Assert.That(BoundaryMath.DisasterHint(disaster), Is.Not.Empty);
    }

    [Test]
    public void RemovedDisasters_AreNotInTheDisasterPool()
    {
        CollectionAssert.DoesNotContain(System.Enum.GetNames(typeof(BoundaryDisaster)), "ReverseCurrent");
        CollectionAssert.DoesNotContain(System.Enum.GetNames(typeof(BoundaryDisaster)), "FalseSingularities");
        Assert.That(System.Enum.GetValues(typeof(BoundaryDisaster)).Length - 1, Is.EqualTo(8));
    }

    [Test]
    public void EveryRemainingDisaster_IsMorePowerfulThanBaseline()
    {
        foreach (BoundaryDisaster disaster in System.Enum.GetValues(typeof(BoundaryDisaster)))
        {
            if (disaster == BoundaryDisaster.None)
                continue;

            Assert.That(BoundaryMath.DisasterPower(disaster), Is.GreaterThanOrEqualTo(1.35f),
                disaster + " was not strengthened.");
        }
    }

    [Test]
    public void ArenaMassPopulation_LeavesExactlyOneQuarterForInnerRing()
    {
        Assert.That(BoundaryMatchController.ArenaMassPopulation, Is.EqualTo(20));
        Assert.That(BoundaryMatchController.ArenaMassInnerSurvivors,
            Is.EqualTo(BoundaryMatchController.ArenaMassPopulation / 4));
    }

    [Test]
    public void ArenaMasses_AreLargeAndAbilityPulseIsDecisive()
    {
        Assert.That(BoundaryMatchController.HazardSizeMultiplier, Is.EqualTo(1.6f));
        Assert.That(BoundaryMatchController.ArenaMassCubeScale, Is.EqualTo(4.48f).Within(0.001f));
        Assert.That(BoundaryMatchController.ArenaMassBlackHoleScale, Is.EqualTo(2.8f).Within(0.001f));
        Assert.That(BoundaryMatchController.ScaleBoundaryHazard(2f), Is.EqualTo(3.2f).Within(0.001f));
        Assert.That(BoundaryMatchController.EventHazardSizeMultiplier, Is.EqualTo(1.5f));
        Assert.That(BoundaryMatchController.ScaleEventBoundaryHazard(2f),
            Is.EqualTo(4.8f).Within(0.001f));
        Assert.That(BoundaryMath.ArenaMassAbilityVelocityChange(0f), Is.EqualTo(0f).Within(0.001f));
        Assert.That(BoundaryMath.ArenaMassAbilityVelocityChange(1f), Is.EqualTo(88f).Within(0.001f));
    }

    [Test]
    public void FieldResponse_FadesWithDistanceAndMass()
    {
        float nearLight = BoundaryMath.FieldVelocityChange(220f, 88f, 1f, 2.5f);
        float farLight = BoundaryMath.FieldVelocityChange(220f, 88f, 0.2f, 2.5f);
        float nearHeavy = BoundaryMath.FieldVelocityChange(220f, 88f, 1f, 10f);

        Assert.That(nearLight, Is.EqualTo(88f).Within(0.001f));
        Assert.That(farLight, Is.LessThan(nearLight));
        Assert.That(nearHeavy, Is.LessThan(nearLight));
        Assert.That(nearHeavy, Is.EqualTo(22f).Within(0.001f));
    }

    [Test]
    public void FieldAcceleration_CapsConfiguredForce()
    {
        Assert.That(BoundaryMath.FieldVelocityChange(1000f, 35f, 1f, 1f),
            Is.EqualTo(35f).Within(0.001f));
    }

    [Test]
    public void ArenaCubesAndGroundBlackHoles_AreLethalOnContact()
    {
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.Cube, true), Is.True);
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, true), Is.True);
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, false), Is.False);
    }

    [Test]
    public void PlatformGrid_OverlapsAndTierRampsRemainSlideable()
    {
        float spacing = BoundaryMath.DensePlatformSpacing(9.2f, 0.4f);
        float outerSlope = BoundaryMath.TierRampSlopeDegrees(2f, 14f);
        float innerSlope = BoundaryMath.TierRampSlopeDegrees(2.25f, 14f);

        Assert.That(spacing, Is.EqualTo(8.8f).Within(0.001f));
        Assert.That(spacing, Is.LessThan(9.2f));
        Assert.That(outerSlope, Is.LessThan(10f));
        Assert.That(innerSlope, Is.LessThan(10f));
    }

    [Test]
    public void StableWallVariation_IsDeterministicAndVaried()
    {
        Assert.That(BoundaryMath.StableUnit(81017, 3),
            Is.EqualTo(BoundaryMath.StableUnit(81017, 3)).Within(0.000001f));
        Assert.That(BoundaryMath.StableUnit(81017, 3),
            Is.Not.EqualTo(BoundaryMath.StableUnit(81017, 4)));
    }

    [Test]
    public void WallBands_RemoveInnerCoverAndReduceOuterCover()
    {
        Assert.That(BoundaryArenaPresentation.WallCountForBand(7, 0), Is.Zero);
        Assert.That(BoundaryArenaPresentation.WallCountForBand(7, 1), Is.EqualTo(7));
        Assert.That(BoundaryArenaPresentation.WallCountForBand(7, 2), Is.EqualTo(5));
    }

    [Test]
    public void ElevatedAbilityLaunch_StaysAbovePlayerCenter()
    {
        Bounds owner = new Bounds(new Vector3(2f, 1f, 3f), new Vector3(1f, 2f, 1f));
        Vector3 elevated = ProjectileLaunchUtility.ElevatedLaunchCenter(
            owner, new Vector3(5f, -2f, 4f), 1.35f);

        Assert.That(elevated.y, Is.EqualTo(2.35f).Within(0.001f));
    }

    [Test]
    public void SingularityProximity_SmoothlyReducesDownwardGravity()
    {
        Vector3 singularity = new Vector3(0f, 32f, 0f);
        float far = BoundaryMath.SingularityProximity01(Vector3.zero, singularity);
        float near = BoundaryMath.SingularityProximity01(new Vector3(0f, 25f, 0f), singularity);

        Assert.That(near, Is.GreaterThan(far));
        Assert.That(BoundaryMath.BoundaryFallGravityMultiplier(near),
            Is.LessThan(BoundaryMath.BoundaryFallGravityMultiplier(far)));
        Assert.That(BoundaryMath.BoundaryFallGravityMultiplier(1f), Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void SingularityPull_GrowsModeratelyWithProximity()
    {
        Vector3 singularity = new Vector3(0f, 32f, 0f);
        Vector3 farPull = BoundaryMath.PlayerPullAcceleration(
            new Vector3(0f, 4f, 0f), singularity, Vector3.zero, -0.9f, 38f, 2.75f, false);
        Vector3 nearPull = BoundaryMath.PlayerPullAcceleration(
            new Vector3(0f, 25f, 0f), singularity, Vector3.zero, -0.9f, 38f, 2.75f, false);

        Assert.That(nearPull.y, Is.GreaterThan(farPull.y));
        Assert.That(nearPull.y, Is.LessThan(farPull.y * 2.8f));
    }

    [Test]
    public void SkinTemplateLookup_FindsTemplatesUnderInactiveRoot()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject skins = new GameObject("skins");
        GameObject turtle = new GameObject("Turtle");
        turtle.transform.SetParent(skins.transform, false);
        skins.SetActive(false);

        Assert.That(PlayerAbilities.FindSkinTemplateInScene(scene, "Turtle"),
            Is.EqualTo(turtle.transform));

        Object.DestroyImmediate(skins);
    }

    [Test]
    public void CustomizationPanels_UseDarkBlueBackgrounds()
    {
        Color controls = ControlLayoutEditorUI.PanelColor;
        Color skins = SkinShopUI.PanelColor;

        Assert.That(controls.b, Is.GreaterThan(controls.r));
        Assert.That(controls.b, Is.GreaterThan(controls.g));
        Assert.That(skins.b, Is.GreaterThan(skins.r));
        Assert.That(skins.b, Is.GreaterThan(skins.g));
        Assert.That(controls.b, Is.LessThan(0.25f));
        Assert.That(skins.b, Is.LessThan(0.25f));
    }

    [Test]
    public void BoundaryEventBanner_UsesCompactReadableDimensions()
    {
        Assert.That(BoundaryHUD.EventBannerWidth, Is.EqualTo(650f));
        Assert.That(BoundaryHUD.EventBannerHeight, Is.EqualTo(142f));
        Assert.That(BoundaryHUD.EventTitleFontSize, Is.EqualTo(29));
        Assert.That(BoundaryHUD.EventCountdownFontSize, Is.EqualTo(23));
        Assert.That(BoundaryHUD.EventHintFontSize, Is.EqualTo(15));
        Assert.That(BoundaryHUD.EventBannerWidth, Is.LessThan(850f));
        Assert.That(BoundaryHUD.EventBannerHeight, Is.LessThan(190f));
    }

    [Test]
    public void Crosshair_DefaultsToFixedCenterWithEditableScale()
    {
        ControlLayoutSettings.LayoutData layout = ControlLayoutSettings.CreateDefault();
        ControlLayoutSettings.ControlEntry crosshair = layout.Find(ControlLayoutSettings.CrosshairControlId);

        Assert.That(crosshair, Is.Not.Null);
        Assert.That(crosshair.x, Is.EqualTo(0.5f));
        Assert.That(crosshair.y, Is.EqualTo(0.5f));
        Assert.That(crosshair.scale, Is.EqualTo(1f));
        Assert.That(ControlLayoutSettings.CrosshairBaseSize, Is.EqualTo(42f));
    }

    [Test]
    public void Crosshair_RuntimeVisualIsWhitePlusAtExactCenter()
    {
        GameObject canvasObject = new GameObject("Crosshair Canvas", typeof(Canvas));
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            ControlLayoutSettings.ApplyToGameCanvas(canvas);
            RectTransform crosshair = canvas.transform.Find("Aim Crosshair") as RectTransform;

            Assert.That(crosshair, Is.Not.Null);
            Assert.That(crosshair.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(crosshair.anchorMax, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(crosshair.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(crosshair.Find("Horizontal").GetComponent<Image>().color, Is.EqualTo(Color.white));
            Assert.That(crosshair.Find("Vertical").GetComponent<Image>().color, Is.EqualTo(Color.white));
            Assert.That(crosshair.Find("Horizontal").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(crosshair.Find("Vertical").GetComponent<Image>().raycastTarget, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void FirstPersonCamera_UsesEyePoseWithoutThirdPersonOffset()
    {
        Vector3 playerPosition = new Vector3(10f, 2f, 3f);
        Vector3 localEyeOffset = new Vector3(0f, 0.72f, 0.08f);
        Vector3 eyePosition = Cam.CalculateFirstPersonEyePosition(
            playerPosition,
            90f,
            localEyeOffset);
        Quaternion viewRotation = Cam.CalculateFirstPersonViewRotation(-30f, 90f);

        Assert.That(eyePosition.x, Is.EqualTo(10.08f).Within(0.001f));
        Assert.That(eyePosition.y, Is.EqualTo(2.72f).Within(0.001f));
        Assert.That(eyePosition.z, Is.EqualTo(3f).Within(0.001f));
        Assert.That(Quaternion.Angle(viewRotation, Quaternion.Euler(-30f, 90f, 0f)),
            Is.LessThan(0.001f));
    }

    [Test]
    public void FirstPersonCamera_UsesPerspectiveAndClippingSafeLens()
    {
        GameObject cameraObject = new GameObject("First Person Camera Test", typeof(Camera));
        try
        {
            Camera unityCamera = cameraObject.GetComponent<Camera>();
            unityCamera.orthographic = true;
            unityCamera.nearClipPlane = 0.3f;
            unityCamera.fieldOfView = 30f;

            Cam.ConfigureFirstPersonCamera(
                unityCamera,
                Cam.DefaultFirstPersonNearClip,
                Cam.DefaultFirstPersonFieldOfView);

            Assert.That(unityCamera.orthographic, Is.False);
            Assert.That(unityCamera.nearClipPlane,
                Is.EqualTo(Cam.DefaultFirstPersonNearClip).Within(0.001f));
            Assert.That(unityCamera.fieldOfView,
                Is.EqualTo(Cam.DefaultFirstPersonFieldOfView).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void CameraFieldOfView_DefaultsAndClampsToComfortableFirstPersonRange()
    {
        ControlLayoutSettings.LayoutData defaults = ControlLayoutSettings.CreateDefault();

        Assert.That(defaults.cameraFieldOfView,
            Is.EqualTo(ControlLayoutSettings.DefaultCameraFieldOfView));
        Assert.That(ControlLayoutSettings.NormalizeCameraFieldOfView(0f),
            Is.EqualTo(ControlLayoutSettings.DefaultCameraFieldOfView),
            "Old layouts without an FOV value must migrate to the default.");
        Assert.That(ControlLayoutSettings.NormalizeCameraFieldOfView(35f),
            Is.EqualTo(ControlLayoutSettings.MinimumCameraFieldOfView));
        Assert.That(ControlLayoutSettings.NormalizeCameraFieldOfView(140f),
            Is.EqualTo(ControlLayoutSettings.MaximumCameraFieldOfView));
        Assert.That(ControlLayoutSettings.NormalizeCameraFieldOfView(96f), Is.EqualTo(96f));
    }

    [Test]
    public void EditControls_CameraRowsHaveCompactHandlesAndDoNotOverlap()
    {
        EventSystem existingEventSystem = EventSystem.current;
        GameObject canvasObject = new GameObject("Edit Controls Canvas", typeof(Canvas));
        GameObject options = new GameObject("OptionsMenu");
        options.transform.SetParent(canvasObject.transform, false);
        try
        {
            ControlLayoutEditorUI editor = canvasObject.AddComponent<ControlLayoutEditorUI>();
            editor.Build(options);

            Transform topBar = canvasObject.transform.Find("ControlLayoutEditor/TopBar");
            Assert.That(topBar, Is.Not.Null);
            RectTransform sensitivity = topBar.Find("SensitivitySlider") as RectTransform;
            RectTransform fieldOfView = topBar.Find("FieldOfViewSlider") as RectTransform;
            Assert.That(sensitivity, Is.Not.Null);
            Assert.That(fieldOfView, Is.Not.Null);
            RectTransform sensitivityHandle = sensitivity.Find("Handle") as RectTransform;
            RectTransform fieldOfViewHandle = fieldOfView.Find("Handle") as RectTransform;

            Assert.That(sensitivityHandle.sizeDelta.y, Is.LessThanOrEqualTo(30f));
            Assert.That(fieldOfViewHandle.sizeDelta.y, Is.LessThanOrEqualTo(30f));

            float rowDistance = Mathf.Abs(
                sensitivity.anchoredPosition.y - fieldOfView.anchoredPosition.y);
            float minimumGap = (sensitivityHandle.sizeDelta.y + fieldOfViewHandle.sizeDelta.y) * 0.5f + 24f;
            Assert.That(rowDistance, Is.GreaterThan(minimumGap),
                "Sensitivity and FOV slider handles must have a visible vertical gap.");
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            if (existingEventSystem == null)
            {
                foreach (EventSystem eventSystem in Object.FindObjectsByType<EventSystem>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(eventSystem.gameObject);
            }
        }
    }

    [Test]
    public void FirstPersonOwner_HidesOnlyItsBodyRenderersAndRestoresTheirState()
    {
        GameObject player = new GameObject("First Person Player Test");
        try
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform, false);
            Renderer bodyRenderer = visual.GetComponent<Renderer>();

            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "eye";
            eye.transform.SetParent(player.transform, false);
            Renderer eyeRenderer = eye.GetComponent<Renderer>();
            eyeRenderer.forceRenderingOff = true;

            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            Cam cameraController = pivot.AddComponent<Cam>();

            cameraController.SetLocalVisualVisibility(true);
            Assert.That(bodyRenderer.forceRenderingOff, Is.True);
            Assert.That(eyeRenderer.forceRenderingOff, Is.True);

            cameraController.SetLocalVisualVisibility(false);
            Assert.That(bodyRenderer.forceRenderingOff, Is.False);
            Assert.That(eyeRenderer.forceRenderingOff, Is.True,
                "A renderer hidden before first-person setup must stay hidden.");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void TouchLookDelta_IsConsumedExactlyOnce()
    {
        GameObject eventSystemObject = new GameObject("Look Event System", typeof(EventSystem));
        GameObject lookObject = new GameObject("Touch Look Test");
        try
        {
            TouchLookHandler look = lookObject.AddComponent<TouchLookHandler>();
            PointerEventData pointer = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
            {
                delta = new Vector2(12f, -7f)
            };

            look.OnPointerDown(pointer);
            look.OnDrag(pointer);

            Assert.That(look.ConsumeLookDelta(), Is.EqualTo(new Vector2(12f, -7f)));
            Assert.That(look.ConsumeLookDelta(), Is.EqualTo(Vector2.zero));
        }
        finally
        {
            Object.DestroyImmediate(lookObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [Test]
    public void MainMenuButtons_AreCenteredDespiteScaledParent()
    {
        GameObject root = new GameObject("Menu Root", typeof(RectTransform));
        GameObject menu = new GameObject("MuiltiplayerMenu", typeof(RectTransform));
        menu.transform.SetParent(root.transform, false);
        menu.transform.localScale = new Vector3(15f, 2f, 1f);
        ((RectTransform)menu.transform).anchoredPosition = new Vector2(9.12f, -4.68f);

        string[] names = { "HOST", "JOIN", "PracticeButton", "BackButton (4)" };
        foreach (string name in names)
        {
            GameObject button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(menu.transform, false);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.537f, 0.5f);
            rect.anchorMax = new Vector2(0.537f, 0.5f);
            rect.anchoredPosition = new Vector2(12f, 20f);
        }

        MenuButtonTextAlignment alignment = root.AddComponent<MenuButtonTextAlignment>();
        alignment.CenterMainMenuStack();
        foreach (string name in names)
        {
            RectTransform rect = menu.transform.Find(name) as RectTransform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(rect.anchoredPosition.x, Is.Zero);
        }
        Assert.That(((RectTransform)menu.transform).anchoredPosition.x, Is.Zero);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void SunDucker_UsesDimensionalRedHairAndSimplifiedDetails()
    {
        GameObject root = new GameObject("Sun Ducker Test");
        try
        {
            SunDuckerDemonVisual.Build(root.transform);

            Transform details = root.transform.Find("DemonDetails");
            Transform hair = details.Find("Crimson 3D Storm Hair");
            Assert.That(hair, Is.Not.Null);

            Renderer lockRenderer = hair.Find("Center Fringe").GetComponent<Renderer>();
            Color hairColor = lockRenderer.sharedMaterial.color;
            Assert.That(hairColor.r, Is.GreaterThan(hairColor.g * 4f));
            Assert.That(hairColor.r, Is.GreaterThan(hairColor.b * 4f));
            Assert.That(lockRenderer.GetComponent<MeshFilter>().sharedMesh.bounds.size.z,
                Is.GreaterThan(0.05f));

            Transform clothes = details.Find("Storm Swordsman Clothing");
            Assert.That(clothes.Find("Teal Neck Cord"), Is.Null);
            Assert.That(clothes.Find("Teal Cord Drop Left"), Is.Null);
            Assert.That(clothes.Find("Teal Cord Drop Right"), Is.Null);
            Assert.That(clothes.Find("Left White Lapel"), Is.Null);
            Assert.That(clothes.Find("Right White Lapel"), Is.Null);

            Assert.That(details.Find("Left Black Lightning Tattoo"), Is.Not.Null);
            Assert.That(details.Find("Right Black Lightning Tattoo"), Is.Not.Null);
            Assert.That(details.Find("Forehead Black Mark"), Is.Null);
            Assert.That(details.Find("Left Lower Face Mark"), Is.Null);
            Assert.That(details.Find("Right Lower Face Mark"), Is.Null);
            Assert.That(details.Find("Left Brow Mark"), Is.Null);
            Assert.That(details.Find("Right Brow Mark"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
