using Godot;

public partial class AudioManager : Node
{
    private AudioStreamPlayer _sfxPunch = null!;
    private AudioStreamPlayer _sfxBubbles = null!;
    private AudioStreamPlayer _sfxPotionDiscard = null!;
    private AudioStreamPlayer _sfxSwordDrawn = null!;
    private AudioStreamPlayer _sfxWeaponDiscard = null!;
    private AudioStreamPlayer _sfxCardDealt = null!;

    public override void _Ready()
    {
        // Set up your sound players here
        _sfxPunch = new AudioStreamPlayer();
        _sfxBubbles = new AudioStreamPlayer();
        _sfxPotionDiscard = new AudioStreamPlayer();
        _sfxSwordDrawn = new AudioStreamPlayer();
        _sfxWeaponDiscard = new AudioStreamPlayer();
        _sfxCardDealt = new AudioStreamPlayer();
        _sfxPunch.Stream = GD.Load<AudioStream>("res://samples/punch.wav");
        _sfxBubbles.Stream = GD.Load<AudioStream>("res://samples/bubbles.wav");
        _sfxPotionDiscard.Stream = GD.Load<AudioStream>("res://samples/potion_discarded.wav");
        _sfxSwordDrawn.Stream = GD.Load<AudioStream>("res://samples/sword_drawn.wav"); // Will be replaced with the actual sword drawn sound
        _sfxWeaponDiscard.Stream = GD.Load<AudioStream>("res://samples/weapon_discarded.wav");
        _sfxCardDealt.Stream = GD.Load<AudioStream>("res://samples/card_dealt.wav");
        AddChild(_sfxPunch);
        AddChild(_sfxBubbles);
        AddChild(_sfxPotionDiscard);
        AddChild(_sfxSwordDrawn);
        AddChild(_sfxWeaponDiscard);
        AddChild(_sfxCardDealt);
    }

    public void PlayPunch()
    {
        _sfxPunch.Play();
    }

    public void PlayBubbles()
    {
        _sfxBubbles.Play();
    }

    public void PlayPotionDiscard()
    {
        _sfxPotionDiscard.Play();
    }

    public void PlaySwordDrawn()
    {
        _sfxSwordDrawn.Play();
    }

    public void PlayWeaponDiscard()
    {
        _sfxWeaponDiscard.Play();
    }

    public void PlayCardDealt()
    {
        _sfxCardDealt.Play();
    }

    public void EndOfGame(float BouncePitchMin, float BouncePitchRange)
    {
        var sfx = new AudioStreamPlayer
        {
            Stream = _sfxCardDealt.Stream,
            PitchScale = BouncePitchMin + (float)new System.Random().NextDouble() * BouncePitchRange
        };
        AddChild(sfx);
        sfx.Connect("finished", Callable.From(sfx.QueueFree));
        sfx.Play();
    }
}