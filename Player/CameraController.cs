using Godot;

namespace NauticalMile.Player;

public partial class CameraController : Camera3D
{
    [Export]
    private CharacterBody3D _player = null!;

    [Export]
    private float _sensitivity = 0.003f; // Radians per pixel

    private float _pitch = 0.0f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseMotion motion)
            return;

        // Rotate the player horizontally (yaw)
        _player.RotateY(-motion.Relative.X * _sensitivity);

        // Rotate the camera vertically (pitch)
        _pitch -= motion.Relative.Y * _sensitivity;
        _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-89.0f), Mathf.DegToRad(89.0f));

        Vector3 rotation = Rotation;
        rotation.X = _pitch;
        Rotation = rotation;
    }
}