namespace MultiCellularDemo.Genomes;

public class Genome : GenomeBase
{
    public override OutputState Evaluate(InputState input) => new OutputState();

    public override void InitializeRandom() { }

    public override GenomeBase CloneMutate(float? mutationRateNorm = null) => new Genome();

    public override GenomeBase Crossover(GenomeBase other) =>
        other is Genome ? new Genome() : CloneMutate(null);
}
