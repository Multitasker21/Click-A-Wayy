using Godot;
using System;
using System.Collections.Generic;

public partial class LandmarkRenderer : Control
{
    
    public List<List<Vector2>> AllHands = new();  // List of hands, each with 21 Vector2s


    public void UpdateMultipleHands(List<List<Vector2>> hands)
    {
        AllHands = hands;
        QueueRedraw();  // Triggers _Draw() to refresh the screen
    }

    public override void _Draw()
    {
        // Clear background 
        DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0, 0, 0, 0), true);

        // Define hand bone structure
        int[][] fingers = new int[][]
        {
            new int[] { 0, 1, 2, 3, 4 },      // Thumb
            new int[] { 0, 5, 6, 7, 8 },      // Index
            new int[] { 0, 9, 10, 11, 12 },   // Middle
            new int[] { 0, 13, 14, 15, 16 },  // Ring
            new int[] { 0, 17, 18, 19, 20 }   // Pinky
        };

        if (AllHands == null)
            return;

        foreach (var hand in AllHands)
        {
            if (hand == null || hand.Count != 21)
                continue;

            // Draw finger bones
            foreach (var finger in fingers)
            {
                for (int i = 0; i < finger.Length - 1; i++)
                {
                    DrawLine(hand[finger[i]], hand[finger[i + 1]], Colors.Red, 2);
                }
            }

            // Draw joints
            foreach (var point in hand)
            {
                DrawCircle(point, 4, Colors.White);
            }
        }
    }

}
