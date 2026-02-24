using MultiCellularDemo;

namespace MultiCellularDemo.Genomes;

/// <summary>Inputs available to a particle cell: self, nearby cells, bonded partners, and food count. Normalized fields (0-1) for genome use.</summary>
public class InputState
{
    public Particle SelfCell { get; set; } = null!;
    public List<Particle> NearbyCells { get; } = new();
    public List<Bond> Bonds { get; } = new();
    public int FoodCount { get; set; }

    /// <summary>Food count scaled to 0-1 (Particle defines scale).</summary>
    public float FoodCountNorm { get; set; }
    /// <summary>Velocity X and Y scaled to 0-1 (e.g. -cap..+cap -> 0..1). Single source: Particle.MaxVelocityCap.</summary>
    public float VelocityXNorm { get; set; }
    public float VelocityYNorm { get; set; }
    /// <summary>This particle's hue in 0-1 (Particle.Hue / 360).</summary>
    public float SelfHueNorm { get; set; }
    /// <summary>Particle age in 0-1 (seconds / Particle.AgeNormScale, capped at 1).</summary>
    public float AgeNorm { get; set; }
    /// <summary>Per nearby cell: (distance 0-1, hue 0-1, relX 0-1, relY 0-1). rel = unit direction from self to other, (-1..1)->(0..1). Same order as NearbyCells.</summary>
    public List<(float distanceNorm, float hueNorm, float relXNorm, float relYNorm)> NearbyNorm { get; } = new();
    /// <summary>Per bond: (partner hue 0-1, bond age 0-1, distance 0-1, relX 0-1, relY 0-1). Same order as Bonds.</summary>
    public List<(float partnerHueNorm, float ageNorm, float distanceNorm, float relXNorm, float relYNorm)> BondsNorm { get; } = new();
}
