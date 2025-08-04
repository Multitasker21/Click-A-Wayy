using Godot;
using System;
using System.Collections.Generic;

public partial class Drum : PanelContainer
{
    private AnimationPlayer _animPlayer;
    private Sprite2D _outline;

    [Export] public NodePath TileHoverScenePath;
    [Export] public Sprite2D DrumSpriteTemplate;

    public override void _Ready()
    {
        _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _outline = GetNode<Sprite2D>("Outline");
        _outline.Visible = false;

        this.Connect(Control.SignalName.MouseEntered, new Callable(this, nameof(OnMouseEntered)));
        this.Connect(Control.SignalName.MouseExited, new Callable(this, nameof(OnMouseExited)));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            GD.Print("🟢 Drum clicked!");

            var tileHover = GetNode<TileHoverScene>(TileHoverScenePath);

            // 🟣 Define 2x2 square footprint for the drum
            // 🟣 Dynamically generate footprint from the visible pixels of the drum sprite
            var footprint = TileHoverScene.GetFootprintFromTexture(
                DrumSpriteTemplate.Texture,
                new Vector2(8, 8), // Same as your tile size
                0.1f               // Alpha threshold: ignore nearly invisible pixels
            );

            tileHover.BeginPlacingObject(DrumSpriteTemplate, footprint);

        }
    }

    private void OnMouseEntered()
    {
        _outline.Visible = true;
        GD.Print("Mouse entered");
        _animPlayer?.Play("Light");
    }

    private void OnMouseExited()
    {
        _outline.Visible = false;
        GD.Print("Mouse exited");
        _animPlayer?.Play("reset");
    }
}
