using Godot;
using System;

public partial class MenuTab : Control
{
    [Export] public RichTextLabel ScoreCount;
    [Export] public RichTextLabel GoldCount;
    [Export] public RichTextLabel ObjectPlaceCount;

    private globals _globals;

    public override void _Ready()
    {
        // Get reference to the autoloaded globals node
        _globals = GetNode<globals>("/root/globals");

        // Connect to score update signal
        _globals.ScoreUpdated += OnScoreUpdated;

        // Initialize display once
        UpdateDisplay(_globals.Score, _globals.GoldCount, _globals.ObjectPlaceCount);
    }

    private void OnScoreUpdated(int score, int gold, int objCount)
    {
        UpdateDisplay(score, gold, objCount);
    }

    private void UpdateDisplay(int score, int gold, int objCount)
    {
        // 🟢 Format preserving display
        // Score: two-line format
        ScoreCount.Text =
            "[center][color=#00eea9][b][font_size=28] SCORE [/font_size][/b][/color][/center]\n" +
            $"[center][color=#00a0a0][font_size=18]{score:D7}[/font_size][/color][/center]";

        // Gold: golden color, same format
        GoldCount.Text =
            $"[center][color=#FFD700][b][font_size=25]{gold:D3}[/font_size][/b][/color][/center]";

        // Object Place Count: white
        ObjectPlaceCount.Text =
            $"[center][color=#ffffff][b][font_size=25]{objCount:D3}[/font_size][/b][/color][/center]";
    }

    public override void _ExitTree()
    {
        // Safe disconnect to avoid duplicate signals
        if (_globals != null)
            _globals.ScoreUpdated -= OnScoreUpdated;
    }
}
