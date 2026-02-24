using MultiCellularDemo;

namespace MultiCellularDemo.Genomes;

/// <summary>Output of genome evaluation: all numeric values in 0-1 range. Particle/simulation scale to world units.</summary>
public class OutputState
{
    public float ForceX { get; set; }
    public float ForceY { get; set; }
    /// <summary>Per-nearby attraction/repulsion (0-1, 0.5=neutral). Set by neural genomes; length matches MaxNearbyEncoded. When set, Particle uses this instead of ForceX/ForceY.</summary>
    public float[]? NearbyAttraction { get; set; }
    /// <summary>Attraction to center of nearest cell with food (0-1, 0.5=neutral). Particle adds force toward that center when set.</summary>
    public float NearbyFoodAttraction { get; set; } = 0.5f;
    public float Hue { get; set; }
    public float BondStrength { get; set; }
    public float SpringDistance { get; set; }
    /// <summary>Max bonds (0-1). Consumers scale to integer range (e.g. 3-10).</summary>
    public float MaxBondingPartners { get; set; } = 0.5f;
    /// <summary>Particles to bind with this frame (may be multiple).</summary>
    public List<Particle> BindTargets { get; set; } = new();
    /// <summary>Bond partners to unbond from this frame (may be multiple).</summary>
    public List<Particle> UnbondTargets { get; set; } = new();
    /// <summary>Partner we're reproducing with this frame, or null to not reproduce (also used for midpoint spawn and bond-age reset).</summary>
    public Particle? ReproduceWithTarget { get; set; }
    /// <summary>Required bond age for reproduction (0-1). Particle scales to [MinBondReproductionTime, BondAgeNormScale]. Default 0 = use min.</summary>
    public float ReproductionBondTimeNorm { get; set; }
    /// <summary>Desired mutation rate for offspring (0-1). Neural nets output this; scaled to [Particle.MinMutationRate, Particle.MaxMutationRate].</summary>
    public float MutationRateNorm { get; set; } = 0.5f;
}
