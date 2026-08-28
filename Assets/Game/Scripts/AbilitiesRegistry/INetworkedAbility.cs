public interface INetworkedAbility
{
    // Called on owner client to get data needed for server execution
    // Returns false if ability can't activate (cooldown, wrong state, etc.)
    bool CanActivate();
    void ActivateLocal();  // Visual/audio feedback on owner immediately
}