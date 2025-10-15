using Godot;
using System;

public partial class Intro02 : Control
{
    [Export] private VideoStreamPlayer VideoPlayerNode;
    [Export] private VideoStreamPlayer CompletionVideoNode;
    [Export] private CircularProgressBar CircularProgressBarNode;
    [Export] private RichTextLabel ProgressLabel;

    [Export] private float BaseClickFill = 0.01f;
    [Export] private float MaxClickFill = 0.03f;
    [Export] private float BaseDecayRate = 0.05f;
    [Export] private float IntensityDistance = 0.5f;

    private float intensityMultiplier = 1f;
    private const float MinMultiplier = 1f;

    private bool completionTriggered = false;

    // New: Click timer
    private float clickTimer = 0f;
    private const float MaxClickInterval = 3f; // 3 seconds
    private bool showClickWarning = false;
    private float clickWarningTimer = 0f;
    private const float ClickWarningDuration = 0.5f; // show "CLICK!!!" for 0.5 seconds

    public override void _Ready()
    {
        if (VideoPlayerNode != null)
        {
            VideoPlayerNode.Play();
            VideoPlayerNode.Finished += OnVideoFinished;
        }

        if (CircularProgressBarNode != null)
            CircularProgressBarNode.SetProgress(0f);

        UpdateProgressLabel();
    }

    private void OnVideoFinished() => VideoPlayerNode.Play();

    public override void _Process(double delta)
    {
        float deltaF = (float)delta;

        if (CircularProgressBarNode == null) return;

        float progressValue = CircularProgressBarNode.Value;

        // Intensity ramps up with progress
        intensityMultiplier = Mathf.Max(MinMultiplier, Mathf.Pow(progressValue, 2.5f) * 5f);
        intensityMultiplier *= Mathf.Lerp(1f, 2f, IntensityDistance);

        // Apply decay only if not full
        if (CircularProgressBarNode.Value < 1f)
        {
            float decay = BaseDecayRate * deltaF * intensityMultiplier;
            CircularProgressBarNode.SetProgress(Mathf.Clamp(CircularProgressBarNode.Value - decay, 0f, 1f));
        }

        // Click timer logic
        if (!completionTriggered)
        {
            clickTimer += deltaF;

            if (clickTimer >= MaxClickInterval && !showClickWarning)
            {
                showClickWarning = true;
                clickWarningTimer = 0f;
                ProgressLabel.Text = "[center]CLICK!!![/center]";
            }
        }

        // Handle click warning display
        if (showClickWarning)
        {
            clickWarningTimer += deltaF;
            if (clickWarningTimer >= ClickWarningDuration)
            {
                showClickWarning = false;
                UpdateProgressLabel(); // revert back to normal percent
                clickTimer = 0f;       // restart timer
            }
        }

        if (completionTriggered)
            FadeOutNodes(deltaF);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (CircularProgressBarNode == null) return;

            float progressValue = CircularProgressBarNode.Value;

            float fill = Mathf.Lerp(BaseClickFill, MaxClickFill, progressValue);
            fill *= intensityMultiplier;

            CircularProgressBarNode.AddProgress(fill);

            // Reset click timer and warning
            clickTimer = 0f;
            showClickWarning = false;

            UpdateProgressLabel();
        }
    }

    private void UpdateProgressLabel()
    {
        if (ProgressLabel == null || CircularProgressBarNode == null) return;

        int percent = (int)(CircularProgressBarNode.Value * 100);
        ProgressLabel.Text = $"[center] {percent}%[/center]";

        if (!completionTriggered && percent >= 100)
        {
            completionTriggered = true;
            if (CompletionVideoNode != null)
                CompletionVideoNode.Play();
        }
    }

    private void FadeOutNodes(float delta)
    {
        if (ProgressLabel != null)
        {
            Color c = ProgressLabel.SelfModulate;
            float newAlpha = Mathf.Max(c.A - delta * 1.0f, 0f);
            ProgressLabel.SelfModulate = new Color(c.R, c.G, c.B, newAlpha);
        }

        if (CircularProgressBarNode != null)
        {
            Color c = CircularProgressBarNode.SelfModulate;
            float newAlpha = Mathf.Max(c.A - delta * 1.0f, 0f);
            CircularProgressBarNode.SelfModulate = new Color(c.R, c.G, c.B, newAlpha);
        }
    }
}
