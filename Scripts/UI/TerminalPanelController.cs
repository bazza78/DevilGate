using DevilGate.Scripts.Systems;
using Godot;
namespace DevilGate.Scripts;

/// <summary>Guild account activation and task rewards. No diagnostic minigame.</summary>
public partial class TerminalPanelController : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    public event Action? MissionBoardRequested;
    public event Action? EchoContractRequested;
    private Button _echoButton=null!;
    private Button _missionButton=null!;
    private Label _missionLabel=null!;
    public override void _Ready()
    {
        _missionButton=GetNode<Button>("%MissionBoardButton");
        _missionLabel=GetNode<Label>("%MissionLabel");
        GetNode<Button>("%CloseButton").Pressed+=()=>CloseRequested?.Invoke();
        _missionButton.Pressed+=OnMissionBoard;
        _echoButton=new Button {Text="Contracts · Echo in the Filters"};
        GetNode<VBoxContainer>("VBox").AddChild(_echoButton);
        _echoButton.Pressed+=()=>EchoContractRequested?.Invoke();
        GameSession.Instance!.StateChanged+=RefreshMission;
        RefreshMission();
    }
    private void RefreshMission()
    {
        var run=GameSession.Instance!.CurrentRun!;
        _missionLabel.Text=EchoMission.Available(run)?EchoMission.Description(run):InductionMission.Description(run);
        _echoButton.Visible=run.InductionStage==3;
        _echoButton.Text=run.Echo.Stage==0?"Contracts · Echo in the Filters":"Journal · Echo in the Filters";
        _missionButton.Text=run.InductionStage switch {0=>"Accept First Connection",1=>"Registration in progress",2=>"Activate account & claim allowance",_=>"Induction complete"};
        _missionButton.Disabled=run.InductionStage is 1 or 3;
    }
    public override void _ExitTree(){if(GameSession.Instance!=null)GameSession.Instance.StateChanged-=RefreshMission;}
    private void OnMissionBoard()
    {
        var run=GameSession.Instance!.CurrentRun!;
        bool changed=run.InductionStage==0 ? InductionMission.Accept(run) : InductionMission.Complete(run);
        if(changed)GameSession.Instance.EmitChanged();
        RefreshMission();MissionBoardRequested?.Invoke();
    }
}
