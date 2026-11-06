using Godot;
using DevilGate.Scripts.Data;
namespace DevilGate.Scripts.Checks;
public partial class IntroChecks : Node
{
    private int _failed;
    private void Check(bool value,string message) { GD.Print($"INTRO {(value ? "PASS" : "FAIL")}: {message}"); if (!value) _failed++; }
    public override void _Ready()
    {
        var session = GameSession.Instance!;
        session.BeginAdventure();
        var run = session.CurrentRun!;
        var id = run.Character.RunId;
        Check(run.CreationStage == 0,"new adventure starts unidentified");
        Check(OpeningGuidance.Next(run).Target == "identity_mirror","new-run guidance points to mirror");
        Check(!SaveData.FromRun(run).ToRunState().GuildTerminalVisited,"terminal discovery roundtrip");
        Check(run.Chest.Inventory.CountItem(Ids.ItemBasicJacket) == 0,"kit removed from chest");
        Check(run.Chest.Inventory.CountItem(Ids.ItemSaveCrystal) == 3 && run.Chest.Gold == 30,"supplies retained");
        Check(new SaveData().CreationStage == 2,"legacy saves default completed");
        Check(SaveData.FromRun(run).ToRunState().CreationStage == 0,"unidentified save roundtrip");
        var mirror = GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();
        mirror.RoomMode = "mirror"; AddChild(mirror);
        var choices=mirror.GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/ArchetypeRow").GetChildren().OfType<Button>().ToArray();
        Check(choices.Count(b=>!b.Disabled)==4 && !choices.Single(b=>b.Text=="Mage").Disabled,"mage, tank, archer and healer selectable in test roster");
        Check(mirror.GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/GenderRow").GetChildren().OfType<Button>().Single(b=>b.Text=="Male").Disabled==false,"male mage choice enabled");
        Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath == "res://Assets/Portraits/mage_female.png","supplied portrait displayed in mirror");
        var maleChoice=mirror.GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/GenderRow").GetChildren().OfType<Button>().Single(b=>b.Text=="Male");
        maleChoice.ButtonPressed=true;maleChoice.EmitSignal(BaseButton.SignalName.Pressed);
        Check(mirror.GetNode<TextureRect>("%PortraitTexture").Texture?.ResourcePath=="res://Assets/Portraits/mage_male.png","male selection shows supplied male portrait");
        var femaleChoice=mirror.GetNode<HBoxContainer>("Margin/HBox/Scroll/LeftVBox/GenderRow").GetChildren().OfType<Button>().Single(b=>b.Text=="Female");
        femaleChoice.ButtonPressed=true;femaleChoice.EmitSignal(BaseButton.SignalName.Pressed);
        mirror.GetNode<LineEdit>("%NameEdit").Text = "Awakened";
        mirror.GetNode<Button>("%BeginButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(run.CreationStage == 1 && run.Character.Name == "Awakened","mirror confirms identity");
        Check(OpeningGuidance.Next(run).Target == "training_rack","identified guidance points to rack");
        Check(run.Character.RunId == id,"identity preserves original run");
        Check(run.Character.GenderId==Ids.GenderFemale && run.Character.ArchetypeId==Ids.ArchetypeMage,"test identity confirms female dark elf mage");
        Check(SaveData.FromRun(run).ToRunState().CreationStage == 1,"identity save roundtrip");
        mirror.QueueFree();
        // Full backpack must not grant a kit or advance creation.
        for (int i=0;i<12;i++) run.Backpack[i] = new ItemStack(Ids.ItemHealthPotion,1);
        var rack = GD.Load<PackedScene>("res://Scenes/CharacterCreation.tscn").Instantiate<CharacterCreationController>();
        rack.RoomMode = "rack"; AddChild(rack);
        var confirm = rack.GetNode<Button>("%BeginButton");
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(run.CreationStage == 1,"full backpack rejects kit atomically");
        run.Backpack[0]=null; run.Backpack[1]=null;
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(run.CreationStage == 2 && run.Backpack.CountItem(Ids.ItemBasicJacket)==1,"rack confirms training and grants kit");
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(run.Backpack.CountItem(Ids.ItemBasicJacket)==1,"repeated confirmation cannot duplicate kit");
        Check(SaveData.FromRun(run).ToRunState().CreationStage == 2,"completed save roundtrip");
        Check(OpeningGuidance.Next(run).Target == "starter_chest","trained guidance points to supplies");
        run.Chest.Gold=0;
        Check(OpeningGuidance.Next(run).Target == "","equipment guidance uses sidebar");
        run.Equipment.Weapon=run.Backpack[0]; run.Equipment.Armour=run.Backpack[1];
        Check(OpeningGuidance.Next(run).Target == "outside_door","equipped guidance points outside for drive");
        run.GuildTerminalVisited=true;
        Check(OpeningGuidance.Next(run).Target == "outside_door","visiting terminal cannot bypass drive requirement");
        GD.Print($"INTRO CHECKS: {_failed} failures");
        GetTree().Quit(_failed==0 ? 0 : 1);
    }
}
