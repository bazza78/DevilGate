using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.World;
using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Owns the safehouse scene: builds the isometric environment (floor, walls,
/// props, colliders, navigation), spawns the player and routes clicks.
/// Interaction spots (chest / PC / door) are registered now so stage 05 can
/// attach real panels without changing movement code.
/// </summary>
public partial class SafehouseController : Node2D
{
    private PlayerController? _player;
    private HUDController? _hud;
    private InteractionController? _interaction;
    private int _physicsFrames;
    private bool _navCheck;
    private string? _shotPath;
    private int _shotFrames;
    private bool _shotWithPanel;
    private bool _moveTest;
    private bool _journey;
    private int _journeyStep;
    private int _journeyWait;
    private bool _exitRequested;
    private string? _exitDestination;
    private string? _devTest;

    public override void _Ready()
    {
        var earlyArgs = OS.GetCmdlineUserArgs();
        var devHarness = earlyArgs.Contains("--navcheck") || earlyArgs.Contains("--savetest")
            || earlyArgs.Contains("--loadtest") || earlyArgs.Contains("--shot")
            || earlyArgs.Contains("--journey");
        if (devHarness && GameSession.Instance?.HasRun != true)
        {
            GameSession.Instance?.StartTemporaryRun();
        }

        var run = GameSession.Instance?.CurrentRun;
        if (run == null)
        {
            AddChild(new Label
            {
                Text = "No run in progress. Return to the title screen.",
                Position = new Vector2(40, 40),
            });
            return;
        }

        if (earlyArgs.Contains("--introshot"))
        {
            GameSession.Instance!.BeginAdventure();
            run = GameSession.Instance.CurrentRun!;
            if (earlyArgs.Contains("--rack")) run.CreationStage = 1;
        }
        if(earlyArgs.Contains("--tank"))
        {
            run.Character.ArchetypeId=Ids.ArchetypeTank;run.Character.GenderId=Ids.GenderFemale;run.Character.Name="Ash";
            run.Character.Attributes=Definitions.GameCatalog.Instance.GetArchetype(Ids.ArchetypeTank).CreateSuggestedAttributes();
            run.Character.Hp=Systems.CharacterRules.MaxHealth(run.Character);run.Character.Mana=Systems.CharacterRules.MaxMana(run.Character);
        }
        if (earlyArgs.Contains("--portraitshot"))
        {
            run.Character.GenderId=Ids.GenderFemale;run.Character.Name="Selene";run.CreationStage=2;run.InductionStage=3;run.MagicAwakened=true;
            Systems.MageTraining.Choose(run,new[]{"ember_bolt","arcane_ward"});
            run.Backpack[0]=new ItemStack(Ids.ItemApprenticeStaff,1);run.Backpack[1]=new ItemStack(Ids.ItemBasicJacket,1);
            run.Backpack[2]=new ItemStack(Ids.ItemHealthPotion,2);run.Backpack[3]=new ItemStack(Ids.ItemSaveCrystal,3);
        }
        if(earlyArgs.Contains("--battle"))
        {
            run.LocationId="woodland";run.InductionStage=3;run.MagicAwakened=true;run.LearnedSpellIds=Systems.StarterTraining.IsTank(run)?new[]{"crushing_blow","iron_stance"}:new[]{"ember_bolt","arcane_ward"};
            var effectArg=Array.IndexOf(earlyArgs,"--battleeffect");
            if(effectArg>=0 && effectArg+1<earlyArgs.Length && Systems.StarterTraining.Skills(run).Any(s=>s.Id==earlyArgs[effectArg+1]))
                run.LearnedSpellIds=new[]{earlyArgs[effectArg+1],Systems.StarterTraining.IsTank(run)?(earlyArgs[effectArg+1]=="iron_stance"?"crushing_blow":"iron_stance"):(earlyArgs[effectArg+1]=="arcane_ward"?"ember_bolt":"arcane_ward")};
            run.PositionX=RiftWisp.EncounterPoint.X;run.PositionY=RiftWisp.EncounterPoint.Y;
        }
        if (earlyArgs.Contains("--occlusion")) { run.PositionX=340;run.PositionY=270; }
        var areaArg=Array.IndexOf(earlyArgs,"--area");
        if(areaArg>=0 && areaArg+1<earlyArgs.Length && WorldRoute.Valid(earlyArgs[areaArg+1]))
        {run.LocationId=earlyArgs[areaArg+1];if(!earlyArgs.Contains("--portraitshot"))run.InductionStage=1;run.PositionX=CourtyardRenderer.Spawn.X;run.PositionY=CourtyardRenderer.Spawn.Y;}
        if (earlyArgs.Contains("--courtyard"))
        {
            run.LocationId="woodland"; run.InductionStage=1;
            run.PositionX=CourtyardRenderer.Spawn.X; run.PositionY=CourtyardRenderer.Spawn.Y;
        }
        if (run.LocationId != "safehouse") BuildCourtyard();
        else BuildEnvironment();
        AddChild(new Atmosphere { Area = run.LocationId, ZIndex = 1 });
        SpawnPlayer(run);
        AddChild(new ArrivalCaption());
        GameSession.Instance!.StateChanged += RefreshPlayerIdentity;

        _hud = GD.Load<PackedScene>("res://Scenes/HUD.tscn").Instantiate<HUDController>();
        AddChild(_hud);

        _interaction = new InteractionController();
        AddChild(_interaction);
        _interaction.Initialize(_player!, _hud!);
        _hud.JournalRequested += () => { if (!_interaction.HasOpenPanel) _interaction.ShowJournal(); };
        _hud.SettingsRequested += () => { if (!_interaction.HasOpenPanel) _interaction.ShowSettings(); };
        _hud.SettleBeforeSave = SettleForSave;
        RegisterInteractables();
        if (GetParent() is MainController)
            _interaction.ExitRequested += (_,e) =>
            {
                if(_pendingDestination!=null || _travelling)return;
                _pendingDestination=e.DestinationId=="outside" ? "woodland" : e.DestinationId;
                CallDeferred(nameof(Travel));
            };

        var args = OS.GetCmdlineUserArgs();
        _navCheck = args.Contains("--navcheck");
        _journey = args.Contains("--journey");
        if (_journey && _interaction != null)
        {
            _interaction.ExitRequested += (_, e) =>
            {
                _exitRequested = true;
                _exitDestination = e.DestinationId;
            };
        }
        _devTest = args.Contains("--savetest") ? "save" : args.Contains("--loadtest") ? "load" : null;
        if (_devTest != null)
        {
            Systems.SaveService.DirectoryOverride = "/tmp/devilgate_savetest";
        }
        var shotIndex = System.Array.IndexOf(args, "--shot");
        if (shotIndex >= 0 && shotIndex + 1 < args.Length)
        {
            _shotPath = args[shotIndex + 1];
        }
        _shotWithPanel = args.Contains("--panel");
        if (!devHarness && !run.WakingIntroductionSeen)
        {
            run.WakingIntroductionSeen = true;
            _interaction!.ShowWakingIntroduction();
        }
    }

    // ------------------------------------------------------------------
    // Environment construction (all geometry comes from SafehouseLayout)
    // ------------------------------------------------------------------

    private string? _pendingDestination;
    private bool _travelling;
    private async void Travel()
    {
        if (_travelling) return;
        var session=GameSession.Instance!;
        var run=session.CurrentRun!;
        var destination=_pendingDestination!;
        if (!WorldRoute.Adjacent(run.LocationId, destination)) { _pendingDestination = null; return; }
        _travelling=true;
        _mouseHeld = false; _player!.Stop();
        var fade = new CanvasLayer { Layer = 20 }; AddChild(fade);
        var cover = new ColorRect { Color = Colors.Black, Modulate = new Color(1,1,1,0), MouseFilter = Control.MouseFilterEnum.Stop };
        cover.SetAnchorsPreset(Control.LayoutPreset.FullRect); fade.AddChild(cover);
        var tween = CreateTween(); tween.TweenProperty(cover, "modulate:a", 1, Systems.ComfortSettings.ReducedMotion ? .05 : .18);
        await ToSignal(tween, Tween.SignalName.Finished);
        _player!.TrackPosition=false;
        bool forward=WorldRoute.Index(destination)>WorldRoute.Index(run.LocationId);
        run.LocationId=destination;
        var spawn=destination=="safehouse" ? SafehouseLayout.DoorApproach : forward ? CourtyardRenderer.Spawn : CourtyardRenderer.ContactApproach;
        run.PositionX=spawn.X; run.PositionY=spawn.Y;
        session.EmitChanged();
        GetParent<MainController>().ShowSafehouse();
    }

    private void BuildCourtyard()
    {
        var area=GameSession.Instance!.CurrentRun!.LocationId;
        if(area=="guild")AddChild(new GuildHallRenderer());
        else AddChild(new CourtyardRenderer {Area=area});
        if(area=="town") AddChild(new Label {Text="ADVENTURERS’ GUILD",Position=SafehouseLayout.Iso(10,-1.4f)+new Vector2(-73,-153),Modulate=new Color("e5d2a9")});
        var polygon=new NavigationPolygon { AgentRadius=11 };
        var geometry=new NavigationMeshSourceGeometryData2D();
        if(area=="guild") geometry.AddTraversableOutline(CourtyardRenderer.WalkPolygon);
        else foreach(var path in CourtyardRenderer.Paths) geometry.AddTraversableOutline(path);
        var lampBodies=new StaticBody2D();AddChild(lampBodies);
        foreach(var lamp in area=="guild" ? Array.Empty<Vector2>() : CourtyardRenderer.Lamps)
        {
            var footprint=CourtyardRenderer.LampFootprint(lamp);
            geometry.AddObstructionOutline(footprint);
            lampBodies.AddChild(new CollisionPolygon2D {Polygon=footprint});
        }
        if(area=="guild")
        {
            foreach(var prop in GuildHallLayout.Furnishings)
            {
                geometry.AddObstructionOutline(prop.Footprint);
                lampBodies.AddChild(new CollisionPolygon2D {Polygon=prop.Footprint});
                AddChild(new FurnitureSprite {AtlasPath=GuildHallLayout.Atlas,Cell=prop.Cell,Width=prop.Width,Position=prop.Anchor});
            }
        }
        if(area=="grove")
        {
            geometry.AddObstructionOutline(FilterRelay.Footprint);
            lampBodies.AddChild(new CollisionPolygon2D{Polygon=FilterRelay.Footprint});
            AddChild(new FilterRelay{Position=FilterRelay.Anchor});
        }
        NavigationServer2D.BakeFromSourceGeometryData(polygon,geometry,Callable.From(()=>{}));
        AddChild(new NavigationRegion2D { NavigationPolygon=polygon });
        if(area=="woodland")AddChild(new RiftWisp {Position=RiftWisp.EncounterPoint});
        if(area!="guild")return;
        var npc=new PlayerToken { Position=CourtyardRenderer.Contact+new Vector2(0,-28) };
        AddChild(npc);
        // A small idle breath keeps the liaison distinct from static furnishings.
        npc.IdleMotion = true;

        npc.AddChild(new Label {Text="Diane · Guild receptionist",Position=new Vector2(-65,-75),ZIndex=10});
    }

    private void BuildEnvironment()
    {
        AddChild(new FloorRenderer());
        AddChild(new MirrorProp { Position = SafehouseLayout.Iso(16.5f,2.6f) });
        AddChild(new WallRenderer());

        var colliders = new StaticBody2D { Name = "Colliders" };
        AddChild(colliders);

        // Interior partition wall blocks + their collision.
        foreach (var (i, j) in SafehouseLayout.WallTiles)
        {
            AddChild(new InteriorWallBlock(i, j));
            AddTileCollider(colliders, i, j);
        }

        // Visuals, physical blockers and navigation cutouts share the same ground anchors.
        foreach (var prop in FurnitureLayout.All)
        {
            AddChild(new FurnitureSprite { Cell=prop.Cell, Width=prop.Width, Position=prop.Anchor });
            colliders.AddChild(new CollisionPolygon2D {Polygon=prop.Footprint});
        }
        AddTileCollider(colliders,16,2); // mirror stand

        AddExteriorColliders(colliders);
        BuildNavigation();
    }

    private void AddTileCollider(StaticBody2D body, int i, int j)
    {
        var shape = new CollisionPolygon2D { Polygon = SafehouseLayout.TileDiamond(i, j) };
        body.AddChild(shape);
    }

    /// <summary>Invisible collision quads just outside each exterior wall segment.</summary>
    private void AddExteriorColliders(StaticBody2D body)
    {
        var houseCenter = new Vector2(430, 385);
        foreach (var seg in SafehouseLayout.ExteriorWalls())
        {
            var dir = seg.B - seg.A;
            if (dir.LengthSquared() < 1f) continue;
            dir = dir.Normalized();
            var mid = (seg.A + seg.B) * 0.5f;
            // Outward = perpendicular pointing away from the house centre.
            var perp = new Vector2(-dir.Y, dir.X);
            if (perp.Dot(mid - houseCenter) < 0) perp = -perp;
            var n = perp * 26f;
            var quad = new[] { seg.A, seg.B, seg.B + n, seg.A + n };
            body.AddChild(new CollisionPolygon2D { Polygon = quad });
        }
    }

    private void BuildNavigation()
    {
        // Bake the navmesh from traversable tile outlines. Baking erodes the
        // walkable area by AgentRadius, which keeps the player's body clear of
        // prop/wall corners instead of snagging on them.
        var navPoly = new NavigationPolygon { AgentRadius = 12f };
        var src = new NavigationMeshSourceGeometryData2D();
        foreach (var (i, j) in SafehouseLayout.FloorTiles())
        {
            src.AddTraversableOutline(SafehouseLayout.TileDiamond(i, j));
        }
        foreach (var prop in FurnitureLayout.All) src.AddObstructionOutline(prop.Footprint);
        src.AddObstructionOutline(SafehouseLayout.TileDiamond(16,2));
        NavigationServer2D.BakeFromSourceGeometryData(navPoly, src, Callable.From(() => { }));

        var region = new NavigationRegion2D { NavigationPolygon = navPoly };
        AddChild(region);
    }

    // ------------------------------------------------------------------
    // Player
    // ------------------------------------------------------------------

    private void SpawnPlayer(RunState run)
    {
        var pos = new Vector2(run.PositionX, run.PositionY);
        if (!(run.LocationId != "safehouse" ? CourtyardRenderer.Bounds : SafehouseLayout.PlayableBounds).HasPoint(pos))
        {
            pos = run.LocationId != "safehouse" ? CourtyardRenderer.Spawn : SafehouseLayout.SpawnPoint;
        }

        var packed = GD.Load<PackedScene>("res://Scenes/Player.tscn");
        _player = packed.Instantiate<PlayerController>();
        _player.Position = pos;
        AddChild(_player);
        _player.AddToGroup("route_player");

        var color = ArchetypeColor(run.Character.ArchetypeId);
        _player.Configure(run.CreationStage == 0 ? "Unknown traveller" : run.Character.Name, color);
        foreach (var prop in GetChildren().OfType<FurnitureSprite>()) prop.Walker=_player;
        _player.WritePositionToRun();
    }

    private void RefreshPlayerIdentity()
    {
        var run = GameSession.Instance?.CurrentRun;
        if (run != null) _player?.Configure(run.CreationStage == 0 ? "Unknown traveller" : run.Character.Name, new Color("b6cbd6"));
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null) GameSession.Instance.StateChanged -= RefreshPlayerIdentity;
    }

    private static Color ArchetypeColor(string archetypeId)
    {
        var catalog = GameCatalog.Instance;
        if (catalog.TryGetArchetype(archetypeId, out var arch) && arch != null)
        {
            return arch.ThemeColor;
        }
        return new Color("9fb4ff");
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    /// <summary>Stop movement and flush the position so a save snapshot is settled.</summary>
    private void SettleForSave()
    {
        _player?.Stop();
        _player?.WritePositionToRun();
    }

    private void RegisterInteractables()
    {
        var interaction = _interaction!;
        var area=GameSession.Instance!.CurrentRun!.LocationId;
        if(area!="safehouse")
        {
            var index=WorldRoute.Index(area);
            var previous=WorldRoute.Areas[index-1];
            var exitLabel=new Label {Name="ReturnDestinationLabel",Text="← "+WorldRoute.Name(previous),Position=CourtyardRenderer.Door+new Vector2(-55,-88),ZIndex=20,MouseFilter=Control.MouseFilterEnum.Ignore};
            exitLabel.AddThemeColorOverride("font_outline_color",new Color("101c25"));
            exitLabel.AddThemeConstantOverride("outline_size",5);
            AddChild(exitLabel);
            interaction.Register(new Interactable {Id="route_back",Label="To "+WorldRoute.Name(previous),Focus=CourtyardRenderer.Door+new Vector2(0,-20),Approach=CourtyardRenderer.Spawn,OnInteract=()=>interaction.ShowTravelPanel(previous,"Return to "+WorldRoute.Name(previous))});
            if(area=="guild")
            {
                var board=GuildHallLayout.Contracts;
                interaction.Register(new Interactable {Id="guild_noticeboard",Label="Guild contract board",Focus=board.Anchor+new Vector2(0,-65),Approach=board.Anchor+new Vector2(0,35),OnInteract=interaction.ShowGuildNoticeboard});
                interaction.Register(new Interactable {Id="guild_contact",Label="Diane · Guild receptionist",Focus=CourtyardRenderer.Contact+new Vector2(0,-56),Approach=CourtyardRenderer.Contact+new Vector2(0,55),OnInteract=interaction.ShowGuildContact});
            }
            else
            {
                if(area=="grove")interaction.Register(new Interactable{Id="filter_relay",Label="Air-filter relay",Focus=FilterRelay.Focus,Approach=FilterRelay.Approach,OnInteract=interaction.ShowFilterRelay});
                var next=WorldRoute.Areas[index+1];
                interaction.Register(new Interactable {Id="route_forward",Label="To "+WorldRoute.Name(next),Focus=CourtyardRenderer.ContactApproach+new Vector2(0,-20),Approach=CourtyardRenderer.ContactApproach,OnInteract=()=>interaction.ShowTravelPanel(next,next=="guild"?"Enter the Adventurers’ Guild":"Continue to "+WorldRoute.Name(next))});
                AddChild(new Label {Text="To "+WorldRoute.Name(next)+" →",Position=CourtyardRenderer.ContactApproach+new Vector2(-40,-65)});
            }
            return;
        }
        interaction.Register(new Interactable {Id="training_crystal",Label="Enchanted training crystal",Focus=SafehouseLayout.PotionFocus+new Vector2(0,-64),Approach=SafehouseLayout.PotionFocus+new Vector2(0,35),OnInteract=interaction.ShowMagicTraining});
        interaction.Register(new Interactable { Id = "identity_mirror", Label = "Mirror", Focus = SafehouseLayout.Iso(16.5f,2.6f)+new Vector2(0,-38), Approach = SafehouseLayout.Iso(16,3.5f), OnInteract = interaction.ShowMirror });
        interaction.Register(new Interactable { Id = "training_rack", Label = "Equipment rack", Focus = SafehouseLayout.Iso(9,1.5f)+new Vector2(0,-40), Approach = SafehouseLayout.Iso(9,3), OnInteract = interaction.ShowTrainingRack });
        interaction.Register(new Interactable
        {
            Id = "starter_chest",
            Label = "Storage chest",
            Focus = SafehouseLayout.ChestFocus,
            Approach = SafehouseLayout.ChestApproach,
            OnInteract = interaction.ShowChestPanel,
        });
        interaction.Register(new Interactable
        {
            Id = "mission_pc",
            Label = "Mission terminal",
            Focus = SafehouseLayout.PcFocus,
            Approach = SafehouseLayout.PcApproach,
            OnInteract = interaction.ShowTerminalPanel,
        });
        interaction.Register(new Interactable
        {
            Id = "outside_door",
            Label = "Exit door",
            Focus = SafehouseLayout.DoorFocus,
            Approach = SafehouseLayout.DoorApproach,
            OnInteract = interaction.ShowDoorPanel,
        });
    }

    private bool _mouseHeld, _dragActive;
    private Vector2 _lastDrag;
    private float _holdTime;
    private float _dragInterval;
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) { _mouseHeld = false; _player?.Stop(); }
    }
    public override void _Process(double delta)
    {
        if(!Input.IsMouseButtonPressed(MouseButton.Left)) { if (_dragActive) _player?.Stop(); _dragActive=false; _mouseHeld=false;return; }
        if (_travelling) { _mouseHeld=false; return; }
        if (_interaction?.HasOpenPanel == true) { _mouseHeld=false; _dragActive=false; return; }
        if(!_mouseHeld || _interaction==null) return;
        _holdTime+=(float)delta;_dragInterval+=(float)delta;
        var screen=GetViewport().GetMousePosition();
        if(screen.X>=GetViewport().GetVisibleRect().Size.X*.7f || screen.Y<133 || GetViewport().GuiGetHoveredControl()!=null) { if (_dragActive) _player?.Stop(); _dragActive=false; return; }
        var mouse=GetGlobalMousePosition();
        if(_holdTime>.16f && _dragInterval>.08f && mouse.DistanceTo(_lastDrag)>8)
        {
            _dragActive=true; _interaction.HandleGroundDrag(mouse);_lastDrag=mouse;_dragInterval=0;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_interaction == null) return;
        if (@event is InputEventKey { Keycode: Key.M, Pressed: true, Echo: false })
        {
            if (_interaction.TopPanel is RouteMapPanel) _interaction.HandleEscape();
            else if (!_interaction.HasOpenPanel) _interaction.ShowRouteMap();
            GetViewport().SetInputAsHandled(); return;
        }
        if (@event is InputEventKey { Keycode: Key.J, Pressed: true, Echo: false } && !_interaction.HasOpenPanel) { _interaction.ShowJournal(); GetViewport().SetInputAsHandled(); return; }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            _mouseHeld=true;_holdTime=0;_dragInterval=0;_lastDrag=GetGlobalMousePosition();
            _interaction.HandleWorldClick(_lastDrag);
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Keycode: Key.Escape, Pressed: true })
        {
            _mouseHeld=false;
            _interaction.HandleEscape();
            GetViewport().SetInputAsHandled();
        }
    }

    // ------------------------------------------------------------------
    // Headless navigation self-check (`-- --safehouse --navcheck`)
    // ------------------------------------------------------------------

    public override void _PhysicsProcess(double delta)
    {
        _physicsFrames++;
        var encounterRun=GameSession.Instance?.CurrentRun;
        if(_physicsFrames>30 && encounterRun?.LocationId=="woodland" && _interaction!=null && !_interaction.HasOpenPanel && !_travelling && _pendingDestination==null)
        {
            var stage=encounterRun.WoodlandBattle.Stage;
            if(stage is 1 or 3 || (stage==0 && Systems.WoodlandBattle.Unlocked(encounterRun) && _player!.Position.DistanceTo(RiftWisp.EncounterPoint)<48))
            {
                Systems.WoodlandBattle.Start(encounterRun);GameSession.Instance!.EmitChanged();
                _mouseHeld=false;_interaction.ShowWoodlandBattle();
            }
        }
        var relayRun=GameSession.Instance?.CurrentRun;
        if(relayRun?.LocationId=="grove" && relayRun.FilterBattle.Stage is 1 or 3 && _interaction!=null && !_interaction.HasOpenPanel)
        {_mouseHeld=false;_interaction.ShowWoodlandBattle();}
        if (_physicsFrames == 30 && _player != null)
        {
            // Older saves may have feet inside a newly corrected furniture footprint.
            var nearest=NavigationServer2D.MapGetClosestPoint(GetWorld2D().NavigationMap,_player.Position);
            if (_player.Position.DistanceTo(nearest)>2)
            {
                _player.Stop(); _player.Position=nearest; _player.WritePositionToRun();
            }
        }

        if (_shotPath != null)
        {
            _shotFrames++;
            if (_shotFrames == 20 && _shotWithPanel)
            {
                if (OS.GetCmdlineUserArgs().Contains("--magic"))
                {
                    var training=GameSession.Instance!.CurrentRun!;training.InductionStage=3;training.MagicAwakened=true;
                    if(OS.GetCmdlineUserArgs().Contains("--knownmagic")) Systems.MageTraining.Choose(training,new[]{"ember_bolt","arcane_ward"});
                    _interaction?.ShowMagicTraining();
                }
                else if (OS.GetCmdlineUserArgs().Contains("--terminal")) _interaction?.ShowTerminalPanel();
                else if (OS.GetCmdlineUserArgs().Contains("--contact")) _interaction?.ShowGuildContact();
                else if (OS.GetCmdlineUserArgs().Contains("--mirror")) _interaction?.ShowMirror();
                else if (OS.GetCmdlineUserArgs().Contains("--rack")) _interaction?.ShowTrainingRack();
                else _interaction?.ShowChestPanel(); // show a modal in the capture
            }
            if(OS.GetCmdlineUserArgs().Contains("--battlecast") && _shotFrames==35 && _interaction?.TopPanel is WoodlandBattlePanel battlePreview)
            {
                var previewArgs=OS.GetCmdlineUserArgs();var effectArg=Array.IndexOf(previewArgs,"--battleeffect");
                var name=effectArg>=0 && effectArg+1<previewArgs.Length ? Systems.StarterTraining.Skills(GameSession.Instance!.CurrentRun!).First(s=>s.Id==previewArgs[effectArg+1]).Name : Systems.StarterTraining.IsTank(GameSession.Instance!.CurrentRun!)?"Crushing Blow":"Ember Bolt";
                var fire=battlePreview.FindChildren("*","Button",true,false).OfType<Button>().FirstOrDefault(b=>b.Text.StartsWith(name));
                fire?.EmitSignal(BaseButton.SignalName.Pressed);
            }
            if (_shotFrames >= (OS.GetCmdlineUserArgs().Contains("--battlecast") ? 60 : 40))
            {
                var img = GetViewport().GetTexture().GetImage();
                var err = img.SavePng(_shotPath);
                GD.Print(err == Error.Ok ? $"SHOT saved {_shotPath}" : $"SHOT failed {err}");
                GetTree().Quit(err == Error.Ok ? 0 : 1);
            }
            return;
        }

        if (_devTest != null && _physicsFrames >= 10)
        {
            var test = _devTest;
            _devTest = null;
            RunDevTest(test);
            return;
        }

        if (_journey)
        {
            RunJourneyStep();
            return;
        }

        if (!_navCheck && !_moveTest) return;
        if (_physicsFrames < 30) return;

        if (_navCheck)
        {
            _navCheck = false;
            RunNavCheck();
            return;
        }

        if (_physicsFrames == 200)
        {
            var dist = _player == null
                ? float.MaxValue
                : _player.GlobalPosition.DistanceTo(SafehouseLayout.ChestApproach);
            var ok = dist <= 40f;
            GD.Print($"MOVECHECK spawn->chestApproach dist={dist:F1} -> {(ok ? "OK" : "FAIL")}");
            if (_player != null && _interaction != null)
            {
                GD.Print($"MOVECHECK playerPos={_player.GlobalPosition} target={_player.GetNode<NavigationAgent2D>("NavigationAgent2D").TargetPosition}");
                // Follow-up: click the PC and verify walk-up + panel open.
                _interaction.HandleWorldClick(SafehouseLayout.PcFocus);
            }
            else
            {
                GetTree().Quit(ok ? 0 : 1);
            }
            return;
        }

        if (_physicsFrames >= 340)
        {
            _moveTest = false;
            var panelOk = _interaction != null && _interaction.HasOpenPanel;
            GD.Print($"INTERACTCHECK click->pcApproach->panel open={panelOk} -> {(panelOk ? "OK" : "FAIL")}");
            GetTree().Quit(panelOk ? 0 : 1);
        }
    }

    // ------------------------------------------------------------------
    // Dev save/load round-trip across fresh processes
    // (`res://Scenes/Safehouse.tscn -- --savetest` writes, `-- --loadtest` reads)
    // ------------------------------------------------------------------

    private void RunDevTest(string test)
    {
        var session = GameSession.Instance;
        var run = session?.CurrentRun;
        if (session == null || run == null)
        {
            GD.Print($"DEVTEST {test}: no run");
            GetTree().Quit(1);
            return;
        }

        if (test == "save")
        {
            // Move one crystal chest -> backpack so a carried crystal exists.
            var catalog = Definitions.GameCatalog.Instance;
            for (var i = 0; i < InventoryState.SlotCount; i++)
            {
                var stack = run.Chest.Inventory[i];
                if (stack != null && stack.ItemId == Data.Ids.ItemSaveCrystal)
                {
                    var moved = Systems.InventoryService.MoveOne(run.Chest.Inventory, i, run.Backpack, -1, catalog);
                    GD.Print($"DEVTEST crystal move: {moved}");
                    break;
                }
            }
            SettleForSave();
            var before = session.CarriedSaveCrystals();
            var result = session.SaveGame();
            GD.Print($"DEVTEST save: crystalsBefore={before} after={session.CarriedSaveCrystals()} result={result.Success} err={result.Error}");
            GetTree().Quit(result.Success && session.CarriedSaveCrystals() == before - 1 ? 0 : 1);
            return;
        }

        var ok = session.TryLoadGame(out var error);
        var loaded = session.CurrentRun;
        GD.Print(ok && loaded != null
            ? $"DEVTEST load: name={loaded.Character.Name} arch={loaded.Character.ArchetypeId} pos=({loaded.PositionX:F0},{loaded.PositionY:F0}) chestGold={loaded.Chest.Gold} carriedCrystals={session.CarriedSaveCrystals()} chestCrystals={loaded.Chest.Inventory.CountItem(Data.Ids.ItemSaveCrystal)} hp={loaded.Character.Hp}"
            : $"DEVTEST load failed: {error}");
        GetTree().Quit(ok ? 0 : 1);
    }

    // ------------------------------------------------------------------
    // Headless full-journey check (`res://Scenes/Safehouse.tscn -- --journey`)
    // Chest -> PC -> door -> Go Outside, plus modal input blocking.
    // ------------------------------------------------------------------

    private bool _chestPanelOk;
    private bool _pcPanelOk;
    private bool _doorPanelOk;
    private bool _blockedClickOk;
    private Vector2 _posBeforeBlockedClick;

    private void RunJourneyStep()
    {
        if (_physicsFrames < 60) return; // let the navmesh bake settle first
        _journeyWait++;

        switch (_journeyStep)
        {
            case 0: // click chest -> expect chest panel
                if (_journeyWait == 1) _interaction?.HandleWorldClick(SafehouseLayout.ChestFocus);
                if (_interaction?.TopPanel is ChestPanelController)
                {
                    _chestPanelOk = true;
                    _posBeforeBlockedClick = _player?.GlobalPosition ?? default;
                    // A world click while the modal is open must be ignored.
                    _interaction.HandleWorldClick(SafehouseLayout.DoorFocus);
                    _journeyStep = 1;
                    _journeyWait = 0;
                }
                else if (_journeyWait > 600)
                {
                    JourneyFail("chest panel never opened");
                    return;
                }
                break;

            case 1: // verify the ignored click didn't move the player, then close
                if (_journeyWait == 30)
                {
                    var moved = _player == null
                        || _player.GlobalPosition.DistanceTo(_posBeforeBlockedClick) > 2f;
                    _blockedClickOk = !moved;
                    // Exercise the visible close button rather than relying only on Escape.
                    if (_interaction?.TopPanel is ChestPanelController chest)
                        chest.GetNode<Button>("%CloseButton").EmitSignal(BaseButton.SignalName.Pressed);
                    else _interaction?.HandleEscape();
                    _journeyStep = 2;
                    _journeyWait = 0;
                }
                break;

            case 2: // click PC -> expect terminal panel
                if (_journeyWait == 1) _interaction?.HandleWorldClick(SafehouseLayout.PcFocus);
                if (_interaction?.TopPanel is TerminalPanelController or ModalPanelController)
                {
                    _pcPanelOk = true;
                    _interaction.HandleEscape();
                    _journeyStep = 3;
                    _journeyWait = 0;
                }
                else if (_journeyWait > 600)
                {
                    JourneyFail("terminal panel never opened");
                    return;
                }
                break;

            case 3: // click door -> expect modal panel, then press Go Outside
                if (_journeyWait == 1) _interaction?.HandleWorldClick(SafehouseLayout.DoorFocus);
                if (_interaction?.TopPanel is ModalPanelController modal)
                {
                    _doorPanelOk = true;
                    PressModalButton(modal, "Go Outside");
                    _journeyStep = 4;
                    _journeyWait = 0;
                }
                else if (_journeyWait > 600)
                {
                    JourneyFail("door panel never opened");
                    return;
                }
                break;

            case 4: // ExitRequested should have fired; close and report
                if (_journeyWait == 30)
                {
                    _interaction?.HandleEscape();
                    _journeyStep = 5;
                    _journeyWait = 0;
                }
                break;

            case 5:
                var allOk = _chestPanelOk && _pcPanelOk && _doorPanelOk
                    && _blockedClickOk && _exitRequested
                    && _exitDestination == "outside";
                GD.Print($"JOURNEYCHECK chest={_chestPanelOk} pc={_pcPanelOk} door={_doorPanelOk} " +
                         $"blockedClick={_blockedClickOk} exit={_exitRequested}({_exitDestination}) " +
                         $"-> {(allOk ? "OK" : "FAIL")}");
                GetTree().Quit(allOk ? 0 : 1);
                break;
        }
    }

    private void JourneyFail(string reason)
    {
        GD.Print($"JOURNEYCHECK FAIL: {reason}");
        GetTree().Quit(1);
    }

    private static void PressModalButton(ModalPanelController modal, string text)
    {
        var row = modal.GetNodeOrNull<HBoxContainer>("VBox/ButtonRow/ActionButtons");
        if (row == null) return;
        foreach (var child in row.GetChildren())
        {
            if (child is Button { Text: var t } button && t == text)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }
        }
    }

    private void RunNavCheck()
    {
        var map = GetWorld2D().NavigationMap;
        var pairs = new (string Name, Vector2 From, Vector2 To)[]
        {
            ("spawn->crystal",SafehouseLayout.SpawnPoint,SafehouseLayout.PotionFocus+new Vector2(0,35)),
            ("spawn->mirror", SafehouseLayout.SpawnPoint, SafehouseLayout.Iso(16,3.5f)),
            ("spawn->rack", SafehouseLayout.SpawnPoint, SafehouseLayout.Iso(9,3)),
            ("spawn->chest", SafehouseLayout.SpawnPoint, SafehouseLayout.ChestApproach),
            ("spawn->pc", SafehouseLayout.SpawnPoint, SafehouseLayout.PcApproach),
            ("spawn->door", SafehouseLayout.SpawnPoint, SafehouseLayout.DoorApproach),
            ("chest->door", SafehouseLayout.ChestApproach, SafehouseLayout.DoorApproach),
            ("door->chest", SafehouseLayout.DoorApproach, SafehouseLayout.ChestApproach),
        };

        var allOk = true;
        foreach (var (name, from, to) in pairs)
        {
            var closestFrom = NavigationServer2D.MapGetClosestPoint(map, from);
            var closestTo = NavigationServer2D.MapGetClosestPoint(map, to);
            var path = NavigationServer2D.MapGetPath(map, closestFrom, closestTo, true);
            var endDist = path.Length > 0 ? path[^1].DistanceTo(closestTo) : float.MaxValue;
            var ok = path.Length >= 2 && endDist <= 20f;
            allOk &= ok;
            GD.Print($"NAVCHECK {name}: points={path.Length} endDist={endDist:F1} -> {(ok ? "OK" : "FAIL")}");
        }
        GD.Print(allOk ? "NAVCHECK ALL OK" : "NAVCHECK FAILURES PRESENT");
        if (!allOk)
        {
            GetTree().Quit(1);
            return;
        }

        // End-to-end movement check: issue a real move order and verify the
        // player actually travels to the chest approach point.
        if (_player != null)
        {
            _moveTest = true;
            _player.MoveTo(SafehouseLayout.ChestApproach);
        }
        else
        {
            GetTree().Quit(1);
        }
    }
}
