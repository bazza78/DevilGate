using DevilGate.Scripts.Data;
namespace DevilGate.Scripts.Systems;

/// <summary>First Connection: 0 available, 1 accepted, 2 contact met, 3 rewarded.</summary>
public static class InductionMission
{
    public const int GoldReward=15;
    public const int XpReward=25;
    public static bool Accept(RunState run)
    {
        if (run.CreationStage != 2 || run.InductionStage != 0) return false;
        run.InductionStage=1; return true;
    }
    public static bool MeetContact(RunState run)
    {
        if (run.LocationId != "guild" || run.CreationStage != 2 || run.InductionStage is not (0 or 1)) return false;
        run.InductionStage=2; return true;
    }
    public static bool Complete(RunState run)
    {
        if (run.LocationId != "safehouse" || run.InductionStage != 2) return false;
        run.InductionStage=3;
        run.CarriedGold+=GoldReward; Progression.Award(run.Character,XpReward); return true;
    }
    public static string Description(RunState run) => run.InductionStage switch
    {
        0 => "FIRST CONNECTION · Guild induction\nMeet Diane, the guild receptionist in the Adventurers’ Guild reception in town. Return here to register your arrival.\nReward: 15 gold · 25 XP. No combat.",
        1 => "FIRST CONNECTION · Active\nLeave through the apartment door and speak to Diane at guild reception in town.",
        2 => "FIRST CONNECTION · Ready to complete\nDiane has registered you and issued your guild card and drive. Activate your account here to receive 15 gold and 25 XP.",
        _ => "FIRST CONNECTION · Complete\nArrival registered. Reward received: 15 gold and 25 XP. For your next exercise, attune to the enchanted crystal on your potion counter and choose two class skills."
    };
}
