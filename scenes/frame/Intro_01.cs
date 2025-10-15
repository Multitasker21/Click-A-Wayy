using Godot;
using System;

public partial class Intro_01 : Control
{
    [Export] private float SplashDuration = 12f; // Seconds
    private PackedScene Intro02Scene;

    public override void _Ready()
    {
        // Load the scene properly
        Intro02Scene = ResourceLoader.Load<PackedScene>("res://scenes/frame/intro_02.tscn");
        if (Intro02Scene == null)
        {
            GD.PrintErr("Failed to load intro_02.tscn!");
            return;
        }

        // Timer to go to Intro_02
        Timer timer = new Timer();
        AddChild(timer);
        timer.WaitTime = SplashDuration;
        timer.OneShot = true;
        timer.Start();
        timer.Timeout += () =>
        {
            // Access autoload instance via /root
            TransitionLayer transitionLayer = GetNode<TransitionLayer>("/root/TransitionLayer");
            if (transitionLayer != null)
                transitionLayer.StartTransition(Intro02Scene);
        };
    }
}
