using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
    [Export] public float Speed = 80f;
    [Export] public NodePath TargetPath;
    [Export] public NodePath VisualizerPath;
    [Export] public NodePath NavAgentPath;

    [Export] public RayCast2D RayCastUp;
    [Export] public RayCast2D RayCastDown;
    [Export] public RayCast2D RayCastLeft;
    [Export] public RayCast2D RayCastRight;

    private Marker2D _target;
    private NeuralVisualizer _visualizer;
    private NavigationAgent2D _navAgent;

    private Random _random = new();
    private float _previousDistance;
    private float _reward = 0f;

    private bool _useNavigation = false;
    private float _hybridTimer = 0f;
    private float _initialQTime = 0.3f;

    public enum Action { None, Up, Down, Left, Right }
    private static readonly Action[] AllActions = { Action.None, Action.Up, Action.Down, Action.Left, Action.Right };

    private Action _currentAction;
    private float _actionTimer = 0f;
    private float _actionInterval = 0.2f;
    private bool _isRunning = false;

    // Neural Network Parameters
    private const int InputSize = 6;
    private const int HiddenSize = 10;
    private const int OutputSize = 5;

    private float[,] _weightsInputHidden = new float[InputSize, HiddenSize];
    private float[,] _weightsHiddenOutput = new float[HiddenSize, OutputSize];

    private float _learningRate = 0.01f;
    private float _discountFactor = 0.95f;
    private float _explorationRate = 0.1f;

    public override void _Ready()
    {
        _target = GetNode<Marker2D>(TargetPath);
        _visualizer = GetNode<NeuralVisualizer>(VisualizerPath);
        _navAgent = GetNode<NavigationAgent2D>(NavAgentPath);

        _navAgent.TargetDesiredDistance = 4f;
        _navAgent.PathDesiredDistance = 2f;
        _navAgent.Radius = 6f;
        _navAgent.Velocity = Vector2.Zero;
        _navAgent.TargetPosition = _target.GlobalPosition;

        _previousDistance = GlobalPosition.DistanceTo(_target.GlobalPosition);
        RandomizeWeights();
    }

    private void RandomizeWeights()
    {
        for (int i = 0; i < InputSize; i++)
            for (int j = 0; j < HiddenSize; j++)
                _weightsInputHidden[i, j] = (float)_random.NextDouble() * 0.2f - 0.1f;

        for (int i = 0; i < HiddenSize; i++)
            for (int j = 0; j < OutputSize; j++)
                _weightsHiddenOutput[i, j] = (float)_random.NextDouble() * 0.2f - 0.1f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isRunning)
            return;

        SenseEnvironment();

        if (_useNavigation)
        {
            UseNavigationAgent();
        }
        else
        {
            ChooseAction((float)delta);
            PerformAction();

            _hybridTimer -= (float)delta;
            if (_hybridTimer <= 0f)
            {
                _useNavigation = true;
                
            }
        }

        float currentDistance = GlobalPosition.DistanceTo(_target.GlobalPosition);
        float deltaDistance = _previousDistance - currentDistance;

        if (currentDistance < 4f)
        {
            _reward = 5f;
            GD.Print("🎯 Goal Reached!");
            _isRunning = false;
            Velocity = Vector2.Zero;
        }
        else if (deltaDistance > 0.1f)
        {
            _reward = 0.2f;
        }
        else if (deltaDistance < -0.1f)
        {
            _reward = -0.2f;
        }
        else
        {
            _reward = -0.05f;
        }

        if (_currentAction == Action.None && currentDistance > 8f)
            _reward -= 0.2f;

        if (IsTouchingWall())
            _reward -= 1f;

        _reward -= 0.01f;

        _previousDistance = currentDistance;

        //GD.Print($"Action: {_currentAction}, Reward: {_reward:F2}");

        LearnFromReward();
        VisualizeNeuralNetwork(_useNavigation);
        MoveAndSlide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.R)
            {
                _isRunning = true;
                _useNavigation = false;
                _hybridTimer = _initialQTime;
                
            }
            else if (key.Keycode == Key.T)
            {
                _useNavigation = !_useNavigation;
                
                _hybridTimer = _initialQTime;
            }
            else if (key.Keycode == Key.S)
            {
                _isRunning = false;
                GD.Print("⏹️ AI Stopped");
            }
        }
    }

    private void UseNavigationAgent()
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
        _navAgent.Velocity = Velocity;

        _currentAction = direction.LengthSquared() < 0.01f ? Action.None
                        : Mathf.Abs(direction.X) > Mathf.Abs(direction.Y)
                        ? (direction.X > 0 ? Action.Right : Action.Left)
                        : (direction.Y > 0 ? Action.Down : Action.Up);

        LearnFromReward();
    }

    private void SenseEnvironment()
    {
        RayCastUp.ForceRaycastUpdate();
        RayCastDown.ForceRaycastUpdate();
        RayCastLeft.ForceRaycastUpdate();
        RayCastRight.ForceRaycastUpdate();
    }

    private bool IsTouchingWall()
    {
        return RayCastLeft.IsColliding() || RayCastRight.IsColliding() ||
               RayCastUp.IsColliding() || RayCastDown.IsColliding();
    }

    private void ChooseAction(float delta)
    {
        _actionTimer -= delta;
        if (_actionTimer > 0f)
            return;

        float[] input = GetInputVector();
        float[] output = Forward(input);

        if (_random.NextDouble() < _explorationRate)
        {
            _currentAction = AllActions[_random.Next(AllActions.Length)];
        }
        else
        {
            int bestIndex = 0;
            float bestQ = float.MinValue;
            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] > bestQ)
                {
                    bestQ = output[i];
                    bestIndex = i;
                }
            }
            _currentAction = AllActions[bestIndex];
        }

        _actionTimer = _actionInterval;
    }

    private void PerformAction()
    {
        Velocity = _currentAction switch
        {
            Action.Up => Vector2.Up * Speed,
            Action.Down => Vector2.Down * Speed,
            Action.Left => Vector2.Left * Speed,
            Action.Right => Vector2.Right * Speed,
            _ => Vector2.Zero
        };
    }

    private void LearnFromReward()
    {
        float[] input = GetInputVector();
        float[] hidden = new float[HiddenSize];
        float[] output = Forward(input, hidden);

        int actionIndex = Array.IndexOf(AllActions, _currentAction);
        float targetQ = output[actionIndex] + _learningRate * (_reward + _discountFactor * Max(output) - output[actionIndex]);

        // Backpropagation (output layer)
        float error = targetQ - output[actionIndex];
        for (int j = 0; j < HiddenSize; j++)
        {
            _weightsHiddenOutput[j, actionIndex] += _learningRate * error * hidden[j];
        }

        // Backpropagation (input layer - optional for shallow updates)
    }

    private float[] Forward(float[] input)
    {
        float[] dummy = new float[HiddenSize];
        return Forward(input, dummy);
    }

    private float[] Forward(float[] input, float[] hiddenOut)
    {
        for (int j = 0; j < HiddenSize; j++)
        {
            hiddenOut[j] = 0f;
            for (int i = 0; i < InputSize; i++)
                hiddenOut[j] += input[i] * _weightsInputHidden[i, j];
            hiddenOut[j] = Mathf.Tanh(hiddenOut[j]);
        }

        float[] output = new float[OutputSize];
        for (int k = 0; k < OutputSize; k++)
        {
            output[k] = 0f;
            for (int j = 0; j < HiddenSize; j++)
                output[k] += hiddenOut[j] * _weightsHiddenOutput[j, k];
        }

        return output;
    }

    private float Max(float[] values)
    {
        float max = float.MinValue;
        foreach (float val in values)
            if (val > max) max = val;
        return max;
    }

    private float[] GetInputVector()
    {
        return new float[]
        {
            RayCastUp.IsColliding() ? 1f : 0f,
            RayCastDown.IsColliding() ? 1f : 0f,
            RayCastLeft.IsColliding() ? 1f : 0f,
            RayCastRight.IsColliding() ? 1f : 0f,
            _target.GlobalPosition.X - GlobalPosition.X,
            _target.GlobalPosition.Y - GlobalPosition.Y
        };
    }

    private void VisualizeNeuralNetwork(bool fromNav)
    {
        if (_visualizer != null)
        {
            float[] input = GetInputVector();
            float[] hidden = new float[HiddenSize];
            float[] qValues = Forward(input, hidden);

            _visualizer.SetNetworkData(input, hidden, qValues, _weightsInputHidden, _weightsHiddenOutput);
        }
    }
}
