using System.Runtime.CompilerServices;
using Godot;

public partial class AudioManager : Node
{
    required public AudioStreamPlayer _sfxPunch;
    required public AudioStreamPlayer _sfxBubbles;
    required public AudioStreamPlayer _sfxPotionDiscard;
    required public AudioStreamPlayer _sfxSwordDrawn;
    required public AudioStreamPlayer _sfxWeaponDiscard;
    required public AudioStreamPlayer _sfxCardDealt;


    // Set by PlaySfx; read by scene tests to assert which sound last played.
    public string LastSfxPlayed { get; private set; } = "";

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
        PlaySfx(_sfxPunch);
    }

    public void PlayBubbles()
    {
        PlaySfx(_sfxBubbles);
    }

    public void PlayPotionDiscard()
    {
        PlaySfx(_sfxPotionDiscard);
    }

    public void PlaySwordDrawn()
    {
        PlaySfx(_sfxSwordDrawn);
    }

    public void PlayWeaponDiscard()
    {
        PlaySfx(_sfxWeaponDiscard);
    }

    public void PlayCardDealt()
    {
        PlaySfx(_sfxCardDealt);
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

    private void PlaySfx(AudioStreamPlayer player, [CallerArgumentExpression("player")] string sfxName = "")
    {
        LastSfxPlayed = sfxName;
        player.Play();
    }
}