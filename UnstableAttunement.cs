public enum AttunmentElement { Burn, Frostbite, Decay, Shock }

public class UnstableAttunementstate
{
    public AttunmentElement Element;
    public int Value { get; set; }
    public int Max { get; set; } = 100;
    public int ChargeRate { get; set; } = 10;
    public int ChargeDelay { get; set; } = 10;
    public int BurnoutRate { get; set; } = 20;
    public int BurnoutDelay { get; set; } = 10;
    public bool BurnoutActive { get; set; }
}