#if UNITY_EDITOR
using NUnit.Framework;

public sealed class LocalLethalFeedbackTests
{
    [Test]
    public void AcceptedLocalContact_IsTheOnlyFeedbackEligibleCase()
    {
        Assert.That(LocalLethalFeedback.ShouldVibrate(true, true), Is.True);
        Assert.That(LocalLethalFeedback.ShouldVibrate(false, true), Is.False);
        Assert.That(LocalLethalFeedback.ShouldVibrate(true, false), Is.False);
        Assert.That(LocalLethalFeedback.ShouldVibrate(false, false), Is.False);
    }
}
#endif
