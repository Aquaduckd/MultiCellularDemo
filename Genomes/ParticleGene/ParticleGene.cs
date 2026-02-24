using MultiCellularDemo;

namespace MultiCellularDemo.Genomes.ParticleGene;

public class ParticleGene
{
    public float Value { get; set; }
    public float Min { get; set; }
    public float Max { get; set; }
    public float MutationChance { get; set; }
    /// <summary>Mutation strength as a fraction of the gene's range (e.g. 0.2 = 20%).</summary>
    public float MutationStrength { get; set; }

    /// <summary>Creates a gene with the given range, random value, and default mutation chance (0.1) and strength (0.2).</summary>
    public static ParticleGene Create(float min, float max)
    {
        var g = new ParticleGene
        {
            Min = min,
            Max = max,
            MutationChance = 0.1f,
            MutationStrength = 0.2f,
            Value = min + Simulation.RunRng.NextSingle() * (max - min)
        };
        return g;
    }

    /// <summary>Returns a copy of the gene.</summary>
    public static ParticleGene Clone(ParticleGene g)
    {
        return new ParticleGene
        {
            Value = g.Value,
            Min = g.Min,
            Max = g.Max,
            MutationChance = g.MutationChance,
            MutationStrength = g.MutationStrength
        };
    }

    /// <summary>Returns a copy of the gene with mutation applied.</summary>
    public static ParticleGene Mutate(ParticleGene g)
    {
        var copy = Clone(g);
        if (Simulation.RunRng.NextSingle() >= g.MutationChance)
            return copy;
        float range = g.Max - g.Min;
        float delta = (2f * Simulation.RunRng.NextSingle() - 1f) * range * g.MutationStrength;
        copy.Value = Math.Clamp(copy.Value + delta, g.Min, g.Max);
        return copy;
    }
}
