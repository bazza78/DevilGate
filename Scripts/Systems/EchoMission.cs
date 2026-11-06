using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
namespace DevilGate.Scripts.Systems;
public static class EchoMission
{
    public const int GuardXp=15,CompletionXp=20,GoldReward=20;
    public const string Recording="MAINTENANCE LOG · ASHFALL RELAY\n\n'Filters intact. The fault is inside the ward, not the fan. Something is repeating through the ash.'\n\nStatic clears. A voice says: 'You left the light on. Come home.'\n\nYou remember those words from your other world. The recording was made here, before you arrived.";
    public static bool Available(RunState r)=>r.InductionStage==3 && r.WoodlandBattle.Stage==2 && r.LearnedSpellIds.Length==2;
    public static bool Accept(RunState r)
    {
        if(!Available(r)||r.Echo.Stage!=0||r.LocationId is not ("guild" or "safehouse"))return false;
        r.Echo.Stage=1;return true;
    }
    public static bool Inspect(RunState r)
    {
        if(r.LocationId!="grove"||r.Echo.Stage!=1)return false;
        r.Echo.Stage=2;return true;
    }
    public static DiceResult? AttemptMethod(RunState r,IDieSource? dice=null)
    {
        if(r.LocationId!="grove"||r.Echo.Stage!=2||r.Echo.MethodAttempted||r.FilterBattle.Stage!=0)return null;
        var tank=StarterTraining.IsTank(r);
        var roll=new DiceService(dice).Resolve(r.Character,new SkillCheckSpec(tank?AttributeKind.Might:AttributeKind.Willpower,tank?Ids.SkillAthletics:Ids.SkillArcana,12,tank?"Brace the relay casing":"Stabilise the relay ward"),GameCatalog.Instance);
        r.Echo.MethodAttempted=true;r.Echo.Stabilized=roll.Success;return roll;
    }
    public static bool HearRecording(RunState r)
    {
        if(r.LocationId!="grove"||r.Echo.Stage!=3)return false;
        r.Echo.RecordingHeard=true;return true;
    }
    public static bool Collect(RunState r)
    {
        if(r.LocationId!="grove"||r.Echo.Stage!=3||!r.Echo.RecordingHeard)return false;
        r.Echo.Stage=4;return true;
    }
    public static bool Complete(RunState r)
    {
        if(r.LocationId!="guild"||r.Echo.Stage!=4)return false;
        r.Echo.Stage=5;r.CarriedGold+=GoldReward;Progression.Award(r.Character,CompletionXp);return true;
    }
    public static string JournalLine(RunState r)=>r.Echo.Stage switch
    {
        1=>"Echo · Inspect grove relay (1/3)",
        2=>"Echo · Disable guardian (2/3)",
        3=>"Echo · Recover sample (2/3)",
        4=>"Echo · Return to Diane (3/3)",
        5=>"Echo in the Filters · Complete",
        _=>"Echo in the Filters · Available"
    };
    public static string Description(RunState r)=>r.Echo.Stage switch
    {
        0=>Available(r)?"ECHO IN THE FILTERS · Guild inspection\n\nThe Willow grove air-filter relay stopped transmitting before the woodland wisp appeared. Inspect the relay, recover a sealed filter sample and return it to Diane.\n\nReward: 20 gold · 35 XP total (15 for the guardian, 20 for the report). Accept here or through your home PC.":"ECHO IN THE FILTERS · Not yet available\n\nComplete guild induction, choose two skills and defeat the first woodland wisp.",
        1=>"ECHO IN THE FILTERS · 1/3 Inspect the relay\nTravel to Willow grove and inspect the air-filter relay beside the path.",
        2=>"ECHO IN THE FILTERS · 2/3 Disable the corrupted guardian\nThe relay is being held offline by magical interference. Optional class preparation can weaken its guardian; a manual reset is always available. A failed preparation roll never blocks the mission.",
        3=>"ECHO IN THE FILTERS · 2/3 Recover the sample\nThe guardian is disabled. Read the maintenance recording, then reset the relay and seal a sample of the luminous ash.",
        4=>"ECHO IN THE FILTERS · 3/3 Return to Diane\nThe relay is running. You carry the sealed filter sample as a key item (no backpack slot required). Return to guild reception for 20 gold and the remaining 20 XP.",
        _=>"ECHO IN THE FILTERS · Complete\nSample handed over. Total reward: 20 gold · 35 XP.\nASHFALL ARCHIVE · Unlocked\nThe relay's resonance matches the Arcanist's last survey and the Vanguard's evacuation route. Both records end at Transit Gate Seven. Someone altered their final timestamps. Diane asks you to keep the recording private.\nThis chapter is complete. The Gate Seven investigation will continue in a future update."
    };
    public static string? Validate(EchoQuestState? e,WoodlandBattleState? guard,int wispStage,int induction)
    {
        if(e==null||e.Stage<0||e.Stage>5)return "Invalid Echo mission state.";
        if(e.Stage>0&&(wispStage!=2||induction!=3))return "Echo mission requires completed induction and first wisp.";
        if(e.Stabilized&&!e.MethodAttempted || e.Stage<2&&(e.MethodAttempted||e.RecordingHeard) || e.Stage<3&&e.RecordingHeard)return "Invalid relay investigation state.";
        if(guard==null)return "Missing relay guardian state.";
        if(e.Stage<2&&guard.Stage!=0 || e.Stage>=3&&guard.Stage!=2 || e.Stage==2&&guard.Stage==2 || e.Stage>=4&&!e.RecordingHeard)return "Inconsistent relay guardian/sample state.";
        return null;
    }
}
