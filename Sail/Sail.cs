using Godot;

[Tool]
public partial class Sail : Node3D
{
    [ExportCategory("Sail")]

    [Export] public Vector3 Mast { get; set; } = new(0, 6, 0);

    [Export] public Vector3 Boom { get; set; } = new(4, 0, 0);

    [Export(PropertyHint.Range, "1,15,1")] public int Resolution { get; set; } = 3;

    [Export]
    public Curve Curve { get; set; }


    [ExportCategory("Physics")]

    [Export] public float MassPerArea { get; set; } = 1.0f;

    [Export] public float Stiffness { get; set; } = 1.0f;

    [Export] public float AirDensity { get; set; } = 1.225f;

    [Export] public Vector3 Wind { get; set; } = new(10, 0, 0);


    [ExportCategory("Editor")]

    [ExportToolButton("Regenerate Sail")]
    public Callable RegenerateButton => new(this, nameof(Regenerate));


    private SailGeometry _geometry;
    private SailPhysics _physics;
    private MeshInstance3D _meshInstance;


    public Vector3[] Vertices => _physics?.Positions ?? _geometry?.Vertices;
    public int[] Triangles => _geometry?.Triangles;


    public override void _Ready()
    {
        _meshInstance = GetNode<MeshInstance3D>("Mesh");
        Regenerate();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;

        if (_physics == null) return;

        _physics.Wind = Wind;

        _physics.Simulate((float)delta);

        UpdateMesh();
    }


    private void Regenerate()
    {
        _meshInstance ??= GetNode<MeshInstance3D>("Mesh");

        // ---------------------------------------------
        // Create initial sail.
        // ---------------------------------------------
        _geometry = new SailGeometry(
            Resolution,
            Mast,
            Boom,
            Curve
        );

        // Show initial geometry.
        _meshInstance.Mesh = _geometry.Mesh;

        // ---------------------------------------------
        // Create physics from initial geometry.
        // ---------------------------------------------
        if (!Engine.IsEditorHint())
        {
            _physics = new SailPhysics(
                _geometry.Vertices,
                _geometry.Triangles,
                _geometry.Edges,
                _geometry.Fixed,
                MassPerArea,
                Stiffness,
                AirDensity
            );
        }
    }


    private void UpdateMesh()
    {
        SurfaceTool tool = new();

        tool.Begin(Mesh.PrimitiveType.Triangles);

        // Current physics positions.
        foreach (Vector3 position in _physics.Positions) tool.AddVertex(position);

        // Original triangle topology.
        foreach (int index in _geometry.Triangles) tool.AddIndex(index);

        // Recalculate normals from the new positions.
        tool.GenerateNormals();

        _meshInstance.Mesh = tool.Commit();
    }
}