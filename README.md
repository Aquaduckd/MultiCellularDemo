# Multi-Cellular Demo

A 2D simulation of evolving multi-cellular agents. Particles move on a grid, consume food from cells, form bonds, reproduce (consuming food at the midpoint), and mutate. Genome types include neural networks and a simpler gene-based genome.

## Build and run

Requires .NET 9.0.

```bash
cd Demos/MultiCellularDemo
dotnet run
```

Optional arguments:

```bash
dotnet run -- --seed 12345              # Reproducible run with seed 12345
dotnet run -- --genome neural           # Use neural genome (see Genome types)
dotnet run 12345                        # Seed only (bare integer)
dotnet run -- --seed 42 --genome deep   # Combine options
```

### Command-line arguments

| Argument | Description |
|----------|-------------|
| `--seed <number>` | RNG seed for the run. Use the same seed to reproduce a run. |
| `--genome <type>` | Genome type for the initial population (see below). |
| `<number>` (bare) | If a single integer is passed, it is used as the seed. |

### Genome types

Pass `--genome <type>` with one of:

- **`neural`** – Feedforward neural net (71 inputs, 1 hidden layer, 36 outputs). Includes velocity, age, bonds, nearby cells, food, and outputs e.g. force, hue, bond strength, bind/unbind/reproduce targets, mutation rate, nearby attraction, nearby food attraction.
- **`deep`** / **`deepneural`** – Deeper neural net (71 inputs, 3 hidden layers, 36 outputs). Same I/O as `neural`.
- **`chemistry`** – Reduced-input neural net (38 inputs, no velocity/age, distance-only for bonds/nearby). 35 outputs (no nearby food attraction).
- **`particlegene`** / **`gene`** – (default) Simple gene-based genome, not a neural net.

If `--genome` is omitted or unrecognized, **particlegene** is used.

---

## UI

- **Window** – 1280×720, 60 FPS target. Black background; world is drawn with a 2D camera.

### Camera and view

- **Pan** – Click and drag to move the camera.
- **Zoom** – Mouse wheel: scroll up to zoom in, down to zoom out. Zoom is clamped between 0.01 and 10.
- **Particle outline** – White outline on particles is drawn only when zoomed in (zoom ≥ 0.4). When zoomed out, only filled colors are shown so hues remain visible.

### Selection

- **Select particle** – When **paused**, click in the world to select a particle under the cursor. When the selected particle dies, selection can switch to a bond partner or a particle near the camera target.
- **Camera follow** – When a particle is selected, the camera smoothly follows it.
- **Selected panel** – Left side shows: position, hue, bond count, genome gene values, and last neural output (force, hue, bond strength, bind/unbind/reproduce, mutation rate, nearby attraction, etc.).
- **Network view** – When the selected particle uses a neural genome, a small network diagram appears in the bottom-right (layers, weights, activations).

### HUD (left side)

- **Population** – Current particle count.
- **Generation** – Median generation of particles.
- **Births/s** – Births per second over a 5-second window.
- **Total food** – Sum of food in all grid cells (explored + unexplored at initial value).
- **Max food/s** – Maximum food regeneration per second (based on map size and food params).
- **Hue histogram** – Bar chart of particle count per hue bin (36 bins).
- **Births chart** – Time series of births/s (white line). Green line = max food/s reference.

### Top-right

- **Seed: &lt;number&gt;** – Current run seed. Click to copy to clipboard (text highlights for a few seconds).
- **FPS** – Current frames per second.

### Keyboard

- **F5** – Pause / resume simulation. When paused, you can click to select a particle.

---

## Where parameters are defined

Tunable constants are spread across a few files. Change them in code and rebuild.

### Context (`Context.cs`)

Food grid and global simulation state:

- **`FoodCellSize`** – World units per grid cell (500). Cell centers are used for food and for “nearest food” attraction.
- **`MaxFood`** – Cap per cell (100). Food regenerates toward this.
- **`InitialFoodPerCell`** – Amount when a cell is first created (0).
- **`SecondsPerFood`** – Regeneration rate: each cell gains `1/SecondsPerFood` per second (e.g. 5 ⇒ 0.2/s per cell).

### Simulation (`Simulation.cs`)

Map and population:

- **`MapSize`** – World size in both X and Y (10000).
- **`MinCameraSelectionBorderDistance`** – When selecting a particle for camera follow, only consider particles at least this far from the map edge (500).
- **`MaxBirthsPerSecond`** – Birth rate budget (400). Budget accumulates each frame and is spent per birth.
- **`PopulationCap`** – Max particles (1200). Excess is culled (oldest first) when over cap.

`GridCellSize` is taken from `Context.FoodCellSize`.

### Particle (`Particle.cs`)

Physics, bonds, and rendering:

- **`Radius`** – Particle radius for drawing and hit-test (5).
- **`NearbyRadius`** – Max distance to include another particle in “nearby” input (1000).
- **`MaxBondDistance`** – Bonds break if partners are farther than this (200).
- **`ForceScale`** – Scale for force outputs (500). Output 0.5 = neutral; (output − 0.5)×2×ForceScale gives force.
- **`SpringRestLengthMin` / `SpringRestLengthMax`** – Rest length for bonds from SpringDistance output (10–100).
- **`BondAgeNormScale`** – Bond age normalized by this for inputs (10).
- **`MinBondReproductionTime`** – Minimum bond age before reproduction is allowed (0.5 s).
- **`MaxBondingPartnersMin` / `MaxBondingPartnersMax`** – Range for decoding MaxBondingPartners output (1–10).
- **`MinMutationRate` / `MaxMutationRate`** – Range for mutation rate from genome output (0.01–0.5).
- **`MaxVelocityCap`** – Velocity clamped to this magnitude (1600).
- **`AgeNormScale`** – Age in seconds divided by this for AgeNorm input (60).
- **`MinZoomForOutline`** – White outline on particles only when zoom ≥ this (0.4).
- **`HueTickRate`** – Interval (s) between applying genome hue output to the particle (0.1).

### Program (`Program.cs`)

UI and run setup:

- **`BirthsAvgWindowSeconds`** – Window for births/s average (5).
- **`BirthsChartMaxSamples`** – Max points in the births chart (300).
- **`SeedHighlightDuration`** – How long the seed text stays highlighted after copy (3 s).
- **`HueBins`** – Number of hue histogram bars (36).
- **`cameraSmoothSpeed`** – Camera follow smoothing (4).
- Chart sizes, network panel size, etc. are in local constants in the draw loop.

### Genomes

- **NeuralGenome** – `NeuralGenome.cs`: `InputDim`, `HiddenDim`, `OutputDim`, `MaxBondsEncoded`, `MaxNearbyEncoded`, `mutationStrength` in `CloneMutate`.
- **DeepNeuralGenome** – `DeepNeuralGenome.cs`: `LayerDims`, `OutputDim`, same mutation/encoding constants.
- **ChemistryGenome** – `ChemistryGenome.cs`: `InputDim`, `OutputDim` (35, no nearby food attraction), `LayerDims`, etc.
- **ParticleGene** – `ParticleGene.cs`: gene count and mutation parameters.

---

## Summary

Run with `dotnet run`; use `--seed` for reproducibility and `--genome` to choose neural, deep, chemistry, or particlegene. Pan and zoom with mouse; pause with F5 and click to select a particle and inspect its genome and last output. Tweak behavior by editing the constants in `Context.cs`, `Simulation.cs`, `Particle.cs`, and `Program.cs` as listed above.
