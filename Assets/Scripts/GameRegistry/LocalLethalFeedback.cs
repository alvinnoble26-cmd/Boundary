using UnityEngine;

/// <summary>
/// Local-only presentation for an already accepted lethal contact.
/// </summary>
public static class LocalLethalFeedback
{
    public static bool ShouldVibrate(bool acceptedLethalContact, bool isLocalOwner)
    {
        return acceptedLethalContact && isLocalOwner;
    }

    public static void VibrateForAcceptedLocalContact()
    {
#if UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
