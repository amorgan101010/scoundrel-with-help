using Godot;
using System;

public partial class AnalyzeButton : Button
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var analyzer = new ScoundrelAnalyzer();
    
    	// Fire and forget (don't await it here, let it run in the background)
    	_ = analyzer.RunBatchAsync(1000); 
    	GD.Print("Started analyzing 1000 decks in the background...");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
