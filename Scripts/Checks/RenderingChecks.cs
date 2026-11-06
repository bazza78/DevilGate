using Godot;
using DevilGate.Scripts.World;
namespace DevilGate.Scripts.Checks;

/// <summary>Static custom draws must retain their textures across managed collection.</summary>
public partial class RenderingChecks : Node
{
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    public override async void _Ready()
    {
        int failed=0;
        try
        {
            foreach(var area in new[]{"safehouse","guild"})
            {
                var scene=new Node2D();AddChild(scene);
                if(area=="safehouse"){scene.AddChild(new FloorRenderer());scene.AddChild(new WallRenderer());}
                else scene.AddChild(new GuildHallRenderer());
                foreach(var p in area=="safehouse"?FurnitureLayout.All:GuildHallLayout.Furnishings)
                    scene.AddChild(new FurnitureSprite{Position=p.Anchor,Width=p.Width,Cell=p.Cell,AtlasPath=area=="guild"?GuildHallLayout.Atlas:"res://Assets/Environment/studio-furniture-v1.png"});
                await Frames(20);
                var before=GetViewport().GetTexture().GetImage();before.SavePng($"/tmp/devilgate-{area}-gc-before.png");
                for(int i=0;i<3;i++){GC.Collect();GC.WaitForPendingFinalizers();await Frames(10);}
                var after=GetViewport().GetTexture().GetImage();after.SavePng($"/tmp/devilgate-{area}-gc-after.png");
                int changed=0;
                for(int y=0;y<before.GetHeight();y+=2)for(int x=0;x<before.GetWidth();x+=2)
                {
                    var a=before.GetPixel(x,y);var b=after.GetPixel(x,y);
                    if(Mathf.Abs(a.R-b.R)+Mathf.Abs(a.G-b.G)+Mathf.Abs(a.B-b.B)>.1f)changed++;
                }
                bool ok=changed<50;GD.Print($"RENDER {(ok?"PASS":"FAIL")}: {area} stable after managed collection ({changed} changed samples)");if(!ok)failed++;
                scene.QueueFree();await Frames(5);
            }
        }
        catch(Exception e){GD.PrintErr(e);failed++;}
        GD.Print($"RENDER CHECKS: {failed} failures");GetTree().Quit(failed==0?0:1);
    }
}
