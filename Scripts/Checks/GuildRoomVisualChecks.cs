using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
using Godot;
namespace DevilGate.Scripts.Checks;

/// <summary>Real-renderer guild review, including the complete sidebar at normal/enlarged text.</summary>
public partial class GuildRoomVisualChecks : Node
{
    public override async void _Ready()
    {
        int failed=0;
        try
        {
            ComfortSettings.TextScale=1;
            var run=KnightChecks.NewKnight();run.LocationId="guild";run.InductionStage=1;run.MagicAwakened=false;run.LearnedSpellIds=Array.Empty<string>();
            run.PositionX=CourtyardRenderer.Spawn.X;run.PositionY=CourtyardRenderer.Spawn.Y;
            InventoryService.TakeAll(run,GameCatalog.Instance);
            for(int i=0;i<12;i++)if(run.Backpack[i] is {} item&&GameCatalog.Instance.GetItem(item.ItemId).Category is ItemCategory.Weapon or ItemCategory.Armour)InventoryService.EquipFromBackpack(run,i,GameCatalog.Instance);
            GameSession.Instance!.SetCurrentRunForTesting(run);
            var room=GD.Load<PackedScene>("res://Scenes/Safehouse.tscn").Instantiate<SafehouseController>();AddChild(room);
            await ToSignal(GetTree().CreateTimer(2.1,false),SceneTreeTimer.SignalName.Timeout);
            foreach(var width in new[]{1280,960})
            {
                GetWindow().Size=new Vector2I(width,width==1280?800:600);ComfortSettings.TextScale=width==1280?1:1.2f;
                await ToSignal(GetTree().CreateTimer(.4,false),SceneTreeTimer.SignalName.Timeout);
                if(DisplayServer.GetName()!="headless")
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"/tmp/devilgate-guild-{width}.png");
                }
                var props=room.GetChildren().OfType<FurnitureSprite>().ToArray();
                bool correct=props.Length==6&&props.All(p=>p.IsVisibleInTree()&&GetViewport().GetVisibleRect().HasPoint(p.GlobalPosition))&&run.LocationId=="guild";
                GD.Print($"GUILD ROOM VISUAL {(correct?"PASS":"FAIL")}: all six props visible at {width}, text={ComfortSettings.TextScale}");if(!correct)failed++;
            }
            room.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GC.Collect();GC.WaitForPendingFinalizers();
        }
        catch(Exception ex){GD.PrintErr(ex);failed++;}
        finally{ComfortSettings.TextScale=1;}
        GD.Print($"GUILD ROOM VISUAL CHECKS: {failed} failures");GetTree().Quit(failed==0?0:1);
    }
}
