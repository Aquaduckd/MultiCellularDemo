using System.Numerics;

namespace MultiCellularDemo;

public class Context
{
    public const float FoodCellSize = 500f;
    /// <summary>Cap for regeneration and "full" display. New cells start at InitialFoodPerCell.</summary>
    public const float MaxFood = 100f;
    /// <summary>Food amount when a cell is first added (EnsureCellHasFood).</summary>
    public const float InitialFoodPerCell = 10f;
    public const float SecondsPerFood = 5f;

    public List<Particle> Particles { get; } = new();
    public List<Particle> IntendedBirths { get; } = new();
    public List<(Particle self, Particle partner)> IntendedReproductions { get; } = new();
    public List<(Particle self, Particle partner)> IntendedBinds { get; } = new();

    readonly Dictionary<(int, int), float> food = new();
    public IReadOnlyDictionary<(int, int), float> CellFood => food;

    public static (int, int) CellKey(Vector2 position) => (
        (int)MathF.Floor(position.X / FoodCellSize),
        (int)MathF.Floor(position.Y / FoodCellSize));

    public float GetFoodAt(Vector2 position)
    {
        var cell = CellKey(position);
        return food.TryGetValue(cell, out var amount) ? amount : 0f;
    }

    /// <summary>Decrements food at the cell containing position by 1. Returns true if there was food to consume.</summary>
    public bool DecrementFoodAt(Vector2 position)
    {
        var cell = CellKey(position);
        if (!food.TryGetValue(cell, out var amount) || amount < 1f) return false;
        food[cell] = amount - 1f;
        return true;
    }

    /// <summary>Ensure the cell has food (set to InitialFoodPerCell if missing).</summary>
    public void EnsureCellHasFood((int, int) cell)
    {
        if (!food.ContainsKey(cell))
            food[cell] = InitialFoodPerCell;
    }

    /// <summary>Tick food regeneration: each cell gains dt/SecondsPerFood, capped at MaxFood.</summary>
    public void TickFood(float dt)
    {
        foreach (var c in food.Keys.ToList())
            food[c] = Math.Min(MaxFood, food[c] + dt / SecondsPerFood);
    }

    /// <summary>Center of the nearest cell with at least 1 food in the 5x5 around position, or null if none.</summary>
    public Vector2? GetNearestFoodCellCenter(Vector2 position)
    {
        var (cx, cy) = CellKey(position);
        float half = FoodCellSize * 0.5f;
        float bestDistSq = float.MaxValue;
        Vector2? best = null;

        for (int di = -2; di <= 2; di++)
        for (int dj = -2; dj <= 2; dj++)
        {
            int cxi = cx + di, cyi = cy + dj;
            if (!food.TryGetValue((cxi, cyi), out var amount) || amount < 1f) continue;
            float centerX = cxi * FoodCellSize + half, centerY = cyi * FoodCellSize + half;
            float dx = centerX - position.X, dy = centerY - position.Y;
            float dSq = dx * dx + dy * dy;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = new Vector2(centerX, centerY);
            }
        }

        return best;
    }

    public void AddIntendedBirth(Particle particle) => IntendedBirths.Add(particle);

    public void AddIntendedReproduction(Particle self, Particle partner) =>
        IntendedReproductions.Add((self, partner));

    public void AddIntendedBind(Particle self, Particle partner) =>
        IntendedBinds.Add((self, partner));
}
