using Godot;
using System;

public partial class Gold : Node2D
{
    private Area2D _detector;

    public override void _Ready()
    {
        _detector = GetNode<Area2D>("Detector");
        _detector.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("Player"))
        {
            var globals = GetNode<globals>("/root/globals");
            
            // 🟡 Register gold collection in global system
            globals.RegisterGoldCollect(50);

            // 💫 Optional: play a particle or sound
            // var particle = GetNodeOrNull<CPUParticles2D>("CollectEffect");
            // particle?.Emitting = true;

            GD.Print("💰 Gold collected! +50");

            // Remove gold object from the scene
            QueueFree();
        }
    }
}
