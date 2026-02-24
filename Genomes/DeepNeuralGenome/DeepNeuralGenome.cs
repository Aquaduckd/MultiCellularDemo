using System.Numerics;
using MultiCellularDemo;
using MultiCellularDemo.Genomes;

namespace MultiCellularDemo.Genomes.DeepNeuralGenome;

/// <summary>Feedforward neural genome with multiple hidden layers. Same I/O as NeuralGenome (32 inputs, 26 outputs) but deeper topology.</summary>
public class DeepNeuralGenome : GenomeBase
{
    const int InputDim = 71;
    const int OutputDim = 36;
    const int MaxBondsEncoded = 5;
    const int MaxNearbyEncoded = 10;

    /// <summary>Layer dimensions: input, then hidden layers, then output. e.g. [71, 24, 20, 16, 36].</summary>
    static readonly int[] LayerDims = { InputDim, 24, 20, 16, OutputDim };

    /// <summary>Weight matrices: W[i] maps from LayerDims[i] to LayerDims[i+1], row-major [out, in].</summary>
    private float[][] W { get; }
    /// <summary>Bias vectors: B[i] has length LayerDims[i+1].</summary>
    private float[][] B { get; }

    private float[][] _lastActivations { get; set; } = Array.Empty<float[]>();

    public DeepNeuralGenome()
    {
        int n = LayerDims.Length - 1;
        W = new float[n][];
        B = new float[n][];
        for (int i = 0; i < n; i++)
        {
            int inSize = LayerDims[i];
            int outSize = LayerDims[i + 1];
            W[i] = new float[outSize * inSize];
            B[i] = new float[outSize];
        }
    }

    private DeepNeuralGenome(float[][] w, float[][] b)
    {
        W = w;
        B = b;
    }

    public override OutputState Evaluate(InputState input)
    {
        var self = input.SelfCell;
        float[] x = EncodeInput(input);
        var activations = new float[LayerDims.Length][];
        activations[0] = (float[])x.Clone();

        // Forward through hidden layers (tanh)
        for (int layer = 0; layer < W.Length - 1; layer++)
        {
            int inDim = LayerDims[layer];
            int outDim = LayerDims[layer + 1];
            var next = new float[outDim];
            for (int i = 0; i < outDim; i++)
            {
                float sum = B[layer][i];
                for (int j = 0; j < inDim; j++)
                    sum += W[layer][i * inDim + j] * x[j];
                next[i] = MathF.Tanh(sum);
            }
            activations[layer + 1] = (float[])next.Clone();
            x = next;
        }

        // Output layer (linear)
        int lastLayer = W.Length - 1;
        int lastIn = LayerDims[LayerDims.Length - 2];
        float[] outArr = new float[OutputDim];
        for (int i = 0; i < OutputDim; i++)
        {
            float sum = B[lastLayer][i];
            for (int j = 0; j < lastIn; j++)
                sum += W[lastLayer][i * lastIn + j] * x[j];
            outArr[i] = sum;
        }
        activations[LayerDims.Length - 1] = (float[])outArr.Clone();
        _lastActivations = activations;

        // Decode to OutputState (same semantics as NeuralGenome: 10 nearby attraction, then hue/bond/spring/maxPartners, bond slots, bind slots)
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
        for (int layer = 0; layer < W.Length; layer++)
        {
            for (int i = 0; i < W[layer].Length; i++)
                W[layer][i] = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            for (int i = 0; i < B[layer].Length; i++)
                B[layer][i] = (float)(rng.NextDouble() * 2 - 1) * 0.2f;
        }
    }

    public override GenomeBase CloneMutate(float? mutationRateNorm = null)
    {
        float effectiveRate = Particle.MinMutationRate + (mutationRateNorm ?? 0.5f) * (Particle.MaxMutationRate - Particle.MinMutationRate);
        const float mutationStrength = 0.2f;
        var rng = Simulation.RunRng;
        int n = W.Length;
        var wNew = new float[n][];
        var bNew = new float[n][];
        for (int layer = 0; layer < n; layer++)
        {
            wNew[layer] = new float[W[layer].Length];
            for (int i = 0; i < W[layer].Length; i++)
                wNew[layer][i] = W[layer][i] + (rng.NextSingle() < effectiveRate ? (rng.NextSingle() * 2f - 1f) * mutationStrength : 0f);
            bNew[layer] = (float[])B[layer].Clone();
            for (int i = 0; i < bNew[layer].Length; i++)
                bNew[layer][i] += (rng.NextSingle() < effectiveRate ? (rng.NextSingle() * 2f - 1f) * mutationStrength : 0f);
        }
        return new DeepNeuralGenome(wNew, bNew);
    }

    static void CrossoverArrays(float[] a, float[] b, float[] outArr, Random rng)
    {
        for (int i = 0; i < outArr.Length; i++)
            outArr[i] = rng.Next(2) == 0 ? a[i] : b[i];
    }

    public override GenomeBase Crossover(GenomeBase other)
    {
        if (other is not DeepNeuralGenome o) return CloneMutate(null);
        var rng = Simulation.RunRng;
        int n = W.Length;
        var wNew = new float[n][];
        var bNew = new float[n][];
        for (int layer = 0; layer < n; layer++)
        {
            wNew[layer] = new float[W[layer].Length];
            bNew[layer] = new float[B[layer].Length];
            CrossoverArrays(W[layer], o.W[layer], wNew[layer], rng);
            CrossoverArrays(B[layer], o.B[layer], bNew[layer], rng);
        }
        return new DeepNeuralGenome(wNew, bNew);
    }

    public override System.Collections.Generic.IReadOnlyDictionary<string, float> GetGeneValues()
    {
        int totalWeights = 0, totalBiases = 0;
        for (int i = 0; i < W.Length; i++)
        {
            totalWeights += W[i].Length;
            totalBiases += B[i].Length;
        }
        var d = new System.Collections.Generic.Dictionary<string, float>();
        d["Weights"] = totalWeights;
        d["Biases"] = totalBiases;
        d["Layers"] = LayerDims.Length;
        return d;
    }

    public override NetworkLayout? GetNetworkLayout()
    {
        var layerSizes = (int[])LayerDims.Clone();
        var weights = new float[W.Length][];
        var biases = new float[B.Length][];
        for (int i = 0; i < W.Length; i++)
        {
            weights[i] = (float[])W[i].Clone();
            biases[i] = (float[])B[i].Clone();
        }
        float[][]? activations = _lastActivations.Length == LayerDims.Length
            ? Array.ConvertAll(_lastActivations, a => (float[])a.Clone())
            : null;
        return new NetworkLayout(layerSizes, weights, biases, activations);
    }
}
