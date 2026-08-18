using Godot;
using System.Collections.Generic;

public class SailGeometry
{
    public Vector3[] Vertices { get; private set; }
    public int[] Triangles { get; private set; }
    public (int A, int B)[] Edges { get; private set; }
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
        List<(int A, int B)> edges = [];

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

                // i == 0 -> boom
                // j == 0 -> mast
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
                int a = vertexIndices[i, j];
                int b = vertexIndices[i + 1, j];
                int c = vertexIndices[i, j + 1];

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                if (i + j < _resolution - 1)
                {
                    int d = vertexIndices[i + 1, j + 1];

                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(d);
                }
            }
        }

        Vertices = [.. vertices];
        Triangles = [.. triangles];

        // ---------------------------------------------------------
        // Unique edges
        // ---------------------------------------------------------

        HashSet<(int, int)> edgeSet = [];

        for (int i = 0; i < Triangles.Length; i += 3)
        {
            AddEdge(edgeSet, Triangles[i], Triangles[i + 1]);
            AddEdge(edgeSet, Triangles[i + 1], Triangles[i + 2]);
            AddEdge(edgeSet, Triangles[i + 2], Triangles[i]);
        }

        edges.AddRange(edgeSet);

        Edges = [.. edges];

        // ---------------------------------------------------------
        // Render mesh
        // ---------------------------------------------------------

        SurfaceTool tool = new();

        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        foreach (Vector3 vertex in Vertices)
            tool.AddVertex(vertex);

        foreach (int index in Triangles)
            tool.AddIndex(index);

        tool.GenerateNormals();

        Mesh = tool.Commit();
    }

    private static void AddEdge(
        HashSet<(int, int)> edges,
        int a,
        int b)
    {
        if (a > b) (a, b) = (b, a);
        edges.Add((a, b));
    }
}