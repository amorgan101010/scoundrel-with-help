using Godot;

public partial class AudioManager : Node
{
    private AudioStreamPlayer _sfxPunch = null!;

    public override void _Ready()
    {
        // Set up your sound players here
        _sfxPunch = new AudioStreamPlayer();
        _sfxPunch.Stream = GD.Load<AudioStream>("res://samples/punch.wav");
        AddChild(_sfxPunch);
    }

    public void PlayPunch()
    {
        _sfxPunch.Play();
    }
}