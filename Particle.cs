using System.Numerics;
using MultiCellularDemo.Genomes;
using Raylib_cs;

namespace MultiCellularDemo;

public class Particle
{
    public const float Radius = 5f;
    /// <summary>Max distance for building NearbyCells in input (covers influence and binding range).</summary>
    const float NearbyRadius = 1000f;
    /// <summary>Bonds are automatically broken when partner distance exceeds this.</summary>
    public const float MaxBondDistance = 200f;
    /// <summary>Scale for force: OutputState ForceX/ForceY (0-1, 0.5=neutral) are scaled by this.</summary>
    const float ForceScale = 500f;
    /// <summary>Hue in OutputState is 0-1; we store in degrees 0-360 for drawing.</summary>
    const float HueScale = 360f;
    /// <summary>Interval (seconds) between applying genome output hue to live particle; enables live hue signalling.</summary>
    public const float HueTickRate = 0.1f;
    /// <summary>Spring: BondStrength (0-1) is scaled by this for stiffness.</summary>
    const float SpringStiffnessScale = 100f;
    /// <summary>Spring: SpringDistance (0-1) maps to rest length in [Min, Max].</summary>
    const float SpringRestLengthMin = 10f, SpringRestLengthMax = 100f;
    /// <summary>Input normalization: bond age / this -> 0-1 (capped at 1).</summary>
    const float BondAgeNormScale = 10f;
    /// <summary>Minimum bond age (seconds) before reproduction is allowed. Max is BondAgeNormScale.</summary>
    public const float MinBondReproductionTime = 0.5f;
    /// <summary>OutputState.MaxBondingPartners (0-1) is scaled to this integer range.</summary>
    public const int MaxBondingPartnersMin = 1, MaxBondingPartnersMax = 10;
    /// <summary>Mutation rate for offspring: genome outputs 0-1, scaled to [Min, Max]. Used by neural genomes.</summary>
    public const float MinMutationRate = 0.01f;
    public const float MaxMutationRate = 0.5f;
    /// <summary>Velocity is clamped to this magnitude and normalized to 0-1 for genome input.</summary>
    public const float MaxVelocityCap = 1600f;
    /// <summary>Particle age (seconds) / this -> AgeNorm 0-1 (capped at 1).</summary>
    const float AgeNormScale = 60f;
    /// <summary>White outline is only drawn when zoom >= this (zoomed out = no outline so colors are visible).</summary>
    const float MinZoomForOutline = 0.4f;

    static int _nextId;
    /// <summary>Stable id for deterministic ordering (e.g. pair keys). Assigned at creation.</summary>
    public int Id { get; }

    private Vector2 position;
    private Vector2 velocity;
    private Vector2 force;
    private List<Bond> bonds = new();
    private GenomeBase genome;
    private float hue;
    private bool hueInitialized;
    /// <summary>Seconds accumulated toward next hue tick (genome output applied every HueTickRate).</summary>
    private float _hueTickAccumulator;
    /// <summary>Time alive in seconds (accumulated each Update).</summary>
    private float age;

    public Vector2 Position => position;
    public float Hue => hue;
    public GenomeBase Genome => genome;
    public IReadOnlyList<Bond> Bonds => bonds;
    /// <summary>Current velocity magnitude (for selection prioritization).</summary>
    public float Speed => velocity.Length();
    /// <summary>Current velocity (for selection prioritization, e.g. moving away from border).</summary>
    public Vector2 Velocity => velocity;
    /// <summary>Result of the last Evaluate call (for inspector display).</summary>
    public OutputState? LastOutput { get; private set; }
    /// <summary>Generation number (0 = initial population, 1+ = offspring).</summary>
    public int Generation { get; set; }

    public Particle(Vector2 position, GenomeBase genome, int generation = 2)
    {
        Id = ++_nextId;
        this.position = position;
        this.genome = genome;
        Generation = generation;
    }

    public void AddBond(Bond bond) => bonds.Add(bond);

    public void RemoveBondTo(Particle other)
    {
        var b = bonds.FirstOrDefault(x => x.Partner == other);
        if (b == null) return;
        bonds.Remove(b);
        var onOther = other.bonds.FirstOrDefault(x => x.Partner == this);
        if (onOther != null) other.bonds.Remove(onOther);
    }

    public void Update(Context context, float dt)
    {
        float foodCount = (float)context.GetFoodAt(position);
        var input = new InputState
        {
            SelfCell = this,
            FoodCount = (int)foodCount,
            FoodCountNorm = Math.Clamp(foodCount / Context.MaxFood, 0f, 1f),
            VelocityXNorm = Math.Clamp(velocity.X / MaxVelocityCap * 0.5f + 0.5f, 0f, 1f),
            VelocityYNorm = Math.Clamp(velocity.Y / MaxVelocityCap * 0.5f + 0.5f, 0f, 1f),
            SelfHueNorm = (hue / HueScale) % 1f,
            AgeNorm = Math.Clamp(age / AgeNormScale, 0f, 1f)
        };
        foreach (var other in context.Particles)
        {
            if (other == this) continue;
            float d = Vector2.Distance(position, other.Position);
            if (d <= NearbyRadius)
            {
                input.NearbyCells.Add(other);
                Vector2 toOther = other.Position - position;
                float relXNorm = 0.5f;
                float relYNorm = 0.5f;
                if (d >= 0.0001f)
                {
                    Vector2 dir = toOther / d;
                    relXNorm = Math.Clamp(dir.X * 0.5f + 0.5f, 0f, 1f);
                    relYNorm = Math.Clamp(dir.Y * 0.5f + 0.5f, 0f, 1f);
                }
                input.NearbyNorm.Add((
                    Math.Clamp(d / MaxBondDistance, 0f, 1f),
                    (other.Hue / HueScale) % 1f,
                    relXNorm,
                    relYNorm));
            }
        }
        foreach (var b in bonds)
        {
            input.Bonds.Add(b);
            float bDist = Vector2.Distance(position, b.Partner.Position);
            Vector2 toPartner = b.Partner.Position - position;
            float relXNorm = 0.5f;
            float relYNorm = 0.5f;
            if (bDist >= 0.0001f)
            {
                Vector2 dir = toPartner / bDist;
                relXNorm = Math.Clamp(dir.X * 0.5f + 0.5f, 0f, 1f);
                relYNorm = Math.Clamp(dir.Y * 0.5f + 0.5f, 0f, 1f);
            }
            input.BondsNorm.Add(
                ((b.Partner.Hue / HueScale) % 1f,
                Math.Clamp(b.BondAge / BondAgeNormScale, 0f, 1f),
                Math.Clamp(bDist / MaxBondDistance, 0f, 1f),
                relXNorm,
                relYNorm));
        }

        var output = genome.Evaluate(input);

        LastOutput = output;
        if (!hueInitialized)
        {
            hue = output.Hue * HueScale;
            hueInitialized = true;
            _hueTickAccumulator = 0f;
        }
        else
        {
            _hueTickAccumulator += dt;
            if (_hueTickAccumulator >= HueTickRate)
            {
                _hueTickAccumulator = 0f;
                hue = output.Hue * HueScale;
            }
        }
        if (output.NearbyAttraction != null && output.NearbyAttraction.Length >= input.NearbyCells.Count)
        {
            for (int i = 0; i < input.NearbyCells.Count; i++)
            {
                var other = input.NearbyCells[i];
                Vector2 toOther = other.Position - position;
                float d = toOther.Length();
                if (d < 0.0001f) continue;
                Vector2 dir = toOther / d;
                float strength = (output.NearbyAttraction[i] - 0.5f) * 2f * ForceScale;
                force += dir * strength;
            }
        }
        else
        {
            force += new Vector2((output.ForceX - 0.5f) * 2f * ForceScale, (output.ForceY - 0.5f) * 2f * ForceScale);
        }

        var foodCenter = context.GetNearestFoodCellCenter(position);
        if (foodCenter is { } fc)
        {
            Vector2 toFood = fc - position;
            float d = toFood.Length();
            if (d >= 0.0001f)
            {
                Vector2 dir = toFood / d;
                float strength = (output.NearbyFoodAttraction - 0.5f) * 2f * ForceScale;
                force += dir * strength;
            }
        }

        float stiffness = output.BondStrength * SpringStiffnessScale;
        float restLength = output.SpringDistance * (SpringRestLengthMax - SpringRestLengthMin) + SpringRestLengthMin;
        foreach (var bond in bonds)
        {
            var toPartner = bond.Partner.Position - position;
            float d = toPartner.Length();
            if (d < 0.0001f) continue;
            Vector2 dir = toPartner / d;
            float stretch = d - restLength;
            force += dir * (stiffness * stretch);
        }

        foreach (var t in output.UnbondTargets)
            RemoveBondTo(t);

        if (output.ReproduceWithTarget != null)
        {
            var partner = output.ReproduceWithTarget;
            var bond = bonds.FirstOrDefault(b => b.Partner == partner);
            if (bond != null)
            {
                float requiredSeconds = MinBondReproductionTime + output.ReproductionBondTimeNorm * (BondAgeNormScale - MinBondReproductionTime);
                if (bond.BondAge >= requiredSeconds && bond.BondAge <= BondAgeNormScale)
                {
                    var birthPosition = (position + partner.Position) * 0.5f;
                    if (context.GetFoodAt(birthPosition) >= 1f)
                        context.AddIntendedReproduction(this, partner);
                }
            }
        }

        foreach (var t in output.BindTargets)
            context.AddIntendedBind(this, t);

        foreach (var b in bonds)
            b.BondAge += dt;
        age += dt;
    }

    public void ApplyForces(float dt)
    {
        velocity += force * dt;
        float speed = velocity.Length();
        if (speed > MaxVelocityCap && speed > 0.0001f)
            velocity *= MaxVelocityCap / speed;
        position += velocity * dt;
        force = Vector2.Zero;

        var toRemove = new List<Particle>();
        foreach (var b in bonds)
        {
            if (Vector2.Distance(position, b.Partner.Position) > MaxBondDistance)
                toRemove.Add(b.Partner);
        }
        foreach (var other in toRemove)
            RemoveBondTo(other);
    }

    public void DrawBonds()
    {
        foreach (var bond in bonds)
        {
            if (Id >= bond.Partner.Id) continue;
            float mixHue = MeanHue(hue, bond.Partner.Hue);
            Color bondColor = HsvToRgb(mixHue, 1f, 1f);
            Raylib.DrawLineEx(position, bond.Partner.Position, 2f, bondColor);
        }
    }

    public void Draw(float zoomLevel)
    {
        Color color = HsvToRgb(hue, 1f, 1f);
        Raylib.DrawCircleV(position, Radius, color);
        if (zoomLevel >= MinZoomForOutline)
            Raylib.DrawCircleLines((int)position.X, (int)position.Y, Radius + 2f, Color.White);
    }

    static float MeanHue(float h1, float h2)
    {
        float r1 = h1 * (MathF.PI / 180f), r2 = h2 * (MathF.PI / 180f);
        float sumC = MathF.Cos(r1) + MathF.Cos(r2);
        float sumS = MathF.Sin(r1) + MathF.Sin(r2);
        float rad = MathF.Atan2(sumS, sumC);
        float deg = rad * (180f / MathF.PI);
        return deg < 0 ? deg + 360f : deg;
    }

    static Color HsvToRgb(float hDeg, float s, float v)
    {
        float h = hDeg / 60f;
        if (h < 0) h += 6f;
        if (h >= 6f) h -= 6f;
        int i = (int)MathF.Floor(h);
        float f = h - i;
        float p = v * (1f - s);
        float q = v * (1f - s * f);
        float t = v * (1f - s * (1f - f));
        (float r, float g, float b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };
        return new Color((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)255);
    }
}
