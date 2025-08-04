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

        float vSpacing = 45f;
        float layerSpacing = 240f;
        Vector2 origin = new Vector2(100, 100);
        Vector2 labelOffset = new Vector2(20, 0);

        Vector2[] inputPos = new Vector2[_inputCount];
        Vector2[] hiddenPos = new Vector2[_hiddenCount];
        Vector2[] outputPos = new Vector2[_outputCount];

        for (int i = 0; i < _inputCount; i++)
            inputPos[i] = origin + new Vector2(0, i * vSpacing);
        for (int i = 0; i < _hiddenCount; i++)
            hiddenPos[i] = origin + new Vector2(layerSpacing, i * vSpacing);
        for (int i = 0; i < _outputCount; i++)
            outputPos[i] = origin + new Vector2(2 * layerSpacing, i * vSpacing);

        // 1. Draw lines (behind everything)
        for (int i = 0; i < _inputCount; i++)
            for (int j = 0; j < _hiddenCount; j++)
                DrawWeightedLine(inputPos[i], hiddenPos[j], _weightsInputHidden[i, j]);

        for (int i = 0; i < _hiddenCount; i++)
            for (int j = 0; j < _outputCount; j++)
                DrawWeightedLine(hiddenPos[i], outputPos[j], _weightsHiddenOutput[i, j]);

        // 2. Draw labels
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

        // 3. Draw awesome-looking nodes
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
        DrawCircle(position, glowRadius, glowColor); // soft glow

        DrawCircle(position, NodeRadius, baseColor); // main node

        float innerRadius = NodeRadius * 0.4f;
        Color innerColor = baseColor.Inverted().Darkened(0.4f).Lightened(activation);
        DrawCircle(position, innerRadius, innerColor); // center dot

        // Outline
        DrawArc(position, NodeRadius + 2f, 0, Mathf.Tau, 32, Colors.White with { A = 0.2f }, 1.5f);
    }

    private void DrawWeightedLine(Vector2 from, Vector2 to, float weight)
{
    float absWeight = Mathf.Abs(weight);

    // 🎨 Vibrant colors
    Color color = weight > 0
        ? new Color(0.2f, 1f, 0.2f) // Bright green
        : new Color(1f, 0.2f, 0.2f); // Bright red

    // 💡 Enhanced alpha and glow
    color.A = Mathf.Clamp(Mathf.Pow(absWeight, 0.8f), 0.4f, 1f); // more nonlinear vividness

    // 📏 Exponential thickness
    float width = Mathf.Clamp(Mathf.Pow(absWeight, 0.9f) * 20f, 2f, 20f);

    // ✨ Optional glow effect (pseudo-shadow)
    Color glowColor = color with { A = color.A * 0.2f };
    DrawLine(from + new Vector2(1, 1), to + new Vector2(1, 1), glowColor, width + 2f);

    // 🔶 Main vibrant line
    DrawLine(from, to, color, width);
}

}
