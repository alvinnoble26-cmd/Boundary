using NUnit.Framework;

public sealed class SettingsMenuTests
{
    [Test]
    public void VolumePercentConvertsToMixerDecibels()
    {
        Assert.AreEqual(-80f, SettingsMenu.VolumePercentToDecibels(0f), 0.001f);
        Assert.AreEqual(-6.0206f, SettingsMenu.VolumePercentToDecibels(50f), 0.001f);
        Assert.AreEqual(0f, SettingsMenu.VolumePercentToDecibels(100f), 0.001f);
    }

    [Test]
    public void VolumePercentIsClampedToSupportedRange()
    {
        Assert.AreEqual(-80f, SettingsMenu.VolumePercentToDecibels(-1f), 0.001f);
        Assert.AreEqual(0f, SettingsMenu.VolumePercentToDecibels(101f), 0.001f);
    }
}
