namespace DevilGate.Scripts.Data;
public sealed class EchoQuestState
{
    // 0 available after first wisp; 1 accepted; 2 inspected; 3 guard defeated; 4 sample; 5 paid.
    public int Stage {get;set;}
    public bool MethodAttempted {get;set;}
    public bool Stabilized {get;set;}
    public bool RecordingHeard {get;set;}
    public EchoQuestState Clone()=>(EchoQuestState)MemberwiseClone();
}
