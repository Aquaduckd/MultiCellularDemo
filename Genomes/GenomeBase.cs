namespace MultiCellularDemo.Genomes;

public abstract class GenomeBase
{
    public abstract OutputState Evaluate(InputState input);

    /// <summary>Initializes this genome with random values (e.g. random gene values).</summary>
    public abstract void InitializeRandom();

    /// <summary>Returns a new genome that is a mutated copy of this one. When mutationRateNorm is provided (0-1), neural genomes use it to scale mutation rate.</summary>
    public abstract GenomeBase CloneMutate(float? mutationRateNorm = null);

    /// <summary>Returns a new genome by crossing this one with another. If the other genome is a different type, implementations may fall back to CloneMutate of this.</summary>
    public abstract GenomeBase Crossover(GenomeBase other);

    /// <summary>Returns gene names and current values for display (e.g. inspector). Default empty.</summary>
    public virtual System.Collections.Generic.IReadOnlyDictionary<string, float> GetGeneValues() =>
        new System.Collections.Generic.Dictionary<string, float>();

    /// <summary>Returns network layout and weights for drawing, or null if this genome is not a neural network.</summary>
    public virtual NetworkLayout? GetNetworkLayout() => null;
}
