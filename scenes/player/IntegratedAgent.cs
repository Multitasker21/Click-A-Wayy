using Godot;
using System;
using System.Collections.Generic;

public partial class IntegratedAgent : NavigationAgent2D
{
    [Export] public bool ShowDebugPath = true;
    [Export] public PackedScene MarkerScene;

    private Line2D _debugLine;
    private List<Node2D> _markers = new();

    public override void _Ready()
    {
        _debugLine = new Line2D
        {
            Width = 2,
            DefaultColor = new Color(0, 1, 0, 0.7f), // greenish
            ZIndex = 1000
        };
        AddChild(_debugLine);
    }

    public override void _Process(double delta)
    {
        UpdateDebugPath();
    }

    private void UpdateDebugPath()
{
    if (!ShowDebugPath)
    {
        _debugLine.ClearPoints();
        return;
    }

    var path = GetCurrentNavigationPath();
    if (path.Length == 0)
    {
        _debugLine.ClearPoints();
        return;
    }

    // --- Draw main line ---
    _debugLine.ClearPoints();
    foreach (var p in path)
        _debugLine.AddPoint(_debugLine.ToLocal(p));

    // --- Remove old markers ---
    foreach (var m in _markers)
        m.QueueFree();
    _markers.Clear();

    // --- Place markers at corners (excluding start & end) ---
    for (int i = 1; i < path.Length - 1; i++)
        AddMarkerAt(_debugLine.ToLocal(path[i]));

    // --- Add marker at final target point ---
    AddMarkerAt(_debugLine.ToLocal(path[^1])); // ^1 = last element
}

private void AddMarkerAt(Vector2 pos)
{
    Node2D marker;

    if (MarkerScene != null)
    {
        marker = MarkerScene.Instantiate<Node2D>();
    }
    else
    {
        marker = new Node2D();
        marker.Draw += () =>
        {
            marker.DrawCircle(Vector2.Zero, 4, new Color(1, 0, 0, 0.8f));
        };
    }

    marker.Position = pos;
    _debugLine.AddChild(marker);
    _markers.Add(marker);
}


}
