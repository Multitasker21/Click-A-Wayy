using Godot;
using System;

public partial class Player2 : CharacterBody2D
{
    [Export] public float Speed = 80f;
    [Export] public NodePath TargetPath;
    [Export] public NodePath NavAgentPath;

    private Marker2D _target;
    private NavigationAgent2D _navAgent;

    public override void _Ready()
    {
        _target = GetNode<Marker2D>(TargetPath);
        _navAgent = GetNode<NavigationAgent2D>(NavAgentPath);

        _navAgent.PathChanged += OnPathChanged;

        _navAgent.TargetDesiredDistance = 4f;
        _navAgent.PathDesiredDistance = 2f;
        _navAgent.Radius = 6f; // Match to your CollisionShape2D width / 2

        _navAgent.TargetPosition = _target.GlobalPosition;
    }

    private void OnPathChanged()
    {
        GD.Print($"📍 New path with {_navAgent.GetCurrentNavigationPath().Length} points");
    }

    public override void _PhysicsProcess(double delta)
    {
        _navAgent.TargetPosition = _target.GlobalPosition;

        if (_navAgent.IsNavigationFinished())
        {
            Velocity = Vector2.Zero;
            return;
        }

        Vector2 nextPathPoint = _navAgent.GetNextPathPosition();
        Vector2 direction = (nextPathPoint - GlobalPosition).Normalized();

        Velocity = direction * Speed;

        _navAgent.Velocity = Velocity; // Help the agent adjust for collisions

        MoveAndSlide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
        {
            _navAgent.TargetPosition = _target.GlobalPosition;
            GD.Print("🔁 Path refreshed.");
        }
    }
}
