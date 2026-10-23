using System.Numerics;
using System.Runtime.CompilerServices;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core.Rendering;

/// <summary>A pinhole camera in Z-up model coordinates.</summary>
public readonly record struct RenderCamera(Vector3 Position, Vector3 Target, float VerticalFieldOfViewDegrees)
{
    /// <summary>Frames the mesh bounds from a south-east, elevated three-quarter viewpoint.</summary>
    public static RenderCamera FrameBounds(Vector3 min, Vector3 max, float azimuthDegrees = -50,
        float elevationDegrees = 28, float distanceScale = 1)
    {
        var center = (min + max) / 2;
        var radius = MathF.Max(3, (max - min).Length() / 2);
        var azimuth = azimuthDegrees * MathF.PI / 180;
        var elevation = elevationDegrees * MathF.PI / 180;
        var direction = new Vector3(
            MathF.Cos(elevation) * MathF.Cos(azimuth),
            MathF.Cos(elevation) * MathF.Sin(azimuth),
            MathF.Sin(elevation));
        const float fieldOfView = 45;
        var distance = radius / MathF.Sin(fieldOfView * MathF.PI / 360) * 1.05f * Math.Clamp(distanceScale, 0.05f, 20);
        return new RenderCamera(center + direction * distance, center, fieldOfView);
    }
}

/// <summary>Lighting and quality controls for the path tracer.</summary>
public sealed record RenderSettings
{
    /// <summary>Direction pointing from the scene toward the sun (normalized internally).</summary>
    public Vector3 SunDirection { get; init; } = new(-0.45f, -0.55f, 0.70f);
    public float SunIntensity { get; init; } = 3.2f;
    /// <summary>Angular radius of the sun disk in degrees; larger values give softer shadows.</summary>
    public float SunAngularRadiusDegrees { get; init; } = 0.8f;
    public float SkyIntensity { get; init; } = 1.0f;
    public int MaxBounces { get; init; } = 3;
    public float Exposure { get; init; } = 1.0f;
    /// <summary>Worker threads per pass; zero or less uses every core.</summary>
    public int MaxThreads { get; init; }
}

/// <summary>
/// Progressive, multi-threaded unidirectional path tracer for BIM scenes. Each call to
/// <see cref="RenderPass"/> adds one sample per pixel to an accumulation buffer, so the
/// image converges while the user watches and can be stopped at any time.
/// </summary>
public sealed class PathTracer
{
    private readonly Bvh _bvh;
    private readonly SurfaceKind[] _kinds;
    private readonly Vector3[] _normals;
    private readonly Vector3[] _accumulation;
    private readonly RenderSettings _settings;
    private readonly Vector3 _sunDirection;
    private readonly float _sunCosine;
    private RenderCamera _camera;

    public int Width { get; }
    public int Height { get; }
    public int SampleCount { get; private set; }

    public PathTracer(SceneMesh mesh, int width, int height, RenderCamera camera, RenderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (width is < 1 or > 8192 || height is < 1 or > 8192)
            throw new ArgumentOutOfRangeException(nameof(width), "Render size must be 1–8192 pixels.");
        Width = width;
        Height = height;
        _bvh = new Bvh(mesh);
        _kinds = [.. mesh.TriangleKinds];
        _normals = new Vector3[_kinds.Length];
        for (var triangle = 0; triangle < _kinds.Length; triangle++)
            _normals[triangle] = _bvh.Normal(triangle);
        _accumulation = new Vector3[width * height];
        _settings = settings ?? new RenderSettings();
        _sunDirection = Vector3.Normalize(_settings.SunDirection);
        _sunCosine = MathF.Cos(_settings.SunAngularRadiusDegrees * MathF.PI / 180);
        _camera = camera;
    }

    /// <summary>Changes the viewpoint and restarts accumulation.</summary>
    public void SetCamera(RenderCamera camera)
    {
        _camera = camera;
        Reset();
    }

    /// <summary>Discards accumulated samples.</summary>
    public void Reset()
    {
        Array.Clear(_accumulation);
        SampleCount = 0;
    }

    /// <summary>Adds one jittered sample to every pixel, using all available cores.</summary>
    public void RenderPass(CancellationToken cancellationToken = default)
    {
        var forward = Vector3.Normalize(_camera.Target - _camera.Position);
        var worldUp = MathF.Abs(forward.Z) > 0.999f ? Vector3.UnitY : Vector3.UnitZ;
        var right = Vector3.Normalize(Vector3.Cross(forward, worldUp));
        var up = Vector3.Cross(right, forward);
        var halfHeight = MathF.Tan(_camera.VerticalFieldOfViewDegrees * MathF.PI / 360);
        var halfWidth = halfHeight * Width / Height;
        var frame = SampleCount;

        Parallel.For(0, Height, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _settings.MaxThreads > 0 ? _settings.MaxThreads : -1
        }, row =>
        {
            var random = new FastRandom((uint)(row * 9781 + frame * 6271 + 1));
            for (var column = 0; column < Width; column++)
            {
                var x = (2 * (column + random.NextFloat()) / Width - 1) * halfWidth;
                var y = (1 - 2 * (row + random.NextFloat()) / Height) * halfHeight;
                var direction = Vector3.Normalize(forward + right * x + up * y);
                _accumulation[row * Width + column] += Trace(_camera.Position, direction, ref random);
            }
        });
        SampleCount++;
    }

    /// <summary>Returns tone-mapped, gamma-encoded pixels in BGRA32 order for display.</summary>
    public byte[] ToBgra32()
    {
        var pixels = new byte[Width * Height * 4];
        var scale = SampleCount == 0 ? 0 : _settings.Exposure / SampleCount;
        for (var index = 0; index < _accumulation.Length; index++)
        {
            var color = _accumulation[index] * scale;
            pixels[index * 4] = Encode(color.Z);
            pixels[index * 4 + 1] = Encode(color.Y);
            pixels[index * 4 + 2] = Encode(color.X);
            pixels[index * 4 + 3] = 255;
        }
        return pixels;
    }

    /// <summary>Returns the linear (pre-tone-mapping) mean radiance for one pixel.</summary>
    public Vector3 LinearPixel(int column, int row) =>
        SampleCount == 0 ? Vector3.Zero : _accumulation[row * Width + column] / SampleCount;

    /// <summary>Follows one camera path, adding direct sun light at every diffuse vertex.</summary>
    private Vector3 Trace(Vector3 origin, Vector3 direction, ref FastRandom random)
    {
        var radiance = Vector3.Zero;
        var throughput = Vector3.One;
        var specularOrigin = true;
        for (var bounce = 0; bounce <= _settings.MaxBounces; bounce++)
        {
            var hit = new RayHit { Distance = float.MaxValue };
            if (!_bvh.Intersect(origin, direction, 1e-4f, ref hit))
            {
                radiance += throughput * Sky(direction, includeSun: specularOrigin);
                break;
            }

            var kind = _kinds[hit.Triangle];
            var normal = _normals[hit.Triangle];
            if (Vector3.Dot(normal, direction) > 0) normal = -normal;
            var position = origin + direction * hit.Distance;
            var offsetOrigin = position + normal * 1e-3f;

            if (kind == SurfaceKind.Glass)
            {
                // Thin architectural glazing: Schlick reflection, otherwise straight transmission.
                var cosine = MathF.Abs(Vector3.Dot(direction, normal));
                var reflectance = 0.04f + 0.96f * MathF.Pow(1 - cosine, 5);
                if (random.NextFloat() < reflectance)
                {
                    direction = Vector3.Reflect(direction, normal);
                    origin = offsetOrigin;
                }
                else
                {
                    throughput *= new Vector3(0.86f, 0.92f, 0.94f);
                    origin = position - normal * 1e-3f;
                }
                specularOrigin = true;
                continue;
            }

            var albedo = Albedo(kind, position);
            radiance += throughput * albedo * DirectSun(offsetOrigin, normal, ref random);

            // Russian roulette after the first bounces keeps cost bounded without bias.
            if (bounce >= 2)
            {
                var survival = MathF.Min(0.95f, MathF.Max(throughput.X, MathF.Max(throughput.Y, throughput.Z)));
                if (random.NextFloat() > survival) break;
                throughput /= survival;
            }
            throughput *= albedo;
            direction = CosineSampleHemisphere(normal, ref random);
            origin = offsetOrigin;
            specularOrigin = false;
        }
        return radiance;
    }

    /// <summary>Samples the finite sun disk and returns its Lambertian contribution (divided by π).</summary>
    private Vector3 DirectSun(Vector3 origin, Vector3 normal, ref FastRandom random)
    {
        var lightDirection = SampleCone(_sunDirection, _sunCosine, ref random);
        var cosine = Vector3.Dot(normal, lightDirection);
        if (cosine <= 0 || _bvh.Occluded(origin, lightDirection, 1e-4f, float.MaxValue)) return Vector3.Zero;
        return SunColor * (_settings.SunIntensity * cosine);
    }

    private static readonly Vector3 SunColor = new(1.0f, 0.93f, 0.82f);

    /// <summary>Analytic daylight sky: horizon haze, blue zenith, and an optional visible sun disk.</summary>
    private Vector3 Sky(Vector3 direction, bool includeSun)
    {
        var altitude = MathF.Max(direction.Z, -0.2f);
        var zenith = new Vector3(0.24f, 0.45f, 0.85f);
        var horizon = new Vector3(0.78f, 0.84f, 0.90f);
        var ground = new Vector3(0.32f, 0.30f, 0.27f);
        var sky = altitude >= 0
            ? Vector3.Lerp(horizon, zenith, MathF.Pow(altitude, 0.55f))
            : Vector3.Lerp(horizon, ground, MathF.Min(1, -altitude * 5));
        var sunAlignment = MathF.Max(0, Vector3.Dot(direction, _sunDirection));
        sky += SunColor * (0.35f * MathF.Pow(sunAlignment, 24)); // forward-scattering glow
        if (includeSun && sunAlignment > _sunCosine) sky += SunColor * 40f;
        return sky * _settings.SkyIntensity;
    }

    /// <summary>Returns base reflectance; the ground uses a subtle procedural tile so scale is readable.</summary>
    private static Vector3 Albedo(SurfaceKind kind, Vector3 position) => kind switch
    {
        SurfaceKind.Wall => new Vector3(0.80f, 0.78f, 0.73f),
        SurfaceKind.Floor => new Vector3(0.56f, 0.55f, 0.52f),
        SurfaceKind.Ground => ((int)MathF.Floor(position.X) + (int)MathF.Floor(position.Y) & 1) == 0
            ? new Vector3(0.30f, 0.40f, 0.24f)
            : new Vector3(0.28f, 0.37f, 0.22f),
        _ => new Vector3(0.7f)
    };

    /// <summary>Cosine-weighted direction around a normal (pdf = cosθ/π, cancels the BRDF).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 CosineSampleHemisphere(Vector3 normal, ref FastRandom random)
    {
        var r1 = random.NextFloat();
        var r2 = random.NextFloat();
        var phi = 2 * MathF.PI * r1;
        var radius = MathF.Sqrt(r2);
        var (tangent, bitangent) = Basis(normal);
        return Vector3.Normalize(tangent * (radius * MathF.Cos(phi)) + bitangent * (radius * MathF.Sin(phi)) +
                                 normal * MathF.Sqrt(MathF.Max(0, 1 - r2)));
    }

    /// <summary>Uniformly samples a direction inside a cone around an axis.</summary>
    private static Vector3 SampleCone(Vector3 axis, float cosineMax, ref FastRandom random)
    {
        var cosine = 1 - random.NextFloat() * (1 - cosineMax);
        var sine = MathF.Sqrt(MathF.Max(0, 1 - cosine * cosine));
        var phi = 2 * MathF.PI * random.NextFloat();
        var (tangent, bitangent) = Basis(axis);
        return tangent * (sine * MathF.Cos(phi)) + bitangent * (sine * MathF.Sin(phi)) + axis * cosine;
    }

    /// <summary>Builds an orthonormal basis (Duff et al. 2017, branchless).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector3 Tangent, Vector3 Bitangent) Basis(Vector3 n)
    {
        var sign = MathF.CopySign(1, n.Z);
        var a = -1 / (sign + n.Z);
        var b = n.X * n.Y * a;
        return (new Vector3(1 + sign * n.X * n.X * a, sign * b, -sign * n.X),
                new Vector3(b, sign + n.Y * n.Y * a, -n.Y));
    }

    /// <summary>ACES filmic approximation (Narkowicz) followed by sRGB encoding.</summary>
    private static byte Encode(float linear)
    {
        var x = MathF.Max(0, linear);
        var mapped = x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f);
        mapped = Math.Clamp(mapped, 0, 1);
        var srgb = mapped <= 0.0031308f ? 12.92f * mapped : 1.055f * MathF.Pow(mapped, 1 / 2.4f) - 0.055f;
        return (byte)(srgb * 255 + 0.5f);
    }
}

/// <summary>Small xorshift generator; one per worker row keeps rendering lock-free.</summary>
internal struct FastRandom(uint seed)
{
    private uint _state = seed == 0 ? 0x9E3779B9u : seed * 747796405u + 2891336453u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NextFloat()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (_state >> 8) * (1f / 16777216f);
    }
}
