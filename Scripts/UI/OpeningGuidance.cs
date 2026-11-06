using DevilGate.Scripts.Data;
using Godot;
namespace DevilGate.Scripts;

/// <summary>Passive guidance; it never changes progression or grants rewards.</summary>
public static class OpeningGuidance
{
    public static (string Text,string Target) Next(RunState run)
    {
        if(run.LocationId=="woodland" && Systems.WoodlandBattle.Unlocked(run) && run.WoodlandBattle.Stage<2)
            return ("Something stirs on the path\nA rift wisp senses your newly attuned gear. Be ready.","");
        if(run.InductionStage==3 && run.LearnedSpellIds.Length==2)
        {
            if(run.WoodlandBattle.Stage==2)
            {
                string forward=run.LocationId=="safehouse"?"outside_door":"route_forward";
                string toGrove=run.LocationId=="safehouse"?"outside_door":World.WorldRoute.Index(run.LocationId)<World.WorldRoute.Index("grove")?"route_forward":"route_back";
                if(run.Echo.Stage==0)return ("New contract · Echo in the Filters\nAccept from Diane at guild reception or through your home PC.",run.LocationId=="safehouse"?"mission_pc":run.LocationId=="guild"?"guild_contact":"route_forward");
                if(run.Echo.Stage is 1 or 2 or 3)return (run.Echo.Stage switch
                {
                    1=>"Echo in the Filters · 1/3 Inspect the relay\nFind the air-filter relay beside the path in Willow grove.",
                    2=>"Echo in the Filters · 2/3 Disable the guardian\nUse the relay's manual reset to confront the corrupted guardian.",
                    _=>"Echo in the Filters · 2/3 Recover the sample\nRead the relay recording, then reset it and seal the sample."
                },run.LocationId=="grove"?"filter_relay":toGrove);
                if(run.Echo.Stage==4)return ("Echo in the Filters · 3/3 Return to Diane\nTake the sealed sample to guild reception for your reward.",run.LocationId=="guild"?"guild_contact":forward);
                return ("Echo in the Filters complete · Free exploration\nThis chapter ends here. The Gate Seven investigation continues in a future update.","");
            }
            return ("Ready to explore · First field test\nHead outside to Woodland path. Investigate the rift disturbance.",run.LocationId=="safehouse"?"outside_door":"route_back");
        }
        if(run.LocationId!="safehouse")
        {
            if(run.InductionStage==3 && run.LocationId=="guild")return ("Adventurers’ Guild\nRead the notices, speak to Diane, or return to town.","guild_noticeboard");
            if(run.InductionStage==3)return ("Guild registration complete\nFollow the route home when you are ready.","route_back");
            if(run.InductionStage>=2)return ("Return to your apartment\nFollow the route back home and activate your guild account.","route_back");
            if(run.LocationId=="guild")return ("Register as a guild member\nApproach Diane at the reception desk.","guild_contact");
            return ("Travel to the Adventurers’ Guild\nFollow the paths into town. The map shows your current area.","route_forward");
        }
        if (run.InductionStage == 1) return ("First Connection · Step outside\nFollow the paths into town and visit guild reception.","outside_door");
        if (run.InductionStage == 2) return ("First Connection · Activate your guild account\nUse your new drive at home to activate your account and claim your allowance.","mission_pc");
        if (run.InductionStage == 3 && Systems.StarterTraining.IsSupported(run.Character.ArchetypeId) && run.LearnedSpellIds.Length == 0)
            return (Systems.StarterTraining.IsArcher(run)?"A steady hand · Recover your archery\nVisit the training crystal to choose two archer skills.":Systems.StarterTraining.IsHealer(run)?"A healing instinct · Recover your training\nVisit the enchanted crystal to choose two healer skills.":Systems.StarterTraining.IsTank(run)?"The body remembers · Train as a Vanguard\nVisit the crystal on the potion counter to choose two techniques.":"A spark of memory · Awaken your magic\nVisit the enchanted crystal on the potion counter.","training_crystal");
        if (run.InductionStage == 3) return ("Ready to explore\nVisit the training crystal to select your two starter skills.","");
        if (run.CreationStage == 0) return ("1 · A stranger in the mirror\nClick the mirror beside the bed to discover your identity.","identity_mirror");
        if (run.CreationStage == 1) return ("2 · Remember your training\nClick the equipment rack to choose stats and collect your kit.","training_rack");
        if (run.Chest.Gold > 0) return ("3 · Gather your supplies\nOpen the chest. Take the gold, potions and save crystals.","starter_chest");
        if (run.Equipment.Weapon == null || run.Equipment.Armour == null)
            return ("4 · Prepare for the unknown\nSelect your starter items in the sidebar and choose Equip.","");
        if (!run.HasGuildAccessDrive) return ("5 · Find your connection\nFollow the path outside. Diane has your computer access drive.","outside_door");
        return ("Your first connection\nUse your access drive at the guild terminal.","mission_pc");
    }
}

/// <summary>Hover and objective markers projected into the same canvas as the world.</summary>
public partial class InteractionMarkers : Node2D
{
    public Vector2? Hover { get; set; }
    public Vector2? Objective { get; set; }
    private float _time, _markTime;
    private Vector2 _destination;
    public void Mark(Vector2 point) { _destination = point; _markTime = .8f; }
    public override void _Process(double delta) { _time += (float)delta; _markTime = Mathf.Max(0, _markTime - (float)delta); QueueRedraw(); }
    public override void _Draw()
    {
        if (_markTime > 0)
            DrawArc(_destination, 10 + (1 - _markTime) * 8, 0, Mathf.Tau, 40, new Color("b8e7dc", _markTime), 2, true);
        if (Objective is Vector2 target)
        {
            var p = target + new Vector2(0,-27 + Mathf.Sin(_time*2)*3);
            DrawColoredPolygon(new[] {p+new Vector2(-5,-6),p+new Vector2(5,-6),p+new Vector2(0,1)},new Color("e8c88e"));
        }
        if (Hover is Vector2 centre)
        {
            DrawArc(centre,22,0,Mathf.Tau,48,new Color("83e6dc"),2,true);
            DrawCircle(centre,3,new Color("d1fff1"));
        }
    }
}

/// <summary>Short, quiet synthesized UI tones; no external audio dependencies.</summary>
public partial class InteractionAudio : AudioStreamPlayer
{
    public bool Muted { get; set; }
    private readonly Dictionary<string,AudioStreamWav> _clips = new();
    public override void _Ready()
    {
        VolumeDb = -23;
        foreach (var (key,freq) in new[] {("open",520f),("close",350f),("select",660f)})
        {
            const int rate=22050; const int count=2646;
            var bytes=new byte[count*2];
            for(int i=0;i<count;i++)
            {
                var envelope=Math.Sin(Math.PI*i/count)*Math.Exp(-4.0*i/count);
                short sample=(short)(Math.Sin(Math.Tau*freq*i/rate)*envelope*12000);
                bytes[i*2]=(byte)(sample&255); bytes[i*2+1]=(byte)((sample>>8)&255);
            }
            _clips[key]=new AudioStreamWav { Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Data=bytes };
        }
    }
    public override void _ExitTree()
    {
        Stop(); Stream=null;
        foreach (var clip in _clips.Values) clip.Dispose();
        _clips.Clear();
    }
    public void Cue(string key)
    {
        if (DisplayServer.GetName() == "headless" || Muted || !_clips.TryGetValue(key,out var clip)) return;
        VolumeLinear = Systems.ComfortSettings.Master * Systems.ComfortSettings.Effects * .15f;
        Stream=clip; Play();
    }
}
