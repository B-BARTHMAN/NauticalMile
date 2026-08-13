using Godot;
using System.Collections.Generic;

public class SailGeometry
{
    public Vector3[] Vertices { get; private set; }
    public int[] Triangles { get; private set; }
    public ArrayMesh Mesh { get; private set; }
    public bool[] Fixed { get; private set; }

    private readonly int _resolution;
    private readonly Vector3 _mast;
    private readonly Vector3 _boom;
    private readonly Curve _curve;

    public SailGeometry(
        int resolution,
        Vector3 mast,
        Vector3 boom,
        Curve curve)
    {
        _resolution = resolution;
        _mast = mast;
        _boom = boom;
        _curve = curve;

        Generate();
    }

    private void Generate()
    {
        List<Vector3> vertices = [];
        List<int> triangles = [];
        Fixed = new bool[(_resolution + 1) * (_resolution + 2) / 2];

        int[,] vertexIndices = new int[_resolution + 1, _resolution + 1];

        // ---------------------------------------------------------
        // Vertices
        // ---------------------------------------------------------

        for (int i = 0; i <= _resolution; i++)
        {
            for (int j = 0; j <= _resolution - i; j++)
            {
                float a = i / (float)_resolution;
                float b = j / (float)_resolution;

                float height = _curve?.SampleBaked(b) ?? 1.0f;

                Vector3 vertex =
                    (a * _mast * height) +
                    (b * _boom);

                int index = vertices.Count;

                vertexIndices[i, j] = index;

                vertices.Add(vertex);

                Fixed[index] = i == 0 || j == 0;
            }
        }

        // ---------------------------------------------------------
        // Triangles
        // ---------------------------------------------------------

        for (int i = 0; i < _resolution; i++)
        {
            for (int j = 0; j < _resolution - i; j++)
            {
                // First triangle
                int a = vertexIndices[i, j];
                int b = vertexIndices[i + 1, j];
                int c = vertexIndices[i, j + 1];

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                // Second triangle
                if (i + j < _resolution - 1)
                {
                    int d = vertexIndices[i + 1, j + 1];

                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(d);
                }
            }
        }

        // ---------------------------------------------------------
        // Keep the raw data for the particle simulation.
        // ---------------------------------------------------------

        Vertices = [.. vertices];
        Triangles = [.. triangles];

        // ---------------------------------------------------------
        // Build render mesh.
        // ---------------------------------------------------------
        SurfaceTool tool = new();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < Vertices.Length; i++)
        {
            tool.AddVertex(Vertices[i]);
        }

        for (int i = 0; i < Triangles.Length; i++)
        {
            tool.AddIndex(Triangles[i]);
        }

        tool.GenerateNormals();

        Mesh = tool.Commit();
    }
}