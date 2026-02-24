using System.Numerics;
using MultiCellularDemo;
using MultiCellularDemo.Genomes;

namespace MultiCellularDemo.Genomes.ParticleGene;

/// <summary>Gene-based genome. All genes and logic in 0-1; Particle scales inputs and outputs.</summary>
public class ParticleGeneGenome : GenomeBase
{
    private Dictionary<string, ParticleGene> Genes { get; } = new();

    float GetValue(string name) => Genes.TryGetValue(name, out var g) ? g.Value : 0f;

    /// <summary>Circular distance in 0-1 hue space.</summary>
    static float HueDistance01(float a, float b)
    {
        float d = MathF.Abs(a - b);
        if (d > 0.5f) d = 1f - d;
        return d;
    }

    public override OutputState Evaluate(InputState input)
    {
        var self = input.SelfCell;
        float bondPreferred = GetValue("BindingPreferredHue");
        float bondHueRange = GetValue("BindingHueRange");
        float bindRadius = GetValue("BindingRadius");
        float repBondTime = GetValue("ReproductionBondTime");
        float repPreferred = GetValue("ReproductionPreferredHue");
        float repHueRange = GetValue("ReproductionHueRange");

        var unbondTargets = new List<Particle>();
        for (int i = 0; i < input.Bonds.Count; i++)
        {
            var (partnerHueNorm, ageNorm, distanceNorm, _, _) = input.BondsNorm[i];
            if (distanceNorm > bindRadius || HueDistance01(partnerHueNorm, bondPreferred) > bondHueRange)
                unbondTargets.Add(input.Bonds[i].Partner);
        }

        Particle? reproduceWithTarget = null;
        for (int i = 0; i < input.Bonds.Count; i++)
        {
            var (partnerHueNorm, _, _, _, _) = input.BondsNorm[i];
            if (HueDistance01(partnerHueNorm, repPreferred) <= repHueRange)
            {
                reproduceWithTarget = input.Bonds[i].Partner;
                break;
            }
        }

        Vector2 force = ComputeForce(input);

        float bindPreferred = GetValue("BindingPreferredHue");
        float bindHueRange = GetValue("BindingHueRange");
        var bindTargets = new List<Particle>();
        if (bindRadius > 0f)
        {
            for (int i = 0; i < input.NearbyCells.Count; i++)
            {
                var other = input.NearbyCells[i];
                if (other == self) continue;
                var (distanceNorm, hueNorm, _, _) = input.NearbyNorm[i];
                if (distanceNorm > bindRadius) continue;
                if (input.Bonds.Exists(b => b.Partner == other)) continue;
                if (HueDistance01(hueNorm, bindPreferred) <= bindHueRange)
                    bindTargets.Add(other);
            }
        }

        float fx = Math.Clamp((force.X + 1f) * 0.5f, 0f, 1f);
        float fy = Math.Clamp((force.Y + 1f) * 0.5f, 0f, 1f);
        return new OutputState
        {
            ForceX = fx,
            ForceY = fy,
            Hue = GetValue("Hue"),
            BondStrength = GetValue("BindStrength"),
            SpringDistance = GetValue("SpringDistance"),
            MaxBondingPartners = GetValue("MaxBondingPartners"),
            BindTargets = bindTargets,
            UnbondTargets = unbondTargets,
            ReproduceWithTarget = reproduceWithTarget,
            ReproductionBondTimeNorm = GetValue("ReproductionBondTime")
        };
    }

    Vector2 ComputeForce(InputState input)
    {
        var self = input.SelfCell;
        float infRange = GetValue("InfluenceRange");
        float attHue = GetValue("AttractionHue"), attRange = GetValue("AttractionRange"), attStr = GetValue("AttractionStrength");
        float repHue = GetValue("RepulsionHue"), repRange = GetValue("RepulsionRange"), repStr = GetValue("RepulsionStrength");
        Vector2 total = Vector2.Zero;

        for (int i = 0; i < input.NearbyCells.Count; i++)
        {
            var other = input.NearbyCells[i];
            if (other == self) continue;
            var (dNorm, hueNorm, _, _) = input.NearbyNorm[i];
            if (dNorm > infRange || dNorm < 0.0001f) continue;
            Vector2 toOther = other.Position - self.Position;
            float len = toOther.Length();
            if (len < 0.0001f) continue;
            Vector2 dir = toOther / len;
            float dClamped = MathF.Max(dNorm, 0.01f);

            float attDist = HueDistance01(hueNorm, attHue);
            if (attRange > 0f && attDist <= attRange)
            {
                float falloff = 1f - attDist / attRange;
                float mag = attStr * falloff / dClamped;
                total += dir * mag;
            }

            float repDist = HueDistance01(hueNorm, repHue);
            if (repRange > 0f && repDist <= repRange)
            {
                float falloff = 1f - repDist / repRange;
                float mag = repStr * falloff / dClamped;
                total -= dir * mag;
            }
        }

        total.X = Math.Clamp(total.X, -1f, 1f);
        total.Y = Math.Clamp(total.Y, -1f, 1f);
        return total;
    }

    public override void InitializeRandom()
    {
        Genes["Hue"] = ParticleGene.Create(0f, 1f);
        Genes["AttractionHue"] = ParticleGene.Create(0f, 1f);
        Genes["AttractionRange"] = ParticleGene.Create(0f, 1f);
        Genes["AttractionStrength"] = ParticleGene.Create(0f, 1f);
        Genes["RepulsionHue"] = ParticleGene.Create(0f, 1f);
        Genes["RepulsionRange"] = ParticleGene.Create(0f, 1f);
        Genes["RepulsionStrength"] = ParticleGene.Create(0f, 1f);
        Genes["InfluenceRange"] = ParticleGene.Create(0f, 1f);
        Genes["BindingRadius"] = ParticleGene.Create(0f, 1f);
        Genes["BindingPreferredHue"] = ParticleGene.Create(0f, 1f);
        Genes["BindingHueRange"] = ParticleGene.Create(0f, 1f);
        Genes["BindStrength"] = ParticleGene.Create(0f, 1f);
        Genes["MaxBondingPartners"] = ParticleGene.Create(0f, 1f);
        Genes["SpringDistance"] = ParticleGene.Create(0f, 1f);
        Genes["ReproductionPreferredHue"] = ParticleGene.Create(0f, 1f);
        Genes["ReproductionHueRange"] = ParticleGene.Create(0f, 1f);
        Genes["ReproductionBondTime"] = ParticleGene.Create(0f, 1f);
    }

    public override GenomeBase CloneMutate(float? mutationRateNorm = null)
    {
        var g = new ParticleGeneGenome();
        foreach (var kv in Genes)
            g.Genes[kv.Key] = ParticleGene.Mutate(kv.Value);
        return g;
    }

    public override GenomeBase Crossover(GenomeBase other)
    {
        if (other is not ParticleGeneGenome o) return CloneMutate(null);
        var rng = Simulation.RunRng;
        var child = new ParticleGeneGenome();
        foreach (var key in Genes.Keys)
        {
            var a = Genes[key];
            if (!o.Genes.TryGetValue(key, out var b))
            {
                child.Genes[key] = ParticleGene.Clone(a);
                continue;
            }
            var gene = ParticleGene.Clone(rng.Next(2) == 0 ? a : b);
            child.Genes[key] = gene;
        }
        return child;
    }

    public override IReadOnlyDictionary<string, float> GetGeneValues()
    {
        var d = new Dictionary<string, float>();
        foreach (var kv in Genes)
            d[kv.Key] = kv.Value.Value;
        return d;
    }
}
