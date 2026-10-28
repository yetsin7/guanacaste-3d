namespace Guanacaste3D.Core.Rendering;

/// <summary>Why a plan differs from what the person asked for. The interface turns each code into a sentence.</summary>
public enum PlanNote
{
    /// <summary>The quality was chosen automatically from the hardware.</summary>
    ChosenAutomatically,
    /// <summary>Running on battery: fewer cores and a lower quality level to save energy and heat.</summary>
    BatteryLowered,
    /// <summary>The image would not fit the memory budget, so it is rendered at a smaller size.</summary>
    OutputResolutionReduced,
    /// <summary>The scene geometry alone is close to the memory budget.</summary>
    SceneNearBudget
}

/// <summary>
/// Hardware-aware choices behind the AUTO option: which quality level and how many cores to use, and the largest image
/// that fits in memory. <see cref="Configure"/> applies them to the render settings.
/// </summary>
public sealed record QualityPlan
{
    public RenderQuality Quality { get; init; }
    public ResourceMode Resources { get; init; }
    public int OutputWidth { get; init; }
    public int OutputHeight { get; init; }
    public int TargetSamples { get; init; }
    public int Threads { get; init; }
    /// <summary>Run workers below normal priority so the interface and other programs stay responsive.</summary>
    public bool LowPriorityThreads { get; init; } = true;
    /// <summary>Planned memory use of the image buffers plus the scene.</summary>
    public long EstimatedBytes { get; init; }
    public IReadOnlyList<PlanNote> Notes { get; init; } = [];

    /// <summary>
    /// Whether this level reuses bounced light through the radiance cache. The cache blurs light by up to one cell, which
    /// measurably helps interiors and previews and costs a little accuracy outdoors at high budgets, so it is used for
    /// drafts and standard renders and left off where the image must be unbiased (see docs/render/ADR-006).
    /// </summary>
    public bool UsesRadianceCache => Quality is RenderQuality.Draft or RenderQuality.Standard;

    /// <summary>Applies the plan's decisions to settings built by <see cref="RenderPresets.Lighting"/>.</summary>
    public RenderSettings Configure(RenderSettings settings) => settings with
    {
        MaxThreads = Threads,
        UseRadianceCache = UsesRadianceCache
    };
}

/// <summary>
/// Turns a requested quality (or AUTO), the hardware and the scene size into a plan that fits in memory and leaves the
/// machine usable. It is a starting point: the render session still reports what it actually measures.
/// </summary>
public static class QualityPlanner
{
    /// <summary>Approximate bytes per triangle for the hierarchy plus its triangle copy.</summary>
    public const int BytesPerSceneTriangle = 120;

    /// <summary>Largest share of available memory a render may plan to use.</summary>
    public const double MemoryShare = 0.5;

    /// <summary>Smallest image the planner will shrink to before giving up.</summary>
    private const int MinimumWidth = 160;

    /// <param name="requested">The quality the person chose, or null for AUTO.</param>
    public static QualityPlan Plan(RenderQuality? requested, HardwareProfile hardware, int outputWidth, int outputHeight,
        long sceneTriangles = 0, long sceneBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        if (outputWidth < 1 || outputHeight < 1) throw new ArgumentOutOfRangeException(nameof(outputWidth));
        var notes = new List<PlanNote>();
        RenderQuality quality;
        if (requested is null)
        {
            quality = AutomaticQuality(hardware);
            notes.Add(PlanNote.ChosenAutomatically);
        }
        else
        {
            quality = requested.Value;
        }

        var resources = ResourceMode.Balanced;
        if (hardware.IsOnBattery)
        {
            resources = ResourceMode.Eco;
            if (quality > RenderQuality.Standard) quality = RenderQuality.Standard;
            notes.Add(PlanNote.BatteryLowered);
        }

        var budget = (long)(hardware.AvailableMemoryBytes * MemoryShare);
        var sceneCost = sceneBytes + sceneTriangles * BytesPerSceneTriangle;
        if (sceneCost > budget * 0.6) notes.Add(PlanNote.SceneNearBudget);

        var width = outputWidth;
        var height = outputHeight;
        // Working buffers for the image plus the 4-byte output.
        long Cost(int w, int h) => sceneCost + RenderSettings.EstimateImageBytes(w, h) + (long)w * h * 4;
        var reduced = false;
        while (Cost(width, height) > budget && width > MinimumWidth && height > MinimumWidth * 9 / 16)
        {
            width = (int)(width * 0.85);
            height = (int)(height * 0.85);
            reduced = true;
        }
        if (reduced) notes.Add(PlanNote.OutputResolutionReduced);

        var cores = Math.Max(1, hardware.LogicalCores);
        // Plan against the supplied hardware, not the current process's CPU quota.
        // RenderPresets.Threads uses Environment.ProcessorCount for live render defaults,
        // which is unrelated to simulated profiles and can vary across CI runners.
        var threads = resources switch
        {
            ResourceMode.Eco => Math.Max(1, cores / 2),
            ResourceMode.Balanced => Math.Max(1, cores - 1),
            _ => cores
        };
        return new QualityPlan
        {
            Quality = quality,
            Resources = resources,
            OutputWidth = width,
            OutputHeight = height,
            TargetSamples = RenderPresets.Profile(quality).TargetSamples,
            Threads = threads,
            LowPriorityThreads = quality != RenderQuality.Final,
            EstimatedBytes = Cost(width, height),
            Notes = notes
        };
    }

    /// <summary>
    /// Standard is the sweet spot for most machines; strong ones start at High, and weak ones at Draft so the first image
    /// arrives quickly. AUTO never picks Final, which is the person's decision.
    /// </summary>
    public static RenderQuality AutomaticQuality(HardwareProfile hardware)
    {
        var gigabytes = hardware.AvailableMemoryBytes / 1073741824.0;

        // TODO(human): decide how AUTO should weigh the machine. Today it counts logical cores and free memory, which is
        // a proxy: two 16-thread laptops can differ 3x in speed. HardwareProfile.MicrobenchmarkMegaTests holds a measured
        // throughput (0 when Detect() ran without measure: true; about 3 000 on the development machine). Replace or
        // combine the thresholds below so AUTO reflects real speed, and keep tests/.../render-infrastructure-checks.cs
        // ("AUTO picks High … Standard … Draft") passing, or update it to your new rule.
        if (hardware.LogicalCores >= 12 && gigabytes >= 8) return RenderQuality.High;
        if (hardware.LogicalCores >= 6 && gigabytes >= 4) return RenderQuality.Standard;
        return RenderQuality.Draft;
    }
}
