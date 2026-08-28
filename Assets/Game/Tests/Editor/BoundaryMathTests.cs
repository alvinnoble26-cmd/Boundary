#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;

public sealed class BoundaryMathTests
{
    [Test]
    public void PlayerAbilities_ReconstructsActivePresentationsForNewObservers()
    {
        MethodInfo observerHook = typeof(PlayerAbilities)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "OnObserverAdded" &&
                method.DeclaringType == typeof(PlayerAbilities) &&
                method.GetParameters().Length == 1);
        MethodInfo reconstructionRpc = typeof(PlayerAbilities)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(method => method.Name == "ReconstructAbilityPresentation");

        Assert.That(observerHook, Is.Not.Null);
        Assert.That(observerHook.DeclaringType, Is.EqualTo(typeof(PlayerAbilities)));
        Assert.That(reconstructionRpc, Is.Not.Null);
        Assert.That(reconstructionRpc.GetCustomAttributes(typeof(TargetRpcAttribute), false),
            Has.Length.EqualTo(1));
    }

    [Test]
    public void AbilityInformationPanel_IsEditorAuthoredWithEveryAbilityAndNavigation()
    {
        GameObject canvasObject = new GameObject("Ability Guide Test Canvas", typeof(Canvas));
        GameObject menuObject = new GameObject("AbilitiesMenu", typeof(RectTransform));
        menuObject.transform.SetParent(canvasObject.transform, false);
        GameObject backObject = new GameObject("BackButton (3)", typeof(RectTransform), typeof(Image), typeof(Button));
        backObject.transform.SetParent(menuObject.transform, false);
        RectTransform backRect = (RectTransform)backObject.transform;
        backRect.anchoredPosition = new Vector2(1.5f, -201f);
        backRect.sizeDelta = new Vector2(88f, 19.07f);
        backRect.localScale = new Vector3(0.128f, 2.56f, 0.02f);
        GameObject backTextObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        backTextObject.transform.SetParent(backObject.transform, false);
        TMP_Text backText = backTextObject.GetComponent<TMP_Text>();
        backText.text = "BACK";
        backText.fontSize = 30f;
        backText.fontStyle = FontStyles.Bold;
        backText.color = new Color(0.9f, 0.3f, 0.1f, 1f);
        try
        {
            AbilityInformationUI information = menuObject.AddComponent<AbilityInformationUI>();
            information.EnsureBuilt();

            Transform button = menuObject.transform.Find(AbilityInformationUI.InformationButtonName);
            Transform panel = canvasObject.transform.Find(AbilityInformationUI.InformationPanelName);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.GetComponent<Button>(), Is.Not.Null);
            Button informationButton = button.GetComponent<Button>();
            RectTransform informationRect = (RectTransform)button;
            Assert.That(informationRect.anchoredPosition.y, Is.EqualTo(backRect.anchoredPosition.y));
            Assert.That(informationRect.anchoredPosition.x, Is.GreaterThan(backRect.anchoredPosition.x));
            Assert.That(button.GetComponent<Image>().enabled, Is.True);
            Assert.That(button.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            TMP_Text informationText = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(informationButton.targetGraphic, Is.SameAs(informationText));
            Assert.That(informationText.font, Is.SameAs(backText.font));
            Assert.That(informationText.fontSize, Is.EqualTo(backText.fontSize));
            Assert.That(informationText.color, Is.EqualTo(backText.color));
            Assert.That(informationButton.colors.pressedColor, Is.Not.EqualTo(Color.white));
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Image>().color, Is.EqualTo(Color.black));
            Assert.That(panel.Find("Header/Back Button")?.GetComponent<Button>(), Is.Not.Null);
            Transform viewport = panel.Find("Ability Guide Viewport");
            Assert.That(viewport?.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(viewport?.GetComponent<Mask>(), Is.Null);
            Assert.That(panel.GetComponentsInChildren<TMP_Text>(true)
                .Count(text => text.name == "Description"), Is.EqualTo(11));

            information.ShowInformation();
            Assert.That(panel.gameObject.activeSelf, Is.True);
            information.HideInformation();
            Assert.That(panel.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void GrappleTraversal_PreservesAndBuildsSpeedWhileRedirectingTowardAnchor()
    {
        Vector3 velocity = GrappleAbility.CalculateTraversalVelocity(
            Vector3.right * 45f,
            Vector3.forward,
            0.5f);

        Assert.That(velocity.magnitude, Is.EqualTo(92.5f).Within(0.001f));
        Assert.That(Vector3.Angle(velocity, Vector3.forward), Is.LessThan(0.01f));
    }

    [Test]
    public void GrappleTraversal_CapsSpeedAndLaunchesFromRest()
    {
        Vector3 cappedVelocity = GrappleAbility.CalculateTraversalVelocity(
            Vector3.forward * 94f,
            Vector3.forward,
            1f);
        Vector3 launchVelocity = GrappleAbility.CalculateTraversalVelocity(
            Vector3.zero,
            Vector3.up,
            Time.fixedDeltaTime);

        Assert.That(cappedVelocity.magnitude, Is.EqualTo(GrappleAbility.MaximumTraversalSpeed).Within(0.001f));
        Assert.That(launchVelocity.magnitude, Is.GreaterThan(GrappleAbility.MinimumTraversalSpeed));
        Assert.That(Vector3.Angle(launchVelocity, Vector3.up), Is.LessThan(0.01f));
    }

    [Test]
    public void GrappleTimeout_EndsAtFourSeconds()
    {
        Assert.That(GrappleAbility.HasTimedOut(10f, 13.99f), Is.False);
        Assert.That(GrappleAbility.HasTimedOut(10f, 14f), Is.True);
    }

    [Test]
    public void GrappleArrivalTolerance_ReleasesFourUnitsFromAnchor()
    {
        Assert.That(GrappleAbility.ReleaseDistance, Is.EqualTo(4f).Within(0.001f));
    }

    [Test]
    public void GrappleServerFacingValidation_AllowsLatencyToleranceButRejectsRearAim()
    {
        Vector3 toleranceEdge = Quaternion.Euler(0f, GrappleAbility.MaximumServerFacingAngle, 0f) *
            Vector3.forward;

        Assert.That(GrappleAbility.IsAimWithinServerFacing(Vector3.forward, toleranceEdge), Is.True);
        Assert.That(GrappleAbility.IsAimWithinServerFacing(Vector3.forward, Vector3.back), Is.False);
        Assert.That(GrappleAbility.IsAimWithinServerFacing(Vector3.forward, Vector3.up), Is.True);
    }

    [Test]
    public void AbilityIds_RemainAppendOnlyForReleasedClientCompatibility()
    {
        Assert.That((int)AbilityId.Teleport, Is.EqualTo(0));
        Assert.That((int)AbilityId.Slide, Is.EqualTo(1));
        Assert.That((int)AbilityId.Dash, Is.EqualTo(2));
        Assert.That((int)AbilityId.BlackThrow, Is.EqualTo(3));
        Assert.That((int)AbilityId.AttractThrow, Is.EqualTo(4));
        Assert.That((int)AbilityId.RepelThrow, Is.EqualTo(5));
        Assert.That((int)AbilityId.Grapple, Is.EqualTo(6));
        Assert.That((int)AbilityId.Hollow, Is.EqualTo(7));
        Assert.That((int)AbilityId.Void, Is.EqualTo(8));
        Assert.That((int)AbilityId.Bullseye, Is.EqualTo(9));
        Assert.That((int)AbilityId.Charge, Is.EqualTo(10));
        Assert.That((int)AbilityId.Slice, Is.EqualTo(11));
    }

    [Test]
    public void BlackHoleContact_DealsSixtyDamagePerSecond()
    {
        Assert.That(BoundaryMath.BlackHoleDamage(1f), Is.EqualTo(60f).Within(0.001f));
        Assert.That(BoundaryMath.BlackHoleDamage(0.1f), Is.EqualTo(6f).Within(0.001f));
    }

    [Test]
    public void SlideAbility_IsDisabledInLoadouts()
    {
        Assert.That(LoadoutManager.IsAbilityEnabled(AbilityId.Slide), Is.False);
        Assert.That(LoadoutManager.IsAbilityEnabled(AbilityId.Dash), Is.True);
    }

    [Test]
    public void BlackHoleDarknessField_UsesThirtyUnitVisualRadiusWithoutFullBlackout()
    {
        Assert.That(BlackHoleKill.DarknessRadius, Is.EqualTo(21f));
        Assert.That(BlackHoleKill.DarknessExposureStops, Is.EqualTo(-3.3f));
    }

    [Test]
    public void Damage_ClampsHealthBetweenZeroAndMaximum()
    {
        Assert.That(BoundaryMath.ApplyDamage(100f, 30f), Is.EqualTo(70f).Within(0.001f));
        Assert.That(BoundaryMath.ApplyDamage(10f, 30f), Is.Zero);
        Assert.That(BoundaryMath.ApplyDamage(100f, -10f), Is.EqualTo(100f));
    }

    [Test]
    public void OutOfBoundsMargin_TracksShrinkingRingPhase()
    {
        Assert.That(BoundaryMath.OutOfBoundsMargin(BoundaryPhase.OuterRing), Is.EqualTo(5f));
        Assert.That(BoundaryMath.OutOfBoundsMargin(BoundaryPhase.MiddleRing), Is.EqualTo(3f));
        Assert.That(BoundaryMath.OutOfBoundsMargin(BoundaryPhase.InnerRing), Is.EqualTo(2f));
    }

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
    public void ArenaMassPopulation_IncludesRequestedFloorAndFloatingHazards()
    {
        Assert.That(BoundaryMatchController.GroundArenaMassesPerKind, Is.EqualTo(22));
        Assert.That(BoundaryMatchController.FloatingArenaMassesPerKind, Is.EqualTo(18));
        Assert.That(BoundaryMatchController.ArenaMassPopulation, Is.EqualTo(80));
        Assert.That(BoundaryMatchController.IsArenaBlackHole(21), Is.False);
        Assert.That(BoundaryMatchController.IsArenaBlackHole(22), Is.True);
        Assert.That(BoundaryMatchController.IsFloatingArenaMass(43), Is.False);
        Assert.That(BoundaryMatchController.IsFloatingArenaMass(44), Is.True);
        Assert.That(BoundaryMatchController.IsArenaBlackHole(62), Is.True);
        Assert.That(BoundaryMatchController.PlatformHitsToCollapse, Is.EqualTo(5));
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
    public void BlackCubesAndBlackHolesUseHealthDamageInsteadOfInstantDeath()
    {
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.Cube, true), Is.False);
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, true), Is.False);
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, false), Is.False);
    }

    [Test]
    public void ArenaMasses_LeaveHalfOfEachKindAfterInitialBoundaryCollapse()
    {
        int cubeSurvivors = 0;
        int blackHoleSurvivors = 0;
        for (int variant = 0; variant < 10; variant++)
        {
            if (BoundaryMath.SurvivesInitialBoundaryCollapse(variant))
                cubeSurvivors++;
        }
        for (int variant = 10; variant < 20; variant++)
        {
            if (BoundaryMath.SurvivesInitialBoundaryCollapse(variant))
                blackHoleSurvivors++;
        }

        Assert.That(cubeSurvivors, Is.EqualTo(5));
        Assert.That(blackHoleSurvivors, Is.EqualTo(5));
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
    public void ExposedPlatformSides_AreWallJumpableButFloorTopsAndHazardsAreNot()
    {
        GameObject platformRoot = new GameObject("Breakaway Platforms");
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject player = new GameObject("Player Root");
        try
        {
            platform.transform.SetParent(platformRoot.transform, false);
            Collider platformCollider = platform.GetComponent<Collider>();
            Collider hazardCollider = hazard.GetComponent<Collider>();

            Assert.That(PlayerMovement.IsWallJumpSurface(
                platformCollider, Vector3.right, player.transform), Is.True);
            Assert.That(PlayerMovement.IsWallJumpSurface(
                platformCollider, Vector3.up, player.transform), Is.False,
                "Standing on a floor top must remain a normal grounded state.");
            Assert.That(PlayerMovement.IsWallJumpSurface(
                hazardCollider, Vector3.right, player.transform), Is.False,
                "Unrelated cubes and hazards must not become wall-jump surfaces.");
        }
        finally
        {
            Object.DestroyImmediate(platformRoot);
            Object.DestroyImmediate(hazard);
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void SlideWallTangent_StaysHorizontalAndPreservesIncomingTravel()
    {
        Vector3 vertical = SlideAbility.SelectHorizontalWallTangent(Vector3.forward, Vector3.right);
        Vector3 tilted = SlideAbility.SelectHorizontalWallTangent(
            new Vector3(1f, 0f, 1f), new Vector3(0.6f, 0.5f, 0f));

        Assert.That(vertical.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Dot(vertical, Vector3.right), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Dot(vertical, Vector3.forward), Is.GreaterThan(0f));
        Assert.That(tilted.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Dot(tilted, new Vector3(0.6f, 0f, 0f)), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void SlideActivation_RequiresFloorOrWallSupport()
    {
        Assert.That(SlideAbility.HasValidActivationSupport(false, false), Is.False);
        Assert.That(SlideAbility.HasValidActivationSupport(true, false), Is.True);
        Assert.That(SlideAbility.HasValidActivationSupport(false, true), Is.True);
    }

    [Test]
    public void SlideWallEligibility_RejectsDynamicObjectsAndAllowsIntendedPlatformSides()
    {
        GameObject player = new GameObject("Player Root");
        GameObject platformRoot = new GameObject("Breakaway Platforms");
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject dynamicObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            platform.transform.SetParent(platformRoot.transform, false);
            dynamicObject.AddComponent<Rigidbody>();

            Assert.That(PlayerMovement.IsSlideWallSurface(
                platform.GetComponent<Collider>(), Vector3.right, player.transform), Is.True);
            Assert.That(PlayerMovement.IsSlideWallSurface(
                platform.GetComponent<Collider>(), Vector3.up, player.transform), Is.False);
            Assert.That(PlayerMovement.IsSlideWallSurface(
                dynamicObject.GetComponent<Collider>(), Vector3.right, player.transform), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(platformRoot);
            Object.DestroyImmediate(dynamicObject);
        }
    }

    [Test]
    public void SlideJump_UsesTwiceNormalUpwardImpulse()
    {
        float normalImpulse = 7f;
        float slideImpulse = SlideAbility.SlideJumpUpwardImpulse(normalImpulse);

        Assert.That(slideImpulse,
            Is.EqualTo(normalImpulse * SlideAbility.SlideJumpImpulseMultiplier).Within(0.0001f));
        Assert.That(SlideAbility.SlideJumpHeightMultiplier, Is.EqualTo(4f));
    }

    [Test]
    public void SlideJump_PreservesIncomingHorizontalVelocity()
    {
        Vector3 incoming = new Vector3(42f, -3f, -17f);
        Vector3 result = SlideAbility.CalculateSlideJumpVelocity(
            incoming, 7f, Vector3.zero);

        Assert.That(result.x, Is.EqualTo(incoming.x).Within(0.0001f));
        Assert.That(result.z, Is.EqualTo(incoming.z).Within(0.0001f));
        Assert.That(result.y, Is.EqualTo(14f).Within(0.0001f));
    }

    [Test]
    public void SlideJumpAllowance_PreventsBoundaryCapFromShorteningAscent()
    {
        Assert.That(PlayerMovement.ResolveVerticalSpeedCap(22f, 14f),
            Is.EqualTo(22f));
        Assert.That(PlayerMovement.ResolveVerticalSpeedCap(22f, 28f),
            Is.EqualTo(28f).Within(0.0001f));
    }

    [Test]
    public void SlideSpeed_DoesNotApplyAnExtraMultiplier()
    {
        Assert.That(SlideAbility.IncreasedSlideSpeed(80f), Is.EqualTo(80f).Within(0.0001f));
    }

    [Test]
    public void SlideObstacleCollision_RemovesIntoWallVelocityWithoutRandomBounce()
    {
        Vector3 headOn = SlideAbility.ResolveObstacleCollisionVelocity(
            new Vector3(-3f, 2f, 11f), Vector3.forward, Vector3.back, 12f);
        Vector3 glancing = SlideAbility.ResolveObstacleCollisionVelocity(
            new Vector3(8f, -1f, 8f), new Vector3(1f, 0f, 1f), Vector3.back, 12f);

        Assert.That(new Vector3(headOn.x, 0f, headOn.z), Is.EqualTo(Vector3.zero));
        Assert.That(headOn.y, Is.EqualTo(2f));
        Assert.That(Vector3.Dot(glancing, Vector3.back), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(glancing.x, Is.GreaterThan(0f));
        Assert.That(glancing.y, Is.EqualTo(-1f));
    }

    [Test]
    public void SlideJump_EndsActiveSlideSteering()
    {
        Assert.That(SlideAbility.ShouldEndSlideAfterJump(false), Is.True);
        Assert.That(SlideAbility.ShouldEndSlideAfterJump(true), Is.True);
    }

    [Test]
    public void SlideContinuesAfterLosingFloorOrWallSupport()
    {
        Assert.That(SlideAbility.ShouldEndSlideAfterSupportLoss(), Is.False);
    }

    [Test]
    public void PlayerWindIntensity_UsesActualSpeedThresholdsAndClamps()
    {
        Assert.That(PlayerWindPresentation.WorldWindIntensity(1.2f, 1.2f, 7f), Is.Zero);
        Assert.That(PlayerWindPresentation.WorldWindIntensity(4.1f, 1.2f, 7f),
            Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(PlayerWindPresentation.WorldWindIntensity(20f, 1.2f, 7f), Is.EqualTo(1f));

        Assert.That(PlayerWindPresentation.HighSpeedIntensity(7f, 7f, 24f), Is.Zero);
        Assert.That(PlayerWindPresentation.HighSpeedIntensity(15.5f, 7f, 24f),
            Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(PlayerWindPresentation.HighSpeedIntensity(30f, 7f, 24f), Is.EqualTo(1f));
    }

    [Test]
    public void GameExitButton_ArmsOnlyAfterAnInsideTapAndRequiresTheFullHold()
    {
        Assert.That(GameExitButton.ShouldArmAfterTap(true), Is.True);
        Assert.That(GameExitButton.ShouldArmAfterTap(false), Is.False);
        Assert.That(GameExitButton.HasCompletedHold(1.499f), Is.False);
        Assert.That(GameExitButton.HasCompletedHold(GameExitButton.HoldDuration), Is.True);
    }

    [Test]
    public void GeneratedWalls_AreLargerAndMoreFrequentlyElevated()
    {
        Assert.That(BoundaryArenaPresentation.GeneratedWallSizeMultiplier, Is.EqualTo(1.12f));
        Assert.That(BoundaryArenaPresentation.ScaleGeneratedWallDimension(10f),
            Is.EqualTo(11.2f).Within(0.001f));
        Assert.That(BoundaryArenaPresentation.GeneratedWallExtraHeight(4.5f, 0.75f),
            Is.GreaterThan(4.4f));
        Assert.That(BoundaryArenaPresentation.GeneratedWallExtraHeight(4.5f, 1f),
            Is.EqualTo(6.075f).Within(0.001f));
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
    public void HeadlessArenaBuild_CreatesCollidersWithoutRenderers()
    {
        GameObject root = new GameObject("Headless Arena Validation");
        try
        {
            BoundaryMatchController controller = root.AddComponent<BoundaryMatchController>();
            BoundaryArenaPresentation presentation = root.AddComponent<BoundaryArenaPresentation>();

            presentation.BuildPhysicsOnlyArenaForValidation(controller);

            Assert.That(presentation.GeneratedPlatformCount, Is.GreaterThan(0));
            Assert.That(presentation.GeneratedPlatformColliderCount,
                Is.EqualTo(presentation.GeneratedPlatformCount));
            Assert.That(root.GetComponentsInChildren<BoxCollider>(true).Length,
                Is.EqualTo(presentation.GeneratedPlatformCount));
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty,
                "Dedicated-server floor construction must not require render components.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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

            Assert.That(canvasObject.GetComponent<GraphicRaycaster>(), Is.Not.Null,
                "The generated controls editor canvas must participate in UI raycasts.");
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
    public void SavedControlLayout_CanBeReappliedAfterButtonsMoveToCanvas()
    {
        GameObject canvasObject = new GameObject("Saved Controls Canvas", typeof(Canvas));
        try
        {
            CreateRect("Image", canvasObject.transform);
            RectTransform buttonRoot = CreateRect("ButtonBR", canvasObject.transform);
            CreateRect("Button", buttonRoot);
            CreateRect("A1", buttonRoot);
            CreateRect("A2", buttonRoot);
            CreateRect("A3", buttonRoot);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            ControlLayoutSettings.ApplyToGameCanvas(canvas);
            ControlLayoutSettings.ApplyToGameCanvas(canvas);

            ControlLayoutSettings.ControlEntry savedA1 = ControlLayoutSettings.Load().Find("A1");
            RectTransform appliedA1 = canvas.transform.Find("A1") as RectTransform;
            Assert.That(appliedA1, Is.Not.Null);
            Assert.That(appliedA1.anchorMin.x, Is.EqualTo(savedA1.x).Within(0.0001f));
            Assert.That(appliedA1.anchorMin.y, Is.EqualTo(savedA1.y).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return (RectTransform)gameObject.transform;
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
            GameObject cameraObject = new GameObject("Owner Camera", typeof(Camera));
            cameraObject.transform.SetParent(player.transform, false);
            cameraController.cam = cameraObject.transform;
            int originalBodyLayer = bodyRenderer.gameObject.layer;
            ShadowCastingMode originalBodyMode = bodyRenderer.shadowCastingMode;

            cameraController.SetLocalVisualVisibility(true);
            Assert.That(bodyRenderer.forceRenderingOff, Is.False,
                "The Practice owner body must stay enabled.");
            Assert.That(bodyRenderer.gameObject.layer, Is.EqualTo(originalBodyLayer),
                "Camera presentation must not alter player collision layers.");
            LocalPlayerCameraBodyFilter bodyFilter =
                cameraObject.GetComponent<LocalPlayerCameraBodyFilter>();
            Assert.That(bodyFilter, Is.Not.Null);
            Assert.That(bodyRenderer.shadowCastingMode, Is.EqualTo(originalBodyMode),
                "Scene view must see the normal player body outside owner-camera rendering.");

            bodyFilter.ApplyOwnerCameraFilter();
            Assert.That(bodyRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly),
                "Only the owning first-person camera must hide the local body.");
            bodyFilter.RestoreOwnerCameraFilter();
            Assert.That(bodyRenderer.shadowCastingMode, Is.EqualTo(originalBodyMode));
            Assert.That(eyeRenderer.forceRenderingOff, Is.True);

            cameraController.SetLocalVisualVisibility(false);
            Assert.That(bodyRenderer.forceRenderingOff, Is.False);
            Assert.That(bodyRenderer.gameObject.layer, Is.EqualTo(originalBodyLayer));
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
    public void AbilityTouchTransfer_ArmsAndActivatesOnlyOnMatchingRelease()
    {
        GameObject buttonObject = new GameObject("Transfer Ability", typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(AbilityTouchTransferTarget));
        try
        {
            int pressCount = 0;
            int releaseCount = 0;
            int cancelCount = 0;
            AbilityTouchTransferTarget target = buttonObject.GetComponent<AbilityTouchTransferTarget>();
            target.Configure(() => releaseCount++, () => pressCount++, () => cancelCount++);

            Assert.That(target.BeginTransferredTouch(7), Is.True);
            Assert.That(pressCount, Is.EqualTo(1));
            target.ReleaseTransferredTouch(8);
            Assert.That(releaseCount, Is.Zero, "Another finger must not activate the ability.");
            target.ReleaseTransferredTouch(7);
            Assert.That(releaseCount, Is.EqualTo(1));

            Assert.That(target.BeginTransferredTouch(9), Is.True);
            target.CancelTransferredTouch(9);
            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(releaseCount, Is.EqualTo(1), "Leaving an ability must not activate it.");
        }
        finally
        {
            Object.DestroyImmediate(buttonObject);
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
