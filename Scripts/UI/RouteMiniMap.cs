using DevilGate.Scripts.Data;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
using Godot;
namespace DevilGate.Scripts;

/// <summary>Authored floor geometry and landmarks, projected consistently in compact and expanded maps.</summary>
public partial class RouteMiniMap : Control
{
    public event Action? OpenRequested;
    public readonly record struct Landmark(string Id,string Name,Vector2 Point);
    private readonly StyleBoxFlat _background=new() {BgColor=new Color("14232ff2"),BorderColor=new Color("385660"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=7,CornerRadiusTopRight=7,CornerRadiusBottomLeft=7,CornerRadiusBottomRight=7};
    public override void _Ready()
    {
        Position=new Vector2(20,18);Size=new Vector2(195,168);MouseFilter=MouseFilterEnum.Stop;
        TooltipText="Area map · Click or press M to expand. Shows your position, landmarks and current objective.";
    }
    public override void _Process(double delta)=>QueueRedraw();
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:true}) {OpenRequested?.Invoke();AcceptEvent();}
    }
    public static Landmark[] Landmarks(string area,RunState run)
    {
        var route=new List<Landmark>();
        if(area=="safehouse")
            return new[]{new Landmark("identity_mirror","Mirror",SafehouseLayout.Iso(16,3.5f)),new Landmark("training_rack","Equipment",SafehouseLayout.Iso(9,3)),new Landmark("training_crystal","Training crystal",SafehouseLayout.PotionFocus),new Landmark("starter_chest","Chest",SafehouseLayout.ChestFocus),new Landmark("mission_pc","Terminal",SafehouseLayout.TableFocus),new Landmark("outside_door","Woodland exit",SafehouseLayout.DoorApproach)};
        int index=WorldRoute.Index(area);
        if(index>0)route.Add(new("route_back","To "+WorldRoute.Name(WorldRoute.Areas[index-1]),CourtyardRenderer.Spawn));
        if(index>=0 && index<WorldRoute.Areas.Length-1)route.Add(new("route_forward","To "+WorldRoute.Name(WorldRoute.Areas[index+1]),CourtyardRenderer.ContactApproach));
        if(area=="guild") {route.Add(new("guild_contact","Diane",CourtyardRenderer.Contact));route.Add(new("guild_noticeboard","Contracts",GuildHallLayout.Contracts.Anchor));}
        if(area=="grove")route.Add(new("filter_relay",run.Echo.Stage>=4?"Restored relay":"Filter relay",FilterRelay.Anchor));
        if(area=="woodland" && WoodlandBattle.Unlocked(run) && run.WoodlandBattle.Stage<2)route.Add(new("wisp","Rift disturbance",RiftWisp.EncounterPoint));
        return route.ToArray();
    }
    public static Vector2 Project(Vector2 world,string area,Rect2 rect)
    {
        var bounds=area=="safehouse"?SafehouseLayout.PlayableBounds:CourtyardRenderer.Bounds;
        float scale=Mathf.Min(rect.Size.X/bounds.Size.X,rect.Size.Y/bounds.Size.Y);
        var point=rect.Position+(rect.Size-bounds.Size*scale)*.5f+(world-bounds.Position)*scale;
        return point.Clamp(rect.Position+Vector2.One*3,rect.End-Vector2.One*3);
    }
    public static Vector2 LivePosition(Node owner,RunState run)=>owner.GetTree().GetNodesInGroup("route_player").OfType<Node2D>().LastOrDefault()?.GlobalPosition ?? new Vector2(run.PositionX,run.PositionY);
    public static void DrawArea(CanvasItem canvas,RunState run,string area,Rect2 rect,Vector2? player,bool labels)
    {
        Vector2 P(Vector2 point)=>Project(point,area,rect);
        void Floor(Vector2[] polygon) {canvas.DrawColoredPolygon(polygon.Select(P).ToArray(),new Color("38565c"));canvas.DrawPolyline(polygon.Append(polygon[0]).Select(P).ToArray(),new Color("668b8d"),1,true);}
        if(area=="safehouse")
        {
            foreach(var room in new[]{SafehouseLayout.RoomA,SafehouseLayout.RoomB})
                Floor(new[]{SafehouseLayout.Iso(room.Position.X,room.Position.Y),SafehouseLayout.Iso(room.End.X,room.Position.Y),SafehouseLayout.Iso(room.End.X,room.End.Y),SafehouseLayout.Iso(room.Position.X,room.End.Y)});
            foreach(var prop in FurnitureLayout.All)canvas.DrawColoredPolygon(prop.Footprint.Select(P).ToArray(),new Color("182e3a"));
        }
        else if(area=="guild")
        {
            Floor(CourtyardRenderer.WalkPolygon);
            foreach(var prop in GuildHallLayout.Furnishings)canvas.DrawColoredPolygon(prop.Footprint.Select(P).ToArray(),new Color("182e3a"));
        }
        else
        {
            foreach(var path in CourtyardRenderer.Paths)Floor(path);
            foreach(var lamp in CourtyardRenderer.Lamps)canvas.DrawCircle(P(lamp),labels?3:1.5f,new Color("9aa57c"));
        }
        string objective=area==run.LocationId?OpeningGuidance.Next(run).Target:"";
        foreach(var landmark in Landmarks(area,run))
        {
            var point=P(landmark.Point);bool goal=landmark.Id==objective;
            var col=new Color(goal?"ffda8f":landmark.Id.StartsWith("route_")||landmark.Id=="outside_door"?"7cb6e6":"b3c6bc");
            float radius=labels?5:2.5f;
            if(goal)canvas.DrawColoredPolygon(new[]{point+new Vector2(0,-radius-2),point+new Vector2(radius+2,0),point+new Vector2(0,radius+2),point+new Vector2(-radius-2,0)},col);
            else if(landmark.Id.StartsWith("route_")||landmark.Id=="outside_door")canvas.DrawRect(new Rect2(point-Vector2.One*radius,Vector2.One*radius*2),col);
            else canvas.DrawCircle(point,radius,col);
            if(labels)
            {
                var font=ThemeDB.FallbackFont;int size=(int)(12*ComfortSettings.TextScale);
                var textSize=font.GetStringSize(landmark.Name,fontSize:size);
                var at=point+new Vector2(9,4);
                if(at.X+textSize.X>rect.End.X)at.X=point.X-textSize.X-9;
                at.Y=Mathf.Clamp(at.Y,rect.Position.Y+size,rect.End.Y-4);
                canvas.DrawStringOutline(font,at,landmark.Name,fontSize:size,size:4,modulate:new Color("10212b"));
                canvas.DrawString(font,at,landmark.Name,fontSize:size,modulate:col);
            }
        }
        if(player is Vector2 position)
        {
            var point=P(position);canvas.DrawCircle(point,labels?8:5,new Color("102a31"));canvas.DrawCircle(point,labels?5:3,new Color("8affcf"));
        }
    }
    public override void _Draw()
    {
        var run=GameSession.Instance?.CurrentRun;if(run==null)return;
        var font=ThemeDB.FallbackFont;var index=WorldRoute.Index(run.LocationId);
        DrawStyleBox(_background,new Rect2(Vector2.Zero,Size));
        DrawString(font,new Vector2(10,21),WorldRoute.Name(run.LocationId),fontSize:(int)(13*ComfortSettings.TextScale),modulate:new Color("e0eeed"));
        DrawLine(new Vector2(18,41),new Vector2(177,41),new Color("405d66"),2,true);
        for(int i=0;i<WorldRoute.Areas.Length;i++)
        {
            var point=new Vector2(18+i*159f/(WorldRoute.Areas.Length-1),41);
            DrawCircle(point,i==index?5:3,new Color(i==index?"8affcf":"728992"));
        }
        DrawString(font,new Vector2(10,61),"Home",fontSize:10,modulate:new Color("a8bec6"));
        DrawString(font,new Vector2(152,61),"Guild",fontSize:10,modulate:new Color("a8bec6"));
        DrawArea(this,run,run.LocationId,new Rect2(8,69,179,76),LivePosition(this,run),false);
        DrawString(font,new Vector2(10,158),"● You   ◆ Goal   M Map",fontSize:(int)(10*ComfortSettings.TextScale),modulate:new Color("bdd5d5"));
    }
    public override void _ExitTree()=>_background.Dispose();
}

public partial class AreaMapView : Control
{
    public string Area {get;set;}="safehouse";
    public override void _Ready(){CustomMinimumSize=new Vector2(0,270);SizeFlagsHorizontal=SizeFlags.ExpandFill;MouseFilter=MouseFilterEnum.Ignore;}
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        var run=GameSession.Instance?.CurrentRun;if(run==null)return;
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("0d1d28"));
        RouteMiniMap.DrawArea(this,run,Area,new Rect2(12,12,Size.X-24,Size.Y-24),Area==run.LocationId?RouteMiniMap.LivePosition(this,run):null,true);
    }
}
