using Godot;
using System;

public partial class CircularProgressBar : Control
{
    [Export] public float Value { get; set; } = 0f;         // 0.0 to 1.0
    [Export] public Color FillColor { get; set; } = Colors.Lime;
    [Export] public float OuterRadius { get; set; } = 100f;  // outer radius of the ring
    [Export] public float InnerRadius { get; set; } = 70f;   // inner radius
    [Export] public int ArcPoints { get; set; } = 64;        // number of segments for smoothness

    public override void _Draw()
    {
        Vector2 center = GetRect().Size / 2;

        int filledPoints = Mathf.CeilToInt(Value * ArcPoints);

        // Draw the filled ring
        for (int i = 0; i < filledPoints; i++)
        {
            float t0 = (float)i / ArcPoints;
            float t1 = (float)(i + 1) / ArcPoints;

            float angle0 = -Mathf.Pi / 2 + t0 * Mathf.Tau;
            float angle1 = -Mathf.Pi / 2 + t1 * Mathf.Tau;

            Vector2 p0Inner = center + new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0)) * InnerRadius;
            Vector2 p1Inner = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * InnerRadius;
            Vector2 p0Outer = center + new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0)) * OuterRadius;
            Vector2 p1Outer = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * OuterRadius;

            DrawPolygon(new Vector2[] { p0Inner, p0Outer, p1Outer, p1Inner },
                        new Color[] { FillColor, FillColor, FillColor, FillColor });
        }

        // Draw rounded cap at the tip
        if (filledPoints > 0)
        {
            float endAngle = -Mathf.Pi / 2 + Value * Mathf.Tau;
            Vector2 tipCenter = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * ((InnerRadius + OuterRadius) / 2);
            float capRadius = (OuterRadius - InnerRadius) / 2;

            DrawCircle(tipCenter, capRadius, FillColor);
        }
    }

    public void SetProgress(float value)
    {
        Value = Mathf.Clamp(value, 0f, 1f);
        QueueRedraw();
    }

    public void AddProgress(float delta)
    {
        SetProgress(Value + delta);
    }
}
