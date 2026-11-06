using Godot;
using DevilGate.Scripts.Systems;
namespace DevilGate.Scripts;

public partial class InteractionController
{
    public void ShowEchoContract()
    {
        var run=GameSession.Instance!.CurrentRun!;
        var panel=CreatePanel("Echo in the Filters · Guild contract",EchoMission.Description(run));
        if(run.LocationId=="guild")panel.SetPortrait("res://Assets/Portraits/NPCs/diane.png");
        if(EchoMission.Available(run)&&run.Echo.Stage==0)
        {
            var accept=panel.AddButton("Accept Echo in the Filters",()=>
            {
                if(!EchoMission.Accept(run))return;
                GameSession.Instance!.EmitChanged();CloseTopPanel();ShowEchoContract();
            });
        }
        if(run.Echo.Stage==4&&run.LocationId=="guild")
            panel.AddButton("Hand sample to Diane",()=>
            {
                if(!EchoMission.Complete(run))return;
                GameSession.Instance!.EmitChanged();CloseTopPanel();ShowEchoContract();
            });
        OpenPanel(panel);
    }
    public void ShowFilterRelay()
    {
        var run=GameSession.Instance!.CurrentRun!;
        var text=run.Echo.Stage switch
        {
            0=>"WILLOW GROVE · Air-filter relay\n\nThe guild service hatch is locked. Accept Echo in the Filters from Diane or your home PC after dealing with the woodland wisp.",
            1=>"The air-filter relay hums unevenly. Luminous ash has collected inside an intact filter. A corrupted maintenance guardian blocks the service hatch.\n\nInspect the relay to begin the repair.",
            2=>"The fan is intact: magical interference has locked the controls. A corrupted guardian is keeping it offline.\n\nYou may prepare the relay using your class training (one optional roll), or use the manual reset. Neither choice needs a particular equipped skill. The guardian must be disabled before the hatch can open.",
            3=>"The guardian is disabled. The service hatch is open. A maintenance recording is still playing inside the relay, beside an intact filter filled with luminous ash.",
            _=>"The air-filter relay runs steadily again. The sample has already been sealed and removed.\n\n"+EchoMission.Description(run)
        };
        if(run.Echo.Stage==2&&run.Echo.MethodAttempted)
            text+=run.Echo.Stabilized?"\n\nPreparation successful: guardian attack −2 for this encounter.":"\n\nPreparation failed: the guardian is alerted (+1 attack). You can still use the manual reset and finish the mission.";
        var panel=CreatePanel("Willow grove · Air-filter relay",text);
        void Reopen(){GameSession.Instance!.EmitChanged();CloseTopPanel();ShowFilterRelay();}
        if(run.Echo.Stage==1)panel.AddButton("Inspect the relay",()=>{if(EchoMission.Inspect(run))Reopen();});
        if(run.Echo.Stage==2)
        {
            if(!run.Echo.MethodAttempted&&run.FilterBattle.Stage==0)
                panel.AddButton(StarterTraining.IsTank(run)?"Brace the casing · Might check":"Stabilise the ward · Arcana check",()=>
                {
                    var result=EchoMission.AttemptMethod(run);
                    if(result==null)return;
                    GameSession.Instance!.EmitChanged();CloseTopPanel();ShowFilterRelay();
                    if(TopPanel is ModalPanelController updated)updated.SetBody(result.Summary+"\n\n"+(result.Success?"The guardian's attack is weakened by 2. Use the manual reset when ready.":"The guardian is alerted (+1 attack). The manual reset is still available; the mission is not blocked."));
                });
            panel.AddButton("Manual reset · Face guardian",()=>
            {
                if(!WoodlandBattle.Start(run))return;
                GameSession.Instance!.EmitChanged();CloseTopPanel();ShowWoodlandBattle();
            });
        }
        if(run.Echo.Stage==3)
        {
            panel.AddButton("Read the maintenance recording",()=>
            {
                if(!EchoMission.HearRecording(run))return;
                GameSession.Instance!.EmitChanged();CloseTopPanel();ShowFilterRelay();
                if(TopPanel is ModalPanelController updated)updated.SetBody(EchoMission.Recording+"\n\nReset the relay and seal the filter sample when ready.");
            });
            if(run.Echo.RecordingHeard)panel.AddButton("Reset relay & seal sample",()=>{if(EchoMission.Collect(run))Reopen();});
        }
        OpenPanel(panel);
    }
}
