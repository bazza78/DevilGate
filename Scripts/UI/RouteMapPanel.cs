using DevilGate.Scripts.World;
using Godot;
namespace DevilGate.Scripts;

/// <summary>Read-only route and local map. Area selection previews landmarks; travel still uses world exits.</summary>
public partial class RouteMapPanel : PanelContainer, ICloseable
{
    public event Action? CloseRequested;
    public override void _Ready()
    {
        CustomMinimumSize=new Vector2(850,620);
        AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("14232f"),BorderColor=new Color("537d80"),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,ContentMarginLeft=20,ContentMarginRight=20,ContentMarginTop=16,ContentMarginBottom=16,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});
        var run=GameSession.Instance!.CurrentRun!;
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",10);AddChild(column);
        var header=new HBoxContainer();column.AddChild(header);
        header.AddChild(new Label {Text="AREA MAP · "+WorldRoute.Name(run.LocationId),SizeFlagsHorizontal=SizeFlags.ExpandFill});
        var close=new Button {Text="Close map (M / Esc)",Name="CloseMapButton"};header.AddChild(close);close.Pressed+=()=>CloseRequested?.Invoke();
        column.AddChild(new Label {Text="Objective · "+OpeningGuidance.Next(run).Text.Split('\n')[0],AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var route=new HBoxContainer();route.AddThemeConstantOverride("separation",6);column.AddChild(route);
        var map=new AreaMapView {Area=run.LocationId,SizeFlagsVertical=SizeFlags.ExpandFill};
        var areaTitle=new Label();var landmarks=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(0,44)};
        var buttons=new Dictionary<string,Button>();
        void Select(string area)
        {
            map.Area=area;
            areaTitle.Text=WorldRoute.Name(area)+(area==run.LocationId?" · YOU ARE HERE":" · Preview");
            landmarks.Text=string.Join("  ·  ",RouteMiniMap.Landmarks(area,run).Select(p=>p.Name));
            foreach(var (id,button) in buttons)button.ButtonPressed=id==area;
        }
        foreach(var area in WorldRoute.Areas)
        {
            if (buttons.Count > 0) route.AddChild(new Label { Text="→", VerticalAlignment=VerticalAlignment.Center, Modulate=new Color("91b4bd") });
            var button=new Button {Text=WorldRoute.Name(area).Replace(" ","\n"),Name="MapArea_"+area,ToggleMode=true,CustomMinimumSize=new Vector2(135,48),SizeFlagsHorizontal=SizeFlags.ExpandFill};
            if(area==run.LocationId)button.Text+="\n● Here";
            button.TooltipText="Preview "+WorldRoute.Name(area)+". Use the world exits to travel.";
            route.AddChild(button);buttons[area]=button;button.Pressed+=()=>Select(area);
        }
        column.AddChild(areaTitle);column.AddChild(map);column.AddChild(landmarks);
        column.AddChild(new Label {Text="● You   ◆ Current goal   ■ Exit   ● Landmark",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        column.AddChild(new Label {Text="Select an area above to preview it. Walk to an exit in the world to travel.",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        Select(run.LocationId);
    }
}
