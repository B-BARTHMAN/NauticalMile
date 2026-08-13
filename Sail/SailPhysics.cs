using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public class SailPhysics
{
    private struct Edge(int a, int b, float restLength)
    {
        public int A = a;
        public int B = b;
        public float RestLength = restLength;
    }

    private readonly Vector3[] _restPositions;
    private readonly Vector3[] _forces;
    private readonly float[] _masses;
    private readonly bool[] _fixed;

    private readonly int[] _triangles;
    private readonly List<Edge> _edges = [];

    private readonly float _massPerArea;
    private readonly float _stiffness;
    private readonly float _damping;
    private readonly float _airDensity;

    private readonly Curve _liftCurve;
    private readonly Curve _dragCurve;

    // private readonly Vector3 _boomDirection;

    public Vector3[] Positions { get; }
    public Vector3[] Velocities { get; }

    public Vector3 MastForce { get; private set; }
    public Vector3 BoomForce { get; private set; }

    public Vector3 Wind { get; set; }

    public SailPhysics(
    Vector3[] restPositions,
    int[] triangles,
    bool[] fixedVertices,
    float massPerArea,
    float stiffness,
    float damping,
    float airDensity,
    Curve liftCurve,
    Curve dragCurve)
    {
        _restPositions = (Vector3[])restPositions.Clone();
        Positions = (Vector3[])restPositions.Clone();

        _triangles = (int[])triangles.Clone();

        Velocities = new Vector3[restPositions.Length];
        _forces = new Vector3[restPositions.Length];
        _masses = new float[restPositions.Length];

        _fixed = (bool[])fixedVertices.Clone();

        _massPerArea = massPerArea;
        _stiffness = stiffness;
        _damping = damping;
        _airDensity = airDensity;

        _liftCurve = liftCurve;
        _dragCurve = dragCurve;

        CalculateMasses();
        BuildEdges();
    }

    // ---------------------------------------------------------
    // Initialization
    // ---------------------------------------------------------
    private void CalculateMasses()
    {
        Array.Fill(_masses, 0.0f);

        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int a = _triangles[i];
            int b = _triangles[i + 1];
            int c = _triangles[i + 2];

            Vector3 ab = _restPositions[b] - _restPositions[a];
            Vector3 ac = _restPositions[c] - _restPositions[a];

            float area = ab.Cross(ac).Length() * 0.5f;

            float triangleMass = area * _massPerArea;
            float vertexMass = triangleMass / 3.0f;

            _masses[a] += vertexMass;
            _masses[b] += vertexMass;
            _masses[c] += vertexMass;
        }
    }

    private void BuildEdges()
    {
        HashSet<(int, int)> uniqueEdges = [];

        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int[] v = [_triangles[i], _triangles[i + 1], _triangles[i + 2]];

            foreach (var (a, b) in new[] { (v[0], v[1]), (v[1], v[2]), (v[2], v[0]) })
            {
                var edge = (Math.Min(a, b), Math.Max(a, b));

                if (uniqueEdges.Add(edge))
                    _edges.Add(new Edge(a, b, _restPositions[a].DistanceTo(_restPositions[b])));
            }
        }
    }

    // ---------------------------------------------------------
    // Simulation
    // ---------------------------------------------------------
    public void Simulate(float delta)
    {
        Array.Fill(_forces, Vector3.Zero);

        CalculateAerodynamicForces();
        CalculateSpringForces();

        Integrate(delta);
    }

    // ---------------------------------------------------------
    // Aerodynamics
    // ---------------------------------------------------------
    private void CalculateAerodynamicForces()
    {
        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int a = _triangles[i];
            int b = _triangles[i + 1];
            int c = _triangles[i + 2];

            Vector3 pa = Positions[a];
            Vector3 pb = Positions[b];
            Vector3 pc = Positions[c];

            Vector3 cross = (pb - pa).Cross(pc - pa);
            float doubleArea = cross.Length();

            if (doubleArea < 0.000001f) continue;

            float area = doubleArea * 0.5f;
            Vector3 normal = cross / doubleArea;

            Vector3 relativeWind = Wind - ((Velocities[a] + Velocities[b] + Velocities[c]) / 3f);

            float speed = relativeWind.Length();

            if (speed < 0.001f) continue;

            Vector3 windDirection = relativeWind / speed;

            // Angle between the sail normal and incoming wind.
            float windAngle = normal.AngleTo(windDirection);

            float liftCoefficient = _liftCurve?.SampleBaked(windAngle) ?? 0f;

            float dragCoefficient = _dragCurve?.SampleBaked(windAngle) ?? 0f;

            float dynamicPressure = 0.5f * _airDensity * speed * speed;

            Vector3 dragForce = windDirection * dynamicPressure * area * dragCoefficient;

            Vector3 liftDirection = normal - (windDirection * normal.Dot(windDirection));

            Vector3 liftForce = liftDirection.LengthSquared() > 0.000001f
                ? liftDirection.Normalized() * dynamicPressure * area * liftCoefficient
                : Vector3.Zero;

            Vector3 vertexForce = (liftForce + dragForce) / 3f;

            AddForce(a, vertexForce);
            AddForce(b, vertexForce);
            AddForce(c, vertexForce);
        }
    }

    // ---------------------------------------------------------
    // Springs
    // ---------------------------------------------------------
    private void CalculateSpringForces()
    {
        foreach (Edge edge in _edges)
        {
            Vector3 delta = Positions[edge.B] - Positions[edge.A];

            float length = delta.Length();

            Vector3 direction = delta / length;

            // Hooke's law:
            //
            // F = (L - L0) * k

            float extension = length - edge.RestLength;

            float springStiffness = _stiffness / edge.RestLength;
            Vector3 force = direction * extension * springStiffness;

            AddForce(edge.A, force);
            AddForce(edge.B, -force);
        }
    }

    // ---------------------------------------------------------
    // Integration
    // ---------------------------------------------------------
    private void Integrate(float delta)
    {
            var s = Vector3.Zero;
        for (int i = 0; i < Positions.Length; i++)
        {
            if (_fixed[i])
            {
                Positions[i] = _restPositions[i];
                Velocities[i] = Vector3.Zero;
                s += _forces[i];
                continue;
            }

            if (_masses[i] <= 0.000001f) continue;

            Vector3 acceleration = _forces[i] / _masses[i];

            Velocities[i] += acceleration * delta;

            // Simple velocity damping.
            Velocities[i] *= Mathf.Exp(-_damping * delta);

            Positions[i] += Velocities[i] * delta;
        }
        Debug.WriteLine(s);
    }

    private void AddForce(int vertex, Vector3 force)
    {
        if (_fixed[vertex])
        {
            // We don't apply the force to the fixed vertex.
            // Instead, this force contributes to the reaction
            // force that will eventually be sent to the mast/boom.
            //return;adddddadadadadadadasdadasdadadasdadasdasdasdasdasdasdasdasdasdasd
        }

        _forces[vertex] += force;
    }
}