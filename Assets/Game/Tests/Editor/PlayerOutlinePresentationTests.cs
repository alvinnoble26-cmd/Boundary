using NUnit.Framework;
using UnityEngine;

public sealed class PlayerOutlinePresentationTests
{
    [Test]
    public void VoidRevealOutlineExpandsAndContracts()
    {
        float peakTime = Mathf.PI / (2f * PlayerOutlinePresentation.VoidPulseFrequency);
        float troughTime = 3f * Mathf.PI / (2f * PlayerOutlinePresentation.VoidPulseFrequency);

        Assert.That(PlayerOutlinePresentation.VoidOutlineWidthAt(peakTime),
            Is.EqualTo(PlayerOutlinePresentation.VoidPulseMaximumWidth).Within(0.001f));
        Assert.That(PlayerOutlinePresentation.VoidOutlineWidthAt(troughTime),
            Is.EqualTo(PlayerOutlinePresentation.VoidPulseMinimumWidth).Within(0.001f));
    }
}
