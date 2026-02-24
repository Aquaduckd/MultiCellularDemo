using System.Globalization;
using System.Numerics;
using MultiCellularDemo.Genomes;
using MultiCellularDemo.Genomes.ChemistryGenome;
using MultiCellularDemo.Genomes.DeepNeuralGenome;
using MultiCellularDemo.Genomes.NeuralGenome;
using MultiCellularDemo.Genomes.ParticleGene;
using Raylib_cs;

namespace MultiCellularDemo;

public class Simulation
{
    /// <summary>Seed for this run (set by InitRun). Used for reproducible runs.</summary>
    public static int RunSeed { get; private set; }
    static Random? _runRng;
    /// <summary>Seeded RNG for this run. Use this instead of Random.Shared for all run-dependent randomness.</summary>
    public static Random RunRng => _runRng ?? Random.Shared;

    /// <summary>Initialize the run with a seed. Call before creating Simulation.</summary>
    public static void InitRun(int seed)
    {
        RunSeed = seed;
        _runRng = new Random(seed);
    }

    public const float MapSize = 10000f;
    public static Vector2 MapCenter => new(MapSize * 0.5f, MapSize * 0.5f);
    /// <summary>When selecting a particle for the camera, only consider particles at least this far from the map border (ensures selection stays on map).</summary>
    public const float MinCameraSelectionBorderDistance = 500f;
    public const float GridCellSize = Context.FoodCellSize;
    /// <summary>Max births per second (rate-based). Budget accumulates each Update(dt) and is spent per birth.</summary>
    public const float MaxBirthsPerSecond = 400f;
    public const int PopulationCap = 1200;

    /// <summary>Accumulated birth budget (increases by MaxBirthsPerSecond*dt each Update, decreases by 1 per birth). Capped so it doesn't grow unbounded.</summary>
    private float _birthBudget;

    /// <summary>Max food produced per second across the map (cells × 1/SecondsPerFood).</summary>
    public static float MaxFoodPerSecond
    {
        get
        {
            int cellsPerSide = (int)(MapSize / GridCellSize);
            int totalCells = cellsPerSide * cellsPerSide;
            return totalCells * (1f / Context.SecondsPerFood);
        }
    }

    private Context context = new();

    /// <summary>Canonical genome type for this run (e.g. "neural", "deep", "chemistry", "particlegene"). Use for UI.</summary>
    public string GenomeDisplayName { get; }

    public List<Particle> Particles => context.Particles;

    /// <summary>Number of births in the last Update.</summary>
    public int BirthsLastFrame { get; private set; }

    /// <summary>Sum of food in all cells (explored + unexplored). Unexplored in-bounds cells count as InitialFoodPerCell.</summary>
    public float TotalFood
    {
        get
        {
            int totalInBounds = (int)(MapSize / GridCellSize) * (int)(MapSize / GridCellSize);
            float inExplored = context.CellFood.Values.Sum();
            int unexploredCount = totalInBounds - context.CellFood.Count;
            return inExplored + Math.Max(0, unexploredCount) * Context.InitialFoodPerCell;
        }
    }

    /// <summary>Returns the first particle at the given world position (within Particle.Radius), or null.</summary>
    public Particle? GetParticleAt(Vector2 worldPosition)
    {
        float r = Particle.Radius;
        foreach (var p in context.Particles)
        {
            if (Vector2.Distance(p.Position, worldPosition) <= r)
                return p;
        }
        return null;
    }

    /// <summary>Iterates backwards through the particle list (newest first). Returns a particle within maxDistance of searchCenter. When searchCenter is close to a border, only considers particles within the map margin (minBorderDistance). Prioritizes particles with more bonds. If none in radius, returns the nearest to searchCenter among those with the most bonds.</summary>
    public Particle? GetParticleNearPosition(Vector2 searchCenter, float maxDistance, float minBorderDistance = 0f)
    {
        float maxDistSq = maxDistance * maxDistance;
        float minX = minBorderDistance;
        float maxX = MapSize - minBorderDistance;
        float minY = minBorderDistance;
        float maxY = MapSize - minBorderDistance;
        bool nearBorder = minBorderDistance > 0f && (
            searchCenter.X < minBorderDistance || searchCenter.X > MapSize - minBorderDistance ||
            searchCenter.Y < minBorderDistance || searchCenter.Y > MapSize - minBorderDistance);
        bool checkBorder = nearBorder;

        bool InMapWithMargin(Vector2 pos) =>
            pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY;

        Particle? bestInRadius = null;
        int bestInRadiusBonds = -1;
        Particle? bestAny = null;
        int bestAnyBonds = -1;
        float bestAnyDistSq = float.MaxValue;
        var list = context.Particles;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var p = list[i];
            if (checkBorder && !InMapWithMargin(p.Position))
                continue;
            float dSq = Vector2.DistanceSquared(p.Position, searchCenter);
            int bonds = p.Bonds.Count;
            bool inRadius = dSq <= maxDistSq;

            if (inRadius && (bestInRadius == null || bonds > bestInRadiusBonds))
            {
                bestInRadiusBonds = bonds;
                bestInRadius = p;
            }
            if (bestAny == null || bonds > bestAnyBonds || (bonds == bestAnyBonds && dSq < bestAnyDistSq))
            {
                bestAnyBonds = bonds;
                bestAnyDistSq = dSq;
                bestAny = p;
            }
        }
        return bestInRadius ?? bestAny;
    }

    public static bool CellInMapBounds((int, int) cell)
    {
        float x0 = cell.Item1 * GridCellSize, x1 = (cell.Item1 + 1) * GridCellSize;
        float y0 = cell.Item2 * GridCellSize, y1 = (cell.Item2 + 1) * GridCellSize;
        return x1 > 0 && x0 < MapSize && y1 > 0 && y0 < MapSize;
    }

    /// <summary>Resolve user-entered genome type to canonical name used for creation and display. Single source of truth.</summary>
    static string ResolveGenomeType(string? userInput)
    {
        switch (userInput?.ToLowerInvariant())
        {
            case "neural": return "neural";
            case "deep":
            case "deepneural": return "deep";
            case "chemistry": return "chemistry";
            case "particlegene":
            case "gene":
            default: return "particlegene";
        }
    }

    /// <summary>Create a new genome of the given canonical type ("neural", "deep", "chemistry", "particlegene").</summary>
    static GenomeBase CreateGenome(string resolvedType)
    {
        return resolvedType switch
        {
            "neural" => new NeuralGenome(),
            "deep" => new DeepNeuralGenome(),
            "chemistry" => new ChemistryGenome(),
            _ => new ParticleGeneGenome()
        };
    }

    public Simulation(float width = MapSize, float height = MapSize, string? genomeType = null)
    {
        GenomeDisplayName = ResolveGenomeType(genomeType);
        var rng = RunRng;
        for (int i = 0; i < PopulationCap; i++)
        {
            float x = (float)rng.NextDouble() * width;
            float y = (float)rng.NextDouble() * height;
            var genome = CreateGenome(GenomeDisplayName);
            genome.InitializeRandom();
            context.Particles.Add(new Particle(new Vector2(x, y), genome, 1));
        }
    }

    public void Update(float dt)
    {
        foreach (var p in context.Particles)
            p.Update(context, dt);

        foreach (var p in context.Particles)
        {
            var c = Context.CellKey(p.Position);
            if (CellInMapBounds(c))
                context.EnsureCellHasFood(c);
            p.ApplyForces(dt);
        }
        context.TickFood(dt);

        _birthBudget += MaxBirthsPerSecond * dt;
        if (_birthBudget > MaxBirthsPerSecond * 2f) _birthBudget = MaxBirthsPerSecond * 2f;

        var bindSet = new HashSet<(Particle, Particle)>(context.IntendedBinds);
        var processedBindPairs = new HashSet<(Particle, Particle)>();
        foreach (var (self, partner) in context.IntendedBinds)
        {
            var key = self.Id < partner.Id ? (self, partner) : (partner, self);
            if (processedBindPairs.Contains(key)) continue;
            if (!bindSet.Contains((self, partner)) || !bindSet.Contains((partner, self))) continue;
            if (self.Bonds.Any(b => b.Partner == partner)) continue;
            float maxPartnerNorm = self.LastOutput?.MaxBondingPartners ?? 0.5f;
            int range = Particle.MaxBondingPartnersMax - Particle.MaxBondingPartnersMin;
            int selfMax = (int)Math.Clamp(MathF.Round(maxPartnerNorm * range + Particle.MaxBondingPartnersMin), Particle.MaxBondingPartnersMin, Particle.MaxBondingPartnersMax);
            float partnerMaxNorm = partner.LastOutput?.MaxBondingPartners ?? 0.5f;
            int partnerMax = (int)Math.Clamp(MathF.Round(partnerMaxNorm * range + Particle.MaxBondingPartnersMin), Particle.MaxBondingPartnersMin, Particle.MaxBondingPartnersMax);
            if (self.Bonds.Count >= selfMax || partner.Bonds.Count >= partnerMax) continue;
            if (Vector2.Distance(self.Position, partner.Position) > Particle.MaxBondDistance) continue;
            processedBindPairs.Add(key);
            self.AddBond(new Bond { Self = self, Partner = partner, BondAge = 0f });
            partner.AddBond(new Bond { Self = partner, Partner = self, BondAge = 0f });
        }
        context.IntendedBinds.Clear();

        var reproSet = new HashSet<(Particle, Particle)>(context.IntendedReproductions);
        var eligible = new List<(Particle a, Particle b, float bondAge)>();
        var seenKey = new HashSet<(Particle, Particle)>();
        foreach (var (a, b) in context.IntendedReproductions)
        {
            var key = a.Id < b.Id ? (a, b) : (b, a);
            if (seenKey.Contains(key)) continue;
            if (!reproSet.Contains((a, b)) || !reproSet.Contains((b, a))) continue;
            seenKey.Add(key);
            var mid = (a.Position + b.Position) * 0.5f;
            if (context.GetFoodAt(mid) < 1f) continue;
            var aBond = a.Bonds.FirstOrDefault(x => x.Partner == b);
            float age = aBond?.BondAge ?? 0f;
            eligible.Add((a, b, age));
        }
        var processedPairs = new HashSet<(Particle, Particle)>();
        int added = 0;
        foreach (var (a, b, _) in eligible.OrderByDescending(x => x.bondAge).ThenBy(x => x.a.Id).ThenBy(x => x.b.Id))
        {
            if (_birthBudget < 1f) break;
            var key = a.Id < b.Id ? (a, b) : (b, a);
            if (processedPairs.Contains(key)) continue;
            processedPairs.Add(key);
            var mid = (a.Position + b.Position) * 0.5f;
            context.DecrementFoodAt(mid);
            var childGenome = a.Genome.Crossover(b.Genome).CloneMutate(a.LastOutput?.MutationRateNorm);
            var child = new Particle(mid, childGenome, Math.Max(a.Generation, b.Generation) + 1);
            context.Particles.Add(child);
            var aBond = a.Bonds.FirstOrDefault(x => x.Partner == b);
            if (aBond != null) aBond.BondAge = 0f;
            var bBond = b.Bonds.FirstOrDefault(x => x.Partner == a);
            if (bBond != null) bBond.BondAge = 0f;
            added++;
            _birthBudget -= 1f;
        }
        context.IntendedReproductions.Clear();

        foreach (var child in context.IntendedBirths)
        {
            if (_birthBudget < 1f) break;
            if (context.GetFoodAt(child.Position) < 1f) continue;
            context.DecrementFoodAt(child.Position);
            child.Generation = 2;
            context.Particles.Add(child);
            added++;
            _birthBudget -= 1f;
        }
        context.IntendedBirths.Clear();

        BirthsLastFrame = added;

        while (context.Particles.Count > PopulationCap)
        {
            var culled = context.Particles[0];
            foreach (var p in context.Particles)
            {
                if (p == culled) continue;
                p.RemoveBondTo(culled);
            }
            context.Particles.RemoveAt(0);
        }
    }

    public void Draw(Vector2 cameraTarget, float zoomLevel, int screenWidth, int screenHeight, Particle? selectedParticle = null)
    {
        float halfW = screenWidth / (2f * zoomLevel);
        float halfH = screenHeight / (2f * zoomLevel);
        float left = cameraTarget.X - halfW;
        float right = cameraTarget.X + halfW;
        float top = cameraTarget.Y - halfH;
        float bottom = cameraTarget.Y + halfH;

        int x0 = (int)MathF.Floor(left / GridCellSize);
        int x1 = (int)MathF.Ceiling(right / GridCellSize);
        int y0 = (int)MathF.Floor(top / GridCellSize);
        int y1 = (int)MathF.Ceiling(bottom / GridCellSize);

        int mapXiMax = (int)(MapSize / GridCellSize);
        int xiMin = Math.Clamp(x0, 0, mapXiMax);
        int xiMax = Math.Clamp(x1, 0, mapXiMax);
        int yiMin = Math.Clamp(y0, 0, mapXiMax);
        int yiMax = Math.Clamp(y1, 0, mapXiMax);

        float clipLeft = MathF.Max(left, 0f);
        float clipRight = MathF.Min(right, MapSize);
        float clipTop = MathF.Max(top, 0f);
        float clipBottom = MathF.Min(bottom, MapSize);

        for (int xi = xiMin; xi < xiMax; xi++)
            for (int yi = yiMin; yi < yiMax; yi++)
            {
                if (!CellInMapBounds((xi, yi))) continue;
                float amount = context.CellFood.TryGetValue((xi, yi), out var amt) ? amt : Context.InitialFoodPerCell;
                int px = (int)(xi * GridCellSize);
                int py = (int)(yi * GridCellSize);
                int ps = (int)GridCellSize;
                if (amount < 1f)
                    Raylib.DrawRectangle(px, py, ps, ps, new Color(180, 0, 0, 40));
                else if (amount <= 10f)
                    Raylib.DrawRectangle(px, py, ps, ps, new Color(255, 165, 0, 40));
                else
                    Raylib.DrawRectangle(px, py, ps, ps, new Color(0, 180, 0, 40));
            }

        const float gridLineThickness = 25f;
        var gridColor = Color.Black;
        for (int xi = xiMin; xi <= xiMax; xi++)
        {
            float x = xi * GridCellSize;
            Raylib.DrawLineEx(new Vector2(x, clipTop), new Vector2(x, clipBottom), gridLineThickness, gridColor);
        }
        for (int yi = yiMin; yi <= yiMax; yi++)
        {
            float y = yi * GridCellSize;
            Raylib.DrawLineEx(new Vector2(clipLeft, y), new Vector2(clipRight, y), gridLineThickness, gridColor);
        }

        foreach (var (cell, amount) in context.CellFood)
        {
            int cx = cell.Item1, cy = cell.Item2;
            if (cx < x0 || cx > x1 || cy < y0 || cy > y1) continue;
            float centerX = cx * GridCellSize + GridCellSize * 0.5f;
            float centerY = cy * GridCellSize + GridCellSize * 0.5f;
            string label = ((int)MathF.Round(amount)).ToString("N0", CultureInfo.GetCultureInfo("en-US"));
            int tw = Raylib.MeasureText(label, 16);
            Raylib.DrawText(label, (int)(centerX - tw * 0.5f), (int)(centerY - 8), 16, new Color(90, 92, 98, 255));
        }

        foreach (var p in context.Particles)
            p.DrawBonds();
        foreach (var p in context.Particles)
            p.Draw(zoomLevel);
    }
}
