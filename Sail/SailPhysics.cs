using Godot;
using System;

public class SailPhysics
{
    private struct EdgeConstraint
    {
        public int A;
        public int B;

        public float RestLength;
        public float Compliance;

        public float Lambda;
    }

    private readonly Vector3[] _restPositions;
    private readonly Vector3[] _forces;
    private readonly float[] _inverseMasses;

    private readonly bool[] _fixed;

    private readonly EdgeConstraint[] _edges;

    private readonly int[] _triangles;

    private readonly float _stiffness;
    private readonly float _massPerArea;
    private readonly float _airDensity;

    private readonly int _iterations;

    public Vector3 Wind { get; set; }

    /// <summary>
    /// Total force that the sail applies to the mast/boom.
    /// Apply this to the ship.
    /// </summary>
    // public Vector3 ForceOnShip { get; private set; }


    public Vector3[] Positions { get; }

    public Vector3[] Velocities { get; }

    public float[] Masses { get; }


    public SailPhysics(
        Vector3[] positions,
        int[] triangles,
        (int A, int B)[] edges,
        bool[] fixedVertices,
        float massPerArea,
        float stiffness,
        float airDensity,
        int iterations = 10)
    {
        _restPositions = (Vector3[])positions.Clone();

        Positions = (Vector3[])positions.Clone();
        Velocities = new Vector3[positions.Length];
        _forces = new Vector3[positions.Length];

        Masses = new float[positions.Length];
        _inverseMasses = new float[positions.Length];

        _fixed = (bool[])fixedVertices.Clone();

        _triangles = triangles;

        _massPerArea = massPerArea;
        _stiffness = stiffness;
        _airDensity = airDensity;

        _iterations = Math.Max(1, iterations);

        // -----------------------------------------------------
        // Calculate vertex masses from triangle areas.
        // -----------------------------------------------------

        CalculateMasses();

        // -----------------------------------------------------
        // Calculate edge rest lengths and compliance.
        // -----------------------------------------------------

        _edges = new EdgeConstraint[edges.Length];

        CalculateEdges(edges);

        Wind = Vector3.Zero;
    }


    private void CalculateMasses()
    {
        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int a = _triangles[i];
            int b = _triangles[i + 1];
            int c = _triangles[i + 2];

            Vector3 ab = _restPositions[b] - _restPositions[a];
            Vector3 ac = _restPositions[c] - _restPositions[a];

            float area = 0.5f * ab.Cross(ac).Length();

            float triangleMass = area * _massPerArea;
            float vertexMass = triangleMass / 3.0f;

            Masses[a] += vertexMass;
            Masses[b] += vertexMass;
            Masses[c] += vertexMass;
        }

        for (int i = 0; i < Masses.Length; i++)
        {
            // Fixed vertices still need their physical mass.
            // It is used when calculating the reaction force.
            _inverseMasses[i] =
                Masses[i] > 0.0f
                    ? 1.0f / Masses[i]
                    : 0.0f;
        }
    }

    private void CalculateEdges((int A, int B)[] edges) // THIS LOOKS SUS
    {
        for (int i = 0; i < edges.Length; i++)
        {
            int a = edges[i].A;
            int b = edges[i].B;

            float restLength = _restPositions[a].DistanceTo(_restPositions[b]);

            float adjacentArea = GetAdjacentTriangleArea(a, b);

            /*
             * Stiffness is a 2D membrane stiffness [N/m].
             *
             * Converting it to an edge spring stiffness:
             *
             *     k = Stiffness * A / L²
             *
             * A/L² is dimensionless, so k remains N/m.
             *
             * This makes the edge stiffness depend on the
             * local mesh geometry rather than simply using the
             * same spring constant for every edge.
             */
            float edgeStiffness = 0.0f;

            if (restLength > 0.000001f)
            {
                edgeStiffness =
                    _stiffness *
                    adjacentArea /
                    (restLength * restLength);
            }

            float compliance =
                edgeStiffness > 0.0f
                    ? 1.0f / edgeStiffness
                    : 0.0f;

            _edges[i] = new EdgeConstraint
            {
                A = a,
                B = b,
                RestLength = restLength,
                Compliance = compliance,
                Lambda = 0.0f
            };
        }
    }


    private float GetAdjacentTriangleArea(int a, int b) // THIS LOOKS SUS
    {
        float area = 0.0f;

        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int t0 = _triangles[i];
            int t1 = _triangles[i + 1];
            int t2 = _triangles[i + 2];

            bool containsA =
                t0 == a ||
                t1 == a ||
                t2 == a;

            bool containsB =
                t0 == b ||
                t1 == b ||
                t2 == b;

            if (!containsA || !containsB) continue;

            Vector3 ab = _restPositions[t1] - _restPositions[t0];
            Vector3 ac = _restPositions[t2] - _restPositions[t0];

            area += 0.5f * ab.Cross(ac).Length();
        }

        return area;
    }


    public void Simulate(float delta)
    {
        if (delta <= 0.0f) return;

        // -----------------------------------------------------
        // 1. Clear forces.
        // -----------------------------------------------------

        Array.Clear(_forces, 0, _forces.Length);

        // -----------------------------------------------------
        // 2. Aerodynamics.
        // -----------------------------------------------------

        ApplyAerodynamicForces();

        // -----------------------------------------------------
        // 3. Predict positions.
        // -----------------------------------------------------

        Vector3[] oldPositions = (Vector3[])Positions.Clone();

        for (int i = 0; i < Positions.Length; i++)
        {
            // THIS MAY NOT BE NECESSARY
            if (_fixed[i])
            {
                Positions[i] = _restPositions[i];
                Velocities[i] = Vector3.Zero;
                continue;
            }

            Vector3 acceleration = _forces[i] * _inverseMasses[i];

            Velocities[i] += acceleration * delta;

            Positions[i] += Velocities[i] * delta;
        }

        // -----------------------------------------------------
        // 4. XPBD.
        // -----------------------------------------------------

        // XPBD lambdas are per time step, so reset them here.
        for (int i = 0; i < _edges.Length; i++)
        {
            _edges[i].Lambda = 0;
            // EdgeConstraint edge = _edges[i];
            // edge.Lambda = 0.0f;
            // _edges[i] = edge;
        }

        Vector3[] fixedLambdas = new Vector3[Positions.Length];

        for (int iteration = 0; iteration < _iterations; iteration++)
        {
            // ---------------------------------------------
            // Edge constraints
            // ---------------------------------------------

            for (int i = 0; i < _edges.Length; i++)
            {
                SolveEdgeConstraint(ref _edges[i], delta);
            }

            // ---------------------------------------------
            // Fixed constraints
            // ---------------------------------------------

            for (int i = 0; i < Positions.Length; i++)
            {
                if (!_fixed[i]) continue;

                Vector3 lambda = SolveFixedConstraint(i);

                fixedLambdas[i] += lambda;
            }
        }

        // -----------------------------------------------------
        // 5. Calculate new velocities.
        // -----------------------------------------------------

        for (int i = 0; i < Positions.Length; i++)
        {
            Velocities[i] = (Positions[i] - oldPositions[i]) / delta;
        }

        // Fixed particles have zero velocity.
        for (int i = 0; i < Positions.Length; i++) // THIS MAY NOT BE NECESSARY
        {
            if (_fixed[i]) Velocities[i] = Vector3.Zero;
        }

        // -----------------------------------------------------
        // 6. Calculate force transferred to ship.
        // -----------------------------------------------------

        // ForceOnShip = Vector3.Zero;

        // for (int i = 0; i < _positions.Length; i++)
        // {
        //     if (!_fixed[i]) continue;

        //     /*
        //      * Lambda is an impulse-like quantity.
        //      *
        //      * lambda / dt² gives the constraint force on
        //      * the sail.
        //      *
        //      * The ship receives the opposite force.
        //      */
        //     ForceOnShip -= fixedLambdas[i] / (delta * delta);
        // }
    }


    private void ApplyAerodynamicForces()
    {
        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int a = _triangles[i];
            int b = _triangles[i + 1];
            int c = _triangles[i + 2];

            Vector3 p0 = Positions[a];
            Vector3 p1 = Positions[b];
            Vector3 p2 = Positions[c];

            Vector3 cross = (p1 - p0).Cross(p2 - p0);

            float doubleArea = cross.Length();

            if (doubleArea < 0.000001f) continue;

            float area = doubleArea * 0.5f;

            Vector3 normal = cross / doubleArea;

            // Average velocity of the triangle.
            Vector3 triangleVelocity =
                (Velocities[a] +
                 Velocities[b] +
                 Velocities[c]) / 3.0f;

            /*
             * Wind relative to the moving triangle.
             */
            Vector3 relativeWind = Wind - triangleVelocity;

            /*
             * Only the component normal to the sail
             * produces aerodynamic pressure in this simple
             * drag-only model.
             */
            float normalSpeed = relativeWind.Dot(normal);

            /*
             * Dynamic pressure:
             *
             *     q = 0.5 * rho * v²
             *
             * Keep the sign of normalSpeed so that the force
             * automatically points in the correct direction.
             */
            Vector3 force =
                0.5f *
                _airDensity *
                Mathf.Abs(normalSpeed) *
                normalSpeed *
                area *
                normal;

            // Distribute triangle force equally.
            Vector3 vertexForce = force / 3.0f;

            _forces[a] += vertexForce;
            _forces[b] += vertexForce;
            _forces[c] += vertexForce;
        }
    }


    private void SolveEdgeConstraint(ref EdgeConstraint edge, float delta)
    {
        int a = edge.A;
        int b = edge.B;

        Vector3 difference = Positions[b] - Positions[a];

        float length = difference.Length();

        if (length < 0.000001f) return; // MAY NOT BE NECESSARY

        Vector3 direction = difference / length;

        /*
         * C(x) = currentLength - restLength
         */
        float constraint = length - edge.RestLength;

        /*
         * Gradients:
         *
         * dC/dxa = -n
         * dC/dxb = +n
         */
        Vector3 gradientA = -direction; // MAYBE SIGN IS WRONG
        Vector3 gradientB = direction; // MAYBE SIGN IS WRONG

        float weight = (_inverseMasses[a] * gradientA.LengthSquared()) + (_inverseMasses[b] * gradientB.LengthSquared());

        /*
         * XPBD:
         *
         * alphaHat = alpha / dt²
         */
        float alpha = edge.Compliance / (delta * delta);

        float denominator = weight + alpha;

        if (denominator <= 0.0f) return; // MAY NOT BE NECESSARY

        float deltaLambda =
            (-constraint - (alpha * edge.Lambda)) /
            denominator;

        edge.Lambda += deltaLambda;

        /*
         * x += inverseMass * gradient * deltaLambda
         */
        Positions[a] +=
            _inverseMasses[a] *
            gradientA *
            deltaLambda;

        Positions[b] +=
            _inverseMasses[b] *
            gradientB *
            deltaLambda;
    }


    private Vector3 SolveFixedConstraint(int index)
    {
        /*
         * C(x) = x - xRest
         *
         * Gradient is simply the identity.
         */
        Vector3 constraint = Positions[index] - _restPositions[index];

        float inverseMass = _inverseMasses[index];

        /*
         * Fixed constraints have zero compliance.
         */
        float denominator = inverseMass;

        if (denominator <= 0.0f) // MAY NOT BE NECESSARY
        {
            Positions[index] = _restPositions[index];

            return Vector3.Zero;
        }

        /*
         * Vector-valued constraint.
         *
         * lambda = -C / w
         */
        Vector3 lambda = -constraint / denominator;

        /*
         * Position correction.
         */
        Positions[index] += inverseMass * lambda;

        return lambda;
    }
}