using DevilGate.Scripts.World;
using Godot;

namespace DevilGate.Scripts;

/// <summary>Contract for any modal panel that can be closed via CloseRequested.</summary>
public interface ICloseable
{
    event Action? CloseRequested;
}

/// <summary>Raised when the player asks to leave the safehouse (door panel).</summary>
public sealed class ExitRequestedEventArgs : EventArgs
{
    public ExitRequestedEventArgs(string destinationId) => DestinationId = destinationId;
    public string DestinationId { get; }
}

/// <summary>Raised when the player asks for a mission/contract (PC panel).</summary>
public sealed class MissionRequestedEventArgs : EventArgs
{
    public MissionRequestedEventArgs(string missionId) => MissionId = missionId;
    public string MissionId { get; }
}

/// <summary>
/// Owns world interaction for the safehouse: interactable registry, click
/// routing (objects before floor), pending-move execution and the modal panel
/// stack. While any panel is open a full-screen blocker consumes input so
/// world clicks can never move the player; Escape closes the top panel and
/// otherwise cancels pending movement. Closing a panel never resumes a path.
/// </summary>
public partial class InteractionController : CanvasLayer
{
    /// <summary>Typed hook for later exterior work (stage 10 wiring).</summary>
    public event EventHandler<ExitRequestedEventArgs>? ExitRequested;

    /// <summary>Typed hook for later mission-board expansion.</summary>
    public event EventHandler<MissionRequestedEventArgs>? MissionRequested;

    private InteractionMarkers _markers = null!;
    private Label _guidance = null!;
    private Label _hoverLabel = null!;
    private InteractionAudio _audio = null!;
    private PlayerController? _player;
    private HUDController? _hud;
    private readonly List<Interactable> _interactables = new();
    private readonly Stack<(Control Panel, ICloseable Hooks)> _panels = new();
    private ColorRect? _blocker;
    private CenterContainer? _panelRoot;

    public bool HasOpenPanel => _panels.Count > 0;

    /// <summary>The currently topmost open panel, or null.</summary>
    public Control? TopPanel => _panels.Count > 0 ? _panels.Peek().Panel : null;
    public string? LastCloseNotice { get; private set; }

    public override void _Ready()
    {
        Layer = 5;
        _audio = new InteractionAudio(); AddChild(_audio);
        _markers = new InteractionMarkers(); AddChild(_markers);
        var map = new RouteMiniMap(); map.OpenRequested += () => { if (!HasOpenPanel) ShowRouteMap(); }; AddChild(map);
        var guidePanel = new PanelContainer { Position = new Vector2(232,18), MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = new Color(.06f,.1f,.14f,.93f), ContentMarginLeft=14, ContentMarginRight=14, ContentMarginTop=10, ContentMarginBottom=10, CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 };
        guidePanel.AddThemeStyleboxOverride("panel",style);
        _guidance = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _guidance.AddThemeFontSizeOverride("font_size",14);
        guidePanel.AddChild(_guidance); AddChild(guidePanel);
        _hoverLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Visible=false };
        _hoverLabel.AddThemeColorOverride("font_color",new Color("b7fff1"));
        _hoverLabel.AddThemeColorOverride("font_shadow_color",Colors.Black);
        _hoverLabel.AddThemeConstantOverride("shadow_offset_x",1);
        _hoverLabel.AddThemeConstantOverride("shadow_offset_y",1);
        AddChild(_hoverLabel);
        _blocker = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.38f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_blocker);

        _panelRoot = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panelRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_panelRoot);
    }

    public override void _Input(InputEvent input)
    {
        if (TopPanel == null || input is not InputEventKey { Pressed: true } key) return;
        var owner = GetViewport().GuiGetFocusOwner();
        bool tab = key.Keycode == Key.Tab;
        bool arrow = owner is BaseButton && key.Keycode is Key.Up or Key.Down or Key.Left or Key.Right;
        if (!tab && !arrow) return;
        var controls = TopPanel.FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(c => c.IsVisibleInTree() && c.FocusMode == Control.FocusModeEnum.All && (c is not BaseButton b || !b.Disabled)).ToArray();
        if (controls.Length == 0) return;
        int index = Array.IndexOf(controls, owner);
        int direction = key.ShiftPressed || key.Keycode is Key.Up or Key.Left ? -1 : 1;
        controls[(index + direction + controls.Length) % controls.Length].GrabFocus();
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        var run=GameSession.Instance?.CurrentRun;
        if (run == null || _guidance == null) return;
        var next=OpeningGuidance.Next(run);
        _guidance.Text=next.Text;
        var mouse=GetViewport().GetMousePosition();
        bool world=mouse.X<GetViewport().GetVisibleRect().Size.X*.7f && mouse.Y>133 && !HasOpenPanel;
        var canvas=GetViewport().GetCanvasTransform();
        var hovered=world ? NearestInteractable(canvas.AffineInverse()*mouse) : null;
        _markers.Hover=hovered == null ? null : canvas*hovered.Focus;
        var objective=_interactables.Find(i=>i.Id==next.Target);
        _markers.Objective=HasOpenPanel || objective == null ? null : canvas*objective.Focus;
        _hoverLabel.Visible=hovered != null;
        if (hovered != null)
        {
            _hoverLabel.Text=$"{hovered.Label} · click to approach";
            _hoverLabel.Position=new Vector2(Mathf.Min(mouse.X+16,GetViewport().GetVisibleRect().Size.X*.7f-260),mouse.Y+20);
        }
        Input.SetDefaultCursorShape(hovered != null ? Input.CursorShape.PointingHand : Input.CursorShape.Arrow);
    }

    public override void _ExitTree() => Input.SetDefaultCursorShape(Input.CursorShape.Arrow);

    public void Initialize(PlayerController player, HUDController hud)
    {
        _player = player;
        player.MovementFailed += () => _hud?.ShowToast("Cannot reach that destination. Try a nearby clear path.");
        _hud = hud;
    }

    public void Register(Interactable interactable) => _interactables.Add(interactable);

    // ------------------------------------------------------------------
    // Input routing
    // ------------------------------------------------------------------

    /// <summary>Left-click in world space. Interactables win over floor.</summary>
    public void HandleWorldClick(Vector2 pos)
    {
        if (_player == null || _panels.Count > 0) return;

        var map = GetViewport().World2D.NavigationMap;

        var interactable = NearestInteractable(pos);
        if (interactable != null)
        {
            if (!IsReachable(map, interactable.Approach))
            {
                _hud?.ShowToast($"Cannot reach the {interactable.Label.ToLowerInvariant()}.");
                return;
            }
            _audio.Cue("select");
            _markers.Mark(GetViewport().GetCanvasTransform() * interactable.Approach);
            _player.MoveToAndAct(interactable.Approach, interactable.OnInteract);
            return;
        }

        var closest = NavigationServer2D.MapGetClosestPoint(map, pos);
        if (closest.DistanceTo(pos) > SafehouseLayout.ReachableClickTolerance)
        {
            _hud?.ShowToast("Cannot reach that.");
            return;
        }
        _markers.Mark(GetViewport().GetCanvasTransform() * closest);
        _player.MoveTo(closest);
    }

    /// <summary>Escape: close top panel, else cancel pending movement.</summary>
    public void HandleGroundDrag(Vector2 point)
    {
        if (_player==null || HasOpenPanel) return;
        var map=GetViewport().World2D.NavigationMap;
        var nearest=NavigationServer2D.MapGetClosestPoint(map,point);
        if(nearest.DistanceTo(point)<=SafehouseLayout.ReachableClickTolerance) _player.MoveTo(nearest);
    }

    public void HandleEscape()
    {
        if(TopPanel is WoodlandBattlePanel battle){battle.RequestClose();return;}
        if (_panels.Count > 0)
        {
            CloseTopPanel();
            return;
        }
        _player?.CancelPending();
        OpenPanel(new ComfortMenu());
    }

    private Interactable? NearestInteractable(Vector2 pos)
    {
        Interactable? best = null;
        var bestDist = float.MaxValue;
        foreach (var it in _interactables)
        {
            var d = it.Focus.DistanceTo(pos);
            if (d < it.ClickRadius && d < bestDist)
            {
                bestDist = d;
                best = it;
            }
        }
        return best;
    }

    private static bool IsReachable(Rid map, Vector2 point)
    {
        var closest = NavigationServer2D.MapGetClosestPoint(map, point);
        return closest.DistanceTo(point) <= SafehouseLayout.ReachableClickTolerance;
    }

    // ------------------------------------------------------------------
    // Panels
    // ------------------------------------------------------------------

    public void OpenPanel<T>(T panel) where T : Control, ICloseable
    {
        if (_player != null) _player.Stop();
        if(_panels.Count>0)_panels.Peek().Panel.Hide();
        _blocker!.Visible = true;
        _panelRoot!.AddChild(panel);
        panel.CloseRequested += CloseTopPanel;
        _panels.Push((panel, panel));
        panel.Show();
        panel.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => !b.Disabled)?.GrabFocus();
        _audio.Cue("open");
    }

    private void CloseTopPanel()
    {
        if (_panels.Count == 0) return;
        var (panel, hooks) = _panels.Pop();
        if (panel is MagicTrainingPanel training)
        {
            var notice = training.PrepareClose();
            LastCloseNotice = notice;
            if (!string.IsNullOrEmpty(notice)) _hud?.ShowToast(notice);
        }
        hooks.CloseRequested -= CloseTopPanel;
        panel.Hide();
        panel.QueueFree();
        _audio.Cue("close");
        if (_panels.Count == 0)
        {
            _blocker!.Visible = false;
        }
        else
        {
            var previous = _panels.Peek().Panel; previous.Show();
            previous.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => !b.Disabled)?.GrabFocus();
        }
    }

    private ModalPanelController CreatePanel(string title, string body)
    {
        var packed = GD.Load<PackedScene>("res://Scenes/ModalPanel.tscn");
        var panel = packed.Instantiate<ModalPanelController>();
        panel.Setup(title, body);
        return panel;
    }

    private void ResponseAt(Vector2 point)
    {
        var ring = new World.ResponseRing { Position = point };
        GetParent().AddChild(ring);
    }
    public void ShowRouteMap() => OpenPanel(new RouteMapPanel());
    public void ShowJournal() => OpenPanel(new JournalPanel());
    public void ShowSettings() => OpenPanel(new ComfortMenu());

    public void ShowWakingIntroduction()
    {
        var panel = CreatePanel("Somewhere unfamiliar", "You wake in a bed you do not recognise. Even your hands are unfamiliar. This is not your world.\n\nA guild emblem glows on a nearby terminal. Equipment, supplies, a rented room… whoever lived in this body appears to have been an adventurer.\n\nA cloak conceals your unfamiliar form. Click the floor to walk. Look into the mirror beside the bed, then inspect the equipment rack. Supplies wait in the chest.");
        OpenPanel(panel);
    }

    public void ShowMirror()
    {
        if (GameSession.Instance!.CurrentRun!.CreationStage != 0)
        {
            OpenPanel(CreatePanel("Your reflection", "The face in the glass is yours, yet the memories behind it are not. Your identity is settled. The equipment rack may help you remember your training."));
            return;
        }
        var panel = GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();
        panel.RoomMode = "mirror";
        OpenPanel(panel);
    }

    public void ShowTrainingRack()
    {
        var stage = GameSession.Instance!.CurrentRun!.CreationStage;
        if (stage != 1)
        {
            OpenPanel(CreatePanel("Equipment rack", stage == 0 ? "Look into the mirror beside the bed first. Your reflection may tell you who you are." : "The empty hooks tell you what you have taken. Your starter equipment is in your backpack; select each item in the sidebar and choose Equip."));
            return;
        }
        var panel = GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();
        panel.RoomMode = "rack";
        OpenPanel(panel);
    }

    public void ShowChestPanel()
    {
        Systems.GameAudio.Instance?.Cue("chest");
        ResponseAt(SafehouseLayout.ChestFocus);
        var packed = GD.Load<PackedScene>("res://Scenes/ChestPanel.tscn");
        OpenPanel(packed.Instantiate<ChestPanelController>());
    }

    public void ShowTerminalPanel()
    {
        ResponseAt(SafehouseLayout.PcFocus);
        if (GameSession.Instance!.CurrentRun!.CreationStage < 2)
        {
            OpenPanel(CreatePanel("Guild connection", "Identity synchronisation incomplete. Look into the mirror, then choose your training at the equipment rack."));
            return;
        }
        var run = GameSession.Instance!.CurrentRun!;
        if (!run.HasGuildAccessDrive)
        {
            OpenPanel(CreatePanel("Terminal locked", "GUILD ACCESS DRIVE REQUIRED\n\nThe computer refuses your login. A message remains on screen: 'Your access drive is with Diane. Follow the woodland route into town and visit the guild reception.'\n\nFind Diane, then return here with the drive."));
            return;
        }
        if (!run.GuildTerminalVisited) { run.GuildTerminalVisited=true; GameSession.Instance.EmitChanged(); }
        var packed = GD.Load<PackedScene>("res://Scenes/TerminalPanel.tscn");
        var panel = packed.Instantiate<TerminalPanelController>();
        panel.EchoContractRequested += ShowEchoContract;
        panel.MissionBoardRequested += () =>
            MissionRequested?.Invoke(this, new MissionRequestedEventArgs(""));
        OpenPanel(panel);
    }

    public void ShowWoodlandBattle()
    {
        var panel=new WoodlandBattlePanel {SaveRequested=()=>_hud?.RequestSave()};
        panel.RetreatRequested+=()=>ExitRequested?.Invoke(this,new ExitRequestedEventArgs(Systems.WoodlandBattle.RetreatDestination(GameSession.Instance!.CurrentRun!)));
        OpenPanel(panel);
    }

    public void ShowMagicTraining()
    {
        var run=GameSession.Instance!.CurrentRun!;
        if (!Systems.StarterTraining.Eligible(run))
        {
            OpenPanel(CreatePanel("Enchanted crystal","A quiet resonance stirs beneath the surface. Complete your guild induction at the computer before beginning class training."));
            return;
        }
        if (!run.MagicAwakened)
        {
            var panel=CreatePanel(Systems.StarterTraining.IsKnight(run)?"A knight’s resolve":Systems.StarterTraining.IsArcher(run)?"A steady hand":Systems.StarterTraining.IsHealer(run)?"A healing instinct":Systems.StarterTraining.IsTank(run)?"The body remembers":"A spark of memory",Systems.StarterTraining.IsKnight(run)?"The guild lists you as a knight. Your blade feels familiar, and your protective gear responds to your resolve.\n\nThe crystal can restore your techniques. Choose two skills for decisive strikes, protection or recovery. Techniques use focus; a basic blade strike is free. You can change your equipped pair here.":Systems.StarterTraining.IsArcher(run)?"The guild lists you as an Elven archer. The weight of a bow feels familiar, even if your memories do not.\n\nThe crystal can recover your field techniques. Choose two skills for precise shots, control, evasion or recovery. These techniques use focus. You can change your equipped pair here.":Systems.StarterTraining.IsHealer(run)?"The guild lists you as a field healer. Your hands remember the shape of a healing sigil, even if you do not remember learning it.\n\nAttune to the crystal to choose two skills: restore health, protect yourself, or channel light through your wand. Your skills use mana. You can change your equipped pair here.":Systems.StarterTraining.IsTank(run)?"Diane marked your record as Vanguard: a protector trained to hold evacuation routes through the poisoned districts. You do not remember that life, but your body knows how to brace.\n\nThe crystal can synchronise your protective gear and recover your combat instincts. Unlock two starter techniques here; you can retrain freely during testing.":"The guild's induction record mentions a training crystal in your apartment. As you reach towards it, your unfamiliar hands begin to glow.\n\nAttune to unlock two starter skills. You may mix Elemental, Dark and Protective magic, and change your choices here during testing.");
            panel.AddButton("Attune to the crystal",()=>
            {
                if(!Systems.StarterTraining.Awaken(run))return;
                GameSession.Instance.EmitChanged();CloseTopPanel();
                OpenPanel(new MagicTrainingPanel());
            });
            OpenPanel(panel);return;
        }
        OpenPanel(new MagicTrainingPanel());
    }

    public void ShowGuildNoticeboard()
    {
        var notices=CreatePanel("Guild notices", "WELCOME TO THE ADVENTURERS’ GUILD\n\nNew members: register with Diane at reception. Keep your guild card with you. Your access drive connects your home terminal to guild services.\n\nCurrent assignments are available through this board, Diane and your home PC. Complete your first field test to unlock Echo in the Filters.");
        if(Systems.EchoMission.Available(GameSession.Instance!.CurrentRun!))notices.AddButton("Echo in the Filters",ShowEchoContract);
        OpenPanel(notices);
    }

    public void ShowGuildContact()
    {
        var run=GameSession.Instance!.CurrentRun!;
        var panel=CreatePanel("Diane · Guild receptionist",run.InductionStage switch
        {
            0=>"You must be the new arrival. I am Diane, the guild receptionist. Welcome to the Adventurers’ Guild reception. I can register you as a new member. You will receive a guild card and computer access drive.",
            1=>$"{run.Character.Name}? Welcome to the guild. You are at the right desk to register as a guild member. Your guild card and access drive are ready. Once registered, use the drive at home to activate your guild account.",
            2=>"You are registered, and your guild card and drive are yours. Head back along the woodland route and use your terminal to activate the account. Your induction allowance is waiting there.",
            _=>"All registered? Good. Take a breath, get your bearings. The city will still be here when you're ready."
        });
        if(Systems.EchoMission.Available(run))panel.SetBody(run.Echo.Stage switch
        {
            0=>"A rift wisp on Woodland path? That fits the relay outage in Willow grove. I have an inspection contract for you: Echo in the Filters. You can accept it here, or find it on your home PC when you are ready.",
            4=>"You have the sealed sample. Open Echo in the Filters and hand it over; I will authorise your payment and show you the Ashfall archive.",
            5=>"Your sample is in the archive. Gate Seven links both sets of records. Keep that recording private until we know who changed the timestamps.",
            _=>"How is the Willow grove relay? Your Echo in the Filters journal has the current objective. Bring the sealed sample back here when you are done."
        });
        panel.SetPortrait("res://Assets/Portraits/NPCs/diane.png");
        string conversation=panel.GetNode<Label>("%BodyLabel").Text;
        bool showingStory=false;
        bool explainingRobes=false;
        Button? robesButton=null;
        Button? storyButton=null;
        string storyTitle=Systems.StarterTraining.IsKnight(run)?"About my knight record":Systems.StarterTraining.IsArcher(run)?"About my archer record":Systems.StarterTraining.IsHealer(run)?"About my healer record":Systems.StarterTraining.IsTank(run)?"About my Vanguard record":"About my Arcanist record";
        if(Systems.StarterTraining.IsSupported(run.Character.ArchetypeId))
        {
            const string tankStory="Diane opens a sealed personnel record.\n\n'Your guild designation is Vanguard. Whoever wore that armour held the evacuation corridor at Ashfall while the other runners got people out. The last entry ends there.'\n\n'You say you remember another world? I will keep that off the public register. You do not owe us someone else’s memories.'\n\n'Activate your drive at home, then use the training crystal on your potion counter. Start with a heavy strike and Iron Stance. We can investigate Ashfall when you are ready.'";
            const string mageStory="Diane unlocks an archive marked ASHFALL.\n\n'Your guild designation is Arcanist. The person in this record studied a signal beneath the old district. Their last message said it sounded like someone calling from another world. Then their focus went silent.'\n\n'Now you are here, wearing their face and remembering a different life. I will keep that private. We need answers before anyone else notices.'\n\n'Activate your drive at home and attune to the crystal. If the woodland wisps react to you, it may be the same signal. Be careful.'";
            string personalStory=Systems.StarterTraining.IsKnight(run)?"Diane opens your guild record.\n\n'You are a knight: trained to keep a clear head and hold the line. Your techniques draw on Leadership and Presence; your ordinary blade strike relies on Might. Activate your account at home, then visit the crystal. Start with Commanding Strike and Shield Wall.'\n\n'Guard and Shield Wall share one defence bonus. Bracing again will preserve your longer protection.'":Systems.StarterTraining.IsArcher(run)?"Diane checks your guild record.\n\n'An Elven archer. Your bow uses precision, but you also trained to evade attacks and patch yourself up in the field. Activate your account at home, then visit the training crystal. Start with Precision Shot and Evasive Stance.'\n\n'Save your focus when you can. An ordinary bow shot costs no energy.'":Systems.StarterTraining.IsHealer(run)?"Diane opens your guild record.\n\n'You are registered as a field healer. Your wand channels restorative magic, but you can also use it to defend yourself. Activate your account at home, then visit the training crystal. Start with Radiant Bolt and Mend.'\n\n'Out on the woodland route, watch your mana. A free wand pulse can keep you fighting when you need to save energy.'":Systems.StarterTraining.IsTank(run)?tankStory:mageStory;
            storyButton=panel.AddButton(storyTitle,()=>
            {
                showingStory=!showingStory;explainingRobes=false;
                if(robesButton!=null)robesButton.Text="Why does everyone wear robes?";
                storyButton!.Text=showingStory?"Back to our conversation":storyTitle;
                panel.SetBody(showingStory?(run.InductionStage>=2 ? personalStory : "Register with Diane first so she can open your guild record."):conversation);
            });
        }
        robesButton=panel.AddButton("Why does everyone wear robes?",()=>
        {
            showingStory=false;if(storyButton!=null)storyButton.Text=storyTitle;
            explainingRobes=!explainingRobes;
            panel.SetBody(explainingRobes
                ? "Diane lowers her voice.\n\n'They are protective gear. The old industrial districts poisoned the air long before either of us arrived. Dust and chemical ash still drift into town.'\n\n'The robes have a sealed lining, and the hoods carry filtration seals. Keep yours up outside. Guild buildings recycle their air, so you can see who people really are once you are inside.'\n\n'You will learn to recognise people by their voice before their face.'"
                : conversation);
            robesButton!.Text=explainingRobes ? "Back to our conversation" : "Why does everyone wear robes?";
        });
        if (run.InductionStage is 0 or 1)
        {
            Button? registerButton=null;
            registerButton=panel.AddButton("Register with Diane",()=>
            {
                if (!Systems.InductionMission.MeetContact(run)) return;
                registerButton!.Visible=false;
                GameSession.Instance.EmitChanged();
                conversation="Diane checks your details and registers you with the Adventurers' Guild. She hands you a guild card and a small flash drive.\n\n'Welcome. This is your membership card. Use the drive at home to activate your guild account.'\n\nGuild card and access drive received (key items).";
                if(Systems.StarterTraining.IsKnight(run))conversation+="\n\nDiane checks your file: 'Knight. Ask me about your record before you leave.'";
                if(Systems.StarterTraining.IsArcher(run))conversation+="\n\nDiane checks your file: 'Archer. Ask me about your record before you leave.'";
                if(Systems.StarterTraining.IsHealer(run))conversation+="\n\nDiane checks your file: 'Field healer. Ask me about your record before you leave.'";
                if(run.Character.ArchetypeId==Data.Ids.ArchetypeMage)conversation+="\n\nDiane studies your file: 'Arcanist… there is a sealed Ashfall record here. Ask me about it before you leave.'";
                if(Systems.StarterTraining.IsTank(run))conversation+="\n\nDiane pauses at your file: 'Vanguard… there is a sealed Ashfall record here. Ask me about it before you leave.'";
                showingStory=false;if(storyButton!=null)storyButton.Text=storyTitle;
                explainingRobes=false;robesButton!.Text="Why does everyone wear robes?";
                panel.SetBody(conversation);
            });
        }
        if(Systems.EchoMission.Available(run))panel.AddButton("Echo in the Filters",ShowEchoContract);
        OpenPanel(panel);
    }

    public void ShowTravelPanel(string destination,string title)
    {
        var panel=CreatePanel(title,"Continue to "+WorldRoute.Name(destination)+"?");
        var button=panel.AddButton("Travel",()=>ExitRequested?.Invoke(this,new ExitRequestedEventArgs(destination)));
        OpenPanel(panel);
    }

    public void ShowDoorPanel()
    {
        var run=GameSession.Instance!.CurrentRun!;
        bool outside=run.LocationId == "woodland";
        var panel=CreatePanel(outside ? "Apartment entrance" : "Exit door", outside
            ? "The apartment's access panel glows in recognition. Your room is just inside."
            : "The apartment opens onto Woodland path. Continue through Willow grove to Town square and the Adventurers’ Guild. Use the map to follow the shorter route.");
        if (!outside && run.CreationStage < 2)
            panel.SetBody("Before leaving, look into the mirror and choose your training at the equipment rack.");
        else panel.AddButton(outside ? "Return inside" : "Go Outside",()=>
        {
            if (!outside && Systems.InductionMission.Accept(run)) GameSession.Instance.EmitChanged();
            panel.SetBody(outside ? "Returning to the apartment…" : "Heading onto Woodland path…");
            ExitRequested?.Invoke(this,new ExitRequestedEventArgs(outside ? "safehouse" : "outside"));
        });
        OpenPanel(panel);
    }
}
