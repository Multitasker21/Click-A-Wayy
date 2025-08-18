using Godot;
using System;

public partial class NeuralVisualizer : Control
{
    [Export] private Font CustomFont;

    private readonly string[] _inputKeys = new[]
    {
        "RayUp", "RayDown", "RayLeft", "RayRight", "DeltaToTargetX", "DeltaToTargetY"
    };

    private readonly string[] _actions = new[]
    {
        "None", "Up", "Down", "Left", "Right"
    };

    private float[] _inputActivations;
    private float[] _hiddenActivations;
    private float[] _outputQValues;

    private float[,] _weightsInputHidden;
    private float[,] _weightsHiddenOutput;

    private int _inputCount => _inputActivations?.Length ?? 0;
    private int _hiddenCount => _hiddenActivations?.Length ?? 0;
    private int _outputCount => _outputQValues?.Length ?? 0;

    private Font _font;
    private const float NodeRadius = 10f;

    // 👇 Adjustable padding (in pixels)
    private const float Padding = 40f;

    public override void _Ready()
    {
        _font = CustomFont ?? ThemeDB.FallbackFont;
        SetProcess(true);
    }

    public void SetNetworkData(
        float[] inputActivations,
        float[] hiddenActivations,
        float[] outputQValues,
        float[,] weightsInputHidden,
        float[,] weightsHiddenOutput
    )
    {
        _inputActivations = (float[])inputActivations.Clone();
        _hiddenActivations = (float[])hiddenActivations.Clone();
        _outputQValues = (float[])outputQValues.Clone();
        _weightsInputHidden = weightsInputHidden.Clone() as float[,];
        _weightsHiddenOutput = weightsHiddenOutput.Clone() as float[,];

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_inputActivations == null || _hiddenActivations == null || _outputQValues == null)
            return;

        // === Dynamic spacing with padding ===
        float availableWidth = Size.X - 2 * Padding;
        float availableHeight = Size.Y - 2 * Padding;

        int maxNodes = Mathf.Max(_inputCount, Mathf.Max(_hiddenCount, _outputCount));

        float vSpacing = maxNodes > 1 ? availableHeight / (maxNodes - 1) : availableHeight / 2f;

        float layerSpacing = availableWidth / 2.5f;

        // Start position adjusted by padding
        Vector2 origin = new Vector2(Padding + layerSpacing * 0.3f, Padding);
        Vector2 labelOffset = new Vector2(20, 0);

        Vector2[] inputPos = new Vector2[_inputCount];
        Vector2[] hiddenPos = new Vector2[_hiddenCount];
        Vector2[] outputPos = new Vector2[_outputCount];

        for (int i = 0; i < _inputCount; i++)
            inputPos[i] = new Vector2(origin.X, Padding + (availableHeight - (_inputCount - 1) * vSpacing) / 2 + i * vSpacing);

        for (int i = 0; i < _hiddenCount; i++)
            hiddenPos[i] = new Vector2(origin.X + layerSpacing, Padding + (availableHeight - (_hiddenCount - 1) * vSpacing) / 2 + i * vSpacing);

        for (int i = 0; i < _outputCount; i++)
            outputPos[i] = new Vector2(origin.X + 2 * layerSpacing, Padding + (availableHeight - (_outputCount - 1) * vSpacing) / 2 + i * vSpacing);

        // === Draw weights ===
        for (int i = 0; i < _inputCount; i++)
            for (int j = 0; j < _hiddenCount; j++)
                DrawWeightedLine(inputPos[i], hiddenPos[j], _weightsInputHidden[i, j]);

        for (int i = 0; i < _hiddenCount; i++)
            for (int j = 0; j < _outputCount; j++)
                DrawWeightedLine(hiddenPos[i], outputPos[j], _weightsHiddenOutput[i, j]);

        // === Labels ===
        for (int i = 0; i < _inputCount; i++)
        {
            float val = Mathf.Clamp(_inputActivations[i], -1f, 1f);
            string label = i < _inputKeys.Length ? _inputKeys[i] : $"In{i}";
            DrawString(_font, inputPos[i] + labelOffset, $"{label}:{val:F1}", HorizontalAlignment.Left, -1, 15, Colors.White);
        }

        for (int i = 0; i < _hiddenCount; i++)
        {
            float act = Mathf.Clamp(_hiddenActivations[i], 0f, 1f);
            DrawString(_font, hiddenPos[i] + labelOffset, $"H{i}:{act:F2}", HorizontalAlignment.Left, -1, 15, Colors.White);
        }

        for (int i = 0; i < _outputCount; i++)
        {
            float q = _outputQValues[i];
            string label = i < _actions.Length ? _actions[i] : $"Out{i}";
            DrawString(_font, outputPos[i] + labelOffset, $"{label}:{q:F2}", HorizontalAlignment.Left, -1, 15, Colors.Yellow);
        }

        // === Nodes ===
        for (int i = 0; i < _inputCount; i++)
        {
            float val = Mathf.Clamp(_inputActivations[i], -1f, 1f);
            Color c = val >= 0 ? new Color(1, 0.5f, 0.2f).Lightened(val) : new Color(0.5f, 0.5f, 1).Darkened(-val);
            DrawEnhancedNode(inputPos[i], Mathf.Abs(val), c);
        }

        for (int i = 0; i < _hiddenCount; i++)
        {
            float act = Mathf.Clamp(_hiddenActivations[i], 0f, 1f);
            Color c = new Color(0.4f, 1f, 0.4f).Lightened(act);
            DrawEnhancedNode(hiddenPos[i], act, c);
        }

        for (int i = 0; i < _outputCount; i++)
        {
            float q = _outputQValues[i];
            float normQ = Mathf.Clamp(q / 5f, 0f, 1f);
            Color c = new Color(0.2f, 0.8f, 1f).Lightened(normQ);
            DrawEnhancedNode(outputPos[i], normQ, c);
        }
    }

    private void DrawEnhancedNode(Vector2 position, float activation, Color baseColor)
    {
        float glowRadius = NodeRadius + 6f;
        Color glowColor = baseColor with { A = 0.15f };
        DrawCircle(position, glowRadius, glowColor);

        DrawCircle(position, NodeRadius, baseColor);

        float innerRadius = NodeRadius * 0.4f;
        Color innerColor = baseColor.Inverted().Darkened(0.4f).Lightened(activation);
        DrawCircle(position, innerRadius, innerColor);

        DrawArc(position, NodeRadius + 2f, 0, Mathf.Tau, 32, Colors.White with { A = 0.2f }, 1.5f);
    }

    private void DrawWeightedLine(Vector2 from, Vector2 to, float weight)
    {
        float absWeight = Mathf.Abs(weight);

        Color color = weight > 0 ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.2f, 0.2f);
        color.A = Mathf.Clamp(Mathf.Pow(absWeight, 0.8f), 0.4f, 1f);

        float width = Mathf.Clamp(Mathf.Pow(absWeight, 0.9f) * 20f, 2f, 20f);

        Color glowColor = color with { A = color.A * 0.2f };
        DrawLine(from + new Vector2(1, 1), to + new Vector2(1, 1), glowColor, width + 2f);

        DrawLine(from, to, color, width);
    }
}
