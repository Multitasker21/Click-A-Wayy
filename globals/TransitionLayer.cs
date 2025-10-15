using Godot;
using System;

public partial class TransitionLayer : CanvasLayer
{
	private AnimationPlayer _animPlayer;
	private ColorRect _fadeRect;
	private PackedScene _targetScene;
    
	public override void _Ready()
	{
		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		_fadeRect = GetNode<ColorRect>("ColorRect");

		// Ensure ColorRect is initially transparent
		_fadeRect.Modulate = new Color(0, 0, 0, 0);
	}
    public void Reveal()
	{
		if (_targetScene != null)
		{
			GetTree().ChangeSceneToPacked(_targetScene);
			_targetScene = null;

			if (_animPlayer.HasAnimation("FadeOut"))
				_animPlayer.Play("FadeOut"); // Fade out from black
			else
				GD.PrintErr("FadeOut animation missing in AnimationPlayer!");
		}
	}

	/// <summary>
	/// Call this from anywhere to start a transition to a new scene.
	/// </summary>
	public void StartTransition(PackedScene targetScene)
	{
		_targetScene = targetScene;

		if (_animPlayer.HasAnimation("FadeIn"))
			_animPlayer.Play("FadeIn"); // Fade in to black
		else
			GD.PrintErr("FadeIn animation missing in AnimationPlayer!");
	}

	/// <summary>
	/// This is called from AnimationPlayer via a Call Method Track at the end of FadeIn.
	/// </summary>
	
}
