namespace MultiCellularDemo;

/// <summary>A bond between this particle and a partner, with the time since the bond formed.</summary>
public class Bond
{
    public Particle Self { get; set; } = null!;
    public Particle Partner { get; set; } = null!;
    public float BondAge { get; set; }
}
