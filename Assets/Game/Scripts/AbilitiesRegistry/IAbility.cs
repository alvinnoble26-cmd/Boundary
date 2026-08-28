public interface IAbility
{
    AbilityId Id {get;}
    float CooldownDuration { get; }
    void Activate();
}
