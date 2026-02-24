using System.Numerics;
using MultiCellularDemo;
using MultiCellularDemo.Genomes;

namespace MultiCellularDemo.Genomes.NeuralGenome;

/// <summary>Genome implemented as a feedforward neural net. Maps a fixed encoding of InputState to OutputState. No hard-coded logic; weights evolve.</summary>
public class NeuralGenome : GenomeBase
{
    const int InputDim = 71;
    const int HiddenDim = 16;
    const int OutputDim = 36;
    const int MaxBondsEncoded = 5;
    const int MaxNearbyEncoded = 10;

    /// <summary>Input to hidden: [HiddenDim, InputDim].</summary>
    private float[] W1 { get; }
    /// <summary>Hidden bias.</summary>
    private float[] B1 { get; }
    /// <summary>Hidden to output: [OutputDim, HiddenDim].</summary>
    private float[] W2 { get; }
    /// <summary>Output bias.</summary>
    private float[] B2 { get; }

    /// <summary>Last forward-pass activations per layer [input, hidden, output] for network viz.</summary>
    private float[][] _lastActivations { get; set; } = Array.Empty<float[]>();

    public NeuralGenome()
    {
        W1 = new float[HiddenDim * InputDim];
        B1 = new float[HiddenDim];
        W2 = new float[OutputDim * HiddenDim];
        B2 = new float[OutputDim];
    }

    private NeuralGenome(float[] w1, float[] b1, float[] w2, float[] b2)
    {
        W1 = w1;
        B1 = b1;
        W2 = w2;
        B2 = b2;
    }

    public override OutputState Evaluate(InputState input)
    {
        var self = input.SelfCell;
        float[] x = EncodeInput(input);
        float[] hidden = new float[HiddenDim];
        for (int i = 0; i < HiddenDim; i++)
        {
            float sum = B1[i];
            for (int j = 0; j < InputDim; j++)
                sum += W1[i * InputDim + j] * x[j];
            hidden[i] = MathF.Tanh(sum);
        }
        float[] outArr = new float[OutputDim];
        for (int i = 0; i < OutputDim; i++)
        {
            float sum = B2[i];
            for (int j = 0; j < HiddenDim; j++)
                sum += W2[i * HiddenDim + j] * hidden[j];
            outArr[i] = sum;
        }

        _lastActivations = new[] { (float[])x.Clone(), (float[])hidden.Clone(), (float[])outArr.Clone() };

        float Squash(float v) => MathF.Tanh(v) * 0.5f + 0.5f;
        var nearbyAttraction = new float[MaxNearbyEncoded];
        for (int i = 0; i < MaxNearbyEncoded; i++)
            nearbyAttraction[i] = Squash(outArr[i]);
        float hue = Squash(outArr[10]);
        float bondStrength = Squash(outArr[11]);
        float springDistance = Squash(outArr[12]);
        float maxPartners = Squash(outArr[13]);

        var unbondTargets = new List<Particle>();
        for (int i = 0; i < input.Bonds.Count && i < MaxBondsEncoded; i++)
        {
            if (MathF.Tanh(outArr[14 + i * 2]) > 0f)
                unbondTargets.Add(input.Bonds[i].Partner);
        }

        Particle? reproduceWithTarget = null;
        float bestReproduce = 0.5f;
        for (int i = 0; i < input.Bonds.Count && i < MaxBondsEncoded; i++)
        {
            float score = MathF.Tanh(outArr[14 + i * 2 + 1]) * 0.5f + 0.5f;
            if (score > bestReproduce)
            {
                bestReproduce = score;
                reproduceWithTarget = input.Bonds[i].Partner;
            }
        }

        var bindTargets = new List<Particle>();
        for (int i = 0; i < input.NearbyCells.Count && i < MaxNearbyEncoded; i++)
        {
            var other = input.NearbyCells[i];
            if (other == self) continue;
            if (input.Bonds.Exists(b => b.Partner == other)) continue;
            float score = MathF.Tanh(outArr[24 + i]) * 0.5f + 0.5f;
            if (score > 0.5f)
                bindTargets.Add(other);
        }

        return new OutputState
        {
            ForceX = 0.5f,
            ForceY = 0.5f,
            NearbyAttraction = nearbyAttraction,
            Hue = hue,
            BondStrength = bondStrength,
            SpringDistance = springDistance,
            MaxBondingPartners = maxPartners,
            UnbondTargets = unbondTargets,
            ReproduceWithTarget = reproduceWithTarget,
            BindTargets = bindTargets,
            MutationRateNorm = Squash(outArr[34]),
            NearbyFoodAttraction = Squash(outArr[35])
        };
    }

    static float[] EncodeInput(InputState input)
    {
        float[] x = new float[InputDim];
        int idx = 0;
        x[idx++] = input.VelocityXNorm;
        x[idx++] = input.VelocityYNorm;
        x[idx++] = input.SelfHueNorm;
        x[idx++] = input.AgeNorm;
        x[idx++] = input.FoodCountNorm;
        x[idx++] = Math.Clamp(input.Bonds.Count / 10f, 0f, 1f);
        for (int i = 0; i < MaxBondsEncoded; i++)
        {
            if (i < input.BondsNorm.Count)
            {
                var (partnerHueNorm, ageNorm, distanceNorm, relXNorm, relYNorm) = input.BondsNorm[i];
                x[idx++] = partnerHueNorm;
                x[idx++] = ageNorm;
                x[idx++] = distanceNorm;
                x[idx++] = relXNorm;
                x[idx++] = relYNorm;
            }
            else { x[idx++] = 0f; x[idx++] = 0f; x[idx++] = 0f; x[idx++] = 0.5f; x[idx++] = 0.5f; }
        }
        for (int i = 0; i < MaxNearbyEncoded; i++)
        {
            if (i < input.NearbyNorm.Count)
            {
                var (distanceNorm, hueNorm, relXNorm, relYNorm) = input.NearbyNorm[i];
                x[idx++] = hueNorm;
                x[idx++] = distanceNorm;
                x[idx++] = relXNorm;
                x[idx++] = relYNorm;
            }
            else { x[idx++] = 0f; x[idx++] = 0f; x[idx++] = 0.5f; x[idx++] = 0.5f; }
        }
        return x;
    }

    public override void InitializeRandom()
    {
        var rng = Simulation.RunRng;
        for (int i = 0; i < W1.Length; i++)
            W1[i] = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
        for (int i = 0; i < B1.Length; i++)
            B1[i] = (float)(rng.NextDouble() * 2 - 1) * 0.2f;
        for (int i = 0; i < W2.Length; i++)
            W2[i] = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
        for (int i = 0; i < B2.Length; i++)
            B2[i] = (float)(rng.NextDouble() * 2 - 1) * 0.2f;
    }

    public override GenomeBase CloneMutate(float? mutationRateNorm = null)
    {
        float effectiveRate = Particle.MinMutationRate + (mutationRateNorm ?? 0.5f) * (Particle.MaxMutationRate - Particle.MinMutationRate);
        const float mutationStrength = 0.2f;
        var rng = Simulation.RunRng;
        float[] w1 = new float[W1.Length];
        for (int i = 0; i < W1.Length; i++)
            w1[i] = W1[i] + (rng.NextSingle() < effectiveRate ? (rng.NextSingle() * 2f - 1f) * mutationStrength : 0f);
        float[] b1 = (float[])B1.Clone();
        for (int i = 0; i < b1.Length; i++)
            b1[i] += (rng.NextSingle() < effectiveRate ? (rng.NextSingle() * 2f - 1f) * mutationStrength : 0f);
        float[] w2 = new float[W2.Length];
        for (int i = 0; i < W2.Length; i++)
            w2[i] = W2[i] + (rng.NextSingle() < effectiveRate ? (rng.NextSingle() * 2f - 1f) * mutationStrength : 0f);
        float[] b2 = (float[])B2.Clone();
        for (int i = 0; i < b2.Length; i++)
            b2[i] += (rng.NextSingle() < effectiveRate ? (rng.NextSingle() * 2f - 1f) * mutationStrength : 0f);
        return new NeuralGenome(w1, b1, w2, b2);
    }

    static void CrossoverArrays(float[] a, float[] b, float[] outArr, Random rng)
    {
        for (int i = 0; i < outArr.Length; i++)
            outArr[i] = rng.Next(2) == 0 ? a[i] : b[i];
    }

    public override GenomeBase Crossover(GenomeBase other)
    {
        if (other is not NeuralGenome o) return CloneMutate(null);
        var rng = Simulation.RunRng;
        var w1 = new float[W1.Length];
        var b1 = new float[B1.Length];
        var w2 = new float[W2.Length];
        var b2 = new float[B2.Length];
        CrossoverArrays(W1, o.W1, w1, rng);
        CrossoverArrays(B1, o.B1, b1, rng);
        CrossoverArrays(W2, o.W2, w2, rng);
        CrossoverArrays(B2, o.B2, b2, rng);
        return new NeuralGenome(w1, b1, w2, b2);
    }

    public override System.Collections.Generic.IReadOnlyDictionary<string, float> GetGeneValues()
    {
        var d = new System.Collections.Generic.Dictionary<string, float>();
        d["Weights"] = W1.Length + W2.Length;
        d["Biases"] = B1.Length + B2.Length;
        return d;
    }

    public override NetworkLayout? GetNetworkLayout()
    {
        var layerSizes = new[] { InputDim, HiddenDim, OutputDim };
        var weights = new[] { (float[])W1.Clone(), (float[])W2.Clone() };
        var biases = new[] { (float[])B1.Clone(), (float[])B2.Clone() };
        var activations = _lastActivations.Length == 3
            ? new[] { (float[])_lastActivations[0].Clone(), (float[])_lastActivations[1].Clone(), (float[])_lastActivations[2].Clone() }
            : null;
        return new NetworkLayout(layerSizes, weights, biases, activations);
    }
}
