using Godot;
using System;
using System.Collections.Generic;

public partial class globals : Node
{
    // 🧮 Core score variables
    public int GoldCount = 0;
    public int ObjectPlaceCount = 0;
    public int BricksPlaced = 0;
    public int UndoCount = 0;
    public double LvlDiffMul = 1.3;
    public int Score = 100;

    // 🧱 Performance configuration (expand later for multiple levels)
    private Dictionary<int, (int minBricks, int minObjects)> LevelRequirements = new()
    {
        { 1, (2, 2) }  // Level 1 requires at least 2 bricks + 2 objects for max efficiency
    };

    // 📊 Active level (default = 1)
    public int CurrentLevel = 1;

    // 📡 Signal for UI updates
    [Signal]
    public delegate void ScoreUpdatedEventHandler(int score, int gold, int objCount);

    public override void _Ready()
    {
        GD.Print("🌍 Globals ready.");
        RecalculateScore();
    }

    // 🪙 Gold collected (+50 each)
    public void RegisterGoldCollect(int amount)
    {
        GoldCount += amount;
        RecalculateScore();
    }

    // 🧱 Brick moved (-1 × count)
    public void RegisterBrickMove(int count)
    {
        BricksPlaced += count;
        ObjectPlaceCount += count;
        Score -= count;
        RecalculateScore();
    }

    // 🧩 Object placed (-15)
    public void RegisterObjectPlacement(int count)
    {
        ObjectPlaceCount += count * 15;
        Score -= 15 * count;
        RecalculateScore();
    }

    // 🔁 Undo / Redo (-5)
    public void RegisterUndoRedo()
    {
        UndoCount++;
        ObjectPlaceCount += 5;
        Score -= 5;
        RecalculateScore();
    }

    // 🧮 Recalculate total score using performance rules
    public void RecalculateScore()
    {
        var (minBricks, minObjects) = LevelRequirements.ContainsKey(CurrentLevel)
            ? LevelRequirements[CurrentLevel]
            : (2, 2);

        // 🧩 Base calculation
        double baseScore = (GoldCount * LvlDiffMul) - (ObjectPlaceCount + BricksPlaced + UndoCount);

        // 🎯 Performance adjustment
        double performanceMultiplier = 1.0;

        if (BricksPlaced <= minBricks && ObjectPlaceCount <= minObjects * 15)
            performanceMultiplier = 1.2; // Reward efficiency (20% bonus)
        else if (BricksPlaced > minBricks * 3 || ObjectPlaceCount > minObjects * 45)
            performanceMultiplier = 0.7; // Heavy penalty for inefficiency

        Score = Mathf.RoundToInt((float)(baseScore * performanceMultiplier));

        // 🟢 Keep Score minimum 0
        if (Score < 0)
            Score = 0;

        EmitSignal(SignalName.ScoreUpdated, Score, GoldCount, ObjectPlaceCount);
        GD.Print($"📈 Score updated: {Score} | Gold: {GoldCount} | ObjCount: {ObjectPlaceCount}");
    }

    // 🧹 Optional reset between levels or tests
    public void ResetAll()
    {
        GoldCount = 0;
        ObjectPlaceCount = 0;
        BricksPlaced = 0;
        UndoCount = 0;
        Score = 100;
        RecalculateScore();
    }
}
