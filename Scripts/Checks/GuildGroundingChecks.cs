using DevilGate.Scripts.World;
using Godot;
namespace DevilGate.Scripts.Checks;

/// <summary>Ground pivots/contact shadows must correspond to solid pixels in the shipped atlas.</summary>
public partial class GuildGroundingChecks : Node
{
    public override void _Ready()
    {
        int failures=0;
        var texture=GD.Load<Texture2D>(GuildHallLayout.Atlas);
        using var atlas=texture.GetImage();
        foreach(var prop in GuildHallLayout.Furnishings)
        {
            var contact=GuildHallLayout.Contacts[prop.Cell];
            bool valid=contact.Pivot.X>=0&&contact.Pivot.X<512&&contact.Pivot.Y>0&&contact.Pivot.Y<=512&&contact.Feet.Length>=3;
            foreach(var foot in contact.Feet)
            {
                int x=prop.Cell%3*512+(int)foot.X,y=prop.Cell/3*512+(int)foot.Y;
                valid&=atlas.GetPixel(x,y).A>=.5f;
                // Contact should land at or behind the front ground anchor, never below the floor.
                valid&=GuildHallLayout.ContactOffset(prop,foot).Y<=0;
            }
            GD.Print($"GUILD GROUNDING {(valid?"PASS":"FAIL")}: {prop.Id} feet use opaque atlas pixels and stay grounded");
            if(!valid)failures++;
        }
        GD.Print($"GUILD GROUNDING CHECKS: {failures} failures");GetTree().Quit(failures==0?0:1);
    }
}
