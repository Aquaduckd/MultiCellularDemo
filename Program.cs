using System.Globalization;
using System.Numerics;
using MultiCellularDemo;
using Raylib_cs;

static string Fmt(long n) => n.ToString("N0", CultureInfo.GetCultureInfo("en-US"));

static Color HueToColor(float hDeg)
{
    float h = hDeg / 60f;
    if (h < 0) h += 6f;
    if (h >= 6f) h -= 6f;
    int i = (int)MathF.Floor(h);
    float f = h - i;
    float p = 0f;
    float q = 1f - f;
    float t = f;
    (float r, float g, float b) = i switch
    {
        0 => (1f, t, p),
        1 => (q, 1f, p),
        2 => (p, 1f, t),
        3 => (p, q, 1f),
        4 => (t, p, 1f),
        _ => (1f, p, q)
    };
    return new Color((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)255);
}

Raylib.InitWindow(1280, 720, "Multi-Cellular Demo");
Raylib.SetWindowState(ConfigFlags.ResizableWindow);
Raylib.SetTargetFPS(60);

int runSeed = Environment.TickCount;
string? genomeType = null;
string[] cmdArgs = Environment.GetCommandLineArgs();
for (int i = 1; i < cmdArgs.Length; i++)
{
    if (cmdArgs[i] == "--seed" && i + 1 < cmdArgs.Length && int.TryParse(cmdArgs[i + 1], out int parsed))
    {
        runSeed = parsed;
        i++;
        continue;
    }
    if (cmdArgs[i] == "--genome" && i + 1 < cmdArgs.Length)
    {
        genomeType = cmdArgs[i + 1];
        i++;
        continue;
    }
    if (int.TryParse(cmdArgs[i], out int parsedSeed))
    {
        runSeed = parsedSeed;
        continue;
    }
}
Simulation.InitRun(runSeed);
var simulation = new Simulation(Simulation.MapSize, Simulation.MapSize, genomeType);

var cameraTarget = Simulation.MapCenter;
float zoomLevel = 1f;
Vector2? dragStart = null;
bool paused = false;
Particle? selectedParticle = null;
const float BirthsAvgWindowSeconds = 5f;
var birthsWindow = new List<(float dt, int count)>();
float birthsPerSecond = 0f;
const int BirthsChartMaxSamples = 300;
var birthsPerSecondHistory = new List<float>();
float seedHighlightUntil = 0f;
const float SeedHighlightDuration = 3f;

while (!Raylib.WindowShouldClose())
{
    if (Raylib.IsKeyPressed(KeyboardKey.F5))
        paused = !paused;

    float dt = Raylib.GetFrameTime();
    int w = Raylib.GetScreenWidth();
    int h = Raylib.GetScreenHeight();

    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        dragStart = Raylib.GetMousePosition();
    if (Raylib.IsMouseButtonReleased(MouseButton.Left))
    {
        var release = Raylib.GetMousePosition();
        bool shortClick = dragStart is Vector2 start && Vector2.Distance(start, release) < 5f;
        if (shortClick)
        {
            string seedTextForHit = $"Seed: {Simulation.RunSeed}";
            int seedHitW = Raylib.MeasureText(seedTextForHit, 18);
            int seedX = w - seedHitW - 12;
            const int seedY = 12;
            const int seedFontSize = 18;
            if (release.X >= seedX && release.X <= seedX + seedHitW && release.Y >= seedY && release.Y <= seedY + seedFontSize + 4)
            {
                Raylib.SetClipboardText(Simulation.RunSeed.ToString());
                seedHighlightUntil = (float)Raylib.GetTime() + SeedHighlightDuration;
            }
            else if (paused)
            {
                float halfW = w / 2f, halfH = h / 2f;
                var worldPos = cameraTarget + (release - new Vector2(halfW, halfH)) / zoomLevel;
                selectedParticle = simulation.GetParticleAt(worldPos);
            }
        }
        dragStart = null;
    }
    if (dragStart is Vector2 startPos && Raylib.IsMouseButtonDown(MouseButton.Left))
    {
        var pos = Raylib.GetMousePosition();
        var delta = new Vector2(pos.X - startPos.X, pos.Y - startPos.Y);
        cameraTarget -= delta / zoomLevel;
        dragStart = pos;
    }

    float wheel = Raylib.GetMouseWheelMove();
    if (wheel != 0f)
    {
        zoomLevel *= wheel > 0 ? 1.1f : 1f / 1.1f;
        zoomLevel = Math.Clamp(zoomLevel, 0.01f, 10f);
    }

    if (!paused)
        simulation.Update(dt);

    if (selectedParticle != null && !simulation.Particles.Contains(selectedParticle))
    {
        var dead = selectedParticle;
        selectedParticle = null;
        Particle? bondPartner = null;
        int bestBonds = -1;
        foreach (var b in dead.Bonds)
        {
            var p = b.Partner;
            if (!simulation.Particles.Contains(p)) continue;
            if (p.Bonds.Count > bestBonds)
            {
                bestBonds = p.Bonds.Count;
                bondPartner = p;
            }
        }
        selectedParticle = bondPartner ?? simulation.GetParticleNearPosition(cameraTarget, 2000f, Simulation.MinCameraSelectionBorderDistance);
    }

    if (selectedParticle != null)
    {
        var pos = selectedParticle.Position;
        if (pos.X < 0 || pos.X > Simulation.MapSize || pos.Y < 0 || pos.Y > Simulation.MapSize)
            selectedParticle = simulation.GetParticleNearPosition(cameraTarget, 2000f, Simulation.MinCameraSelectionBorderDistance);
        else if (selectedParticle.Speed < 1f)
            selectedParticle = simulation.GetParticleNearPosition(cameraTarget, 2000f, Simulation.MinCameraSelectionBorderDistance);
    }

    if (selectedParticle != null)
    {
        const float cameraSmoothSpeed = 4f;
        float t = 1f - MathF.Exp(-cameraSmoothSpeed * dt);
        cameraTarget = Vector2.Lerp(cameraTarget, selectedParticle.Position, t);
    }

    if (!paused)
    {
        birthsWindow.Add((dt, simulation.BirthsLastFrame));
        float totalTime = 0f;
        int totalBirths = 0;
        int i = 0;
        for (; i < birthsWindow.Count; i++)
        {
            totalTime += birthsWindow[i].dt;
            totalBirths += birthsWindow[i].count;
            if (totalTime > BirthsAvgWindowSeconds) break;
        }
        if (totalTime > BirthsAvgWindowSeconds && i > 0)
        {
            float excess = totalTime - BirthsAvgWindowSeconds;
            totalTime -= excess;
            totalBirths -= (int)(birthsWindow[i].count * (excess / birthsWindow[i].dt));
            birthsWindow.RemoveRange(0, i + 1);
        }
        else if (totalTime > BirthsAvgWindowSeconds)
        {
            birthsWindow.Clear();
            totalTime = 0f;
            totalBirths = 0;
        }
        birthsPerSecond = totalTime > 0f ? totalBirths / totalTime : 0f;
    }

    birthsPerSecondHistory.Add(birthsPerSecond);
    if (birthsPerSecondHistory.Count > BirthsChartMaxSamples)
        birthsPerSecondHistory.RemoveAt(0);

    var camera = new Camera2D
    {
        Offset = new Vector2(w / 2f, h / 2f),
        Target = cameraTarget,
        Rotation = 0f,
        Zoom = zoomLevel
    };

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    Raylib.BeginMode2D(camera);
    simulation.Draw(cameraTarget, zoomLevel, w, h, selectedParticle);
    Raylib.EndMode2D();

    const int HueBins = 36;
    var hueCounts = new int[HueBins];
    foreach (var p in simulation.Particles)
    {
        float hue = p.Hue;
        int bin = (int)Math.Clamp(MathF.Floor(hue / 360f * HueBins), 0, HueBins - 1);
        hueCounts[bin]++;
    }
    int maxHueCount = hueCounts.Length > 0 ? hueCounts.Max() : 1;

    int uiX = 10, uiY = 10;
    Raylib.DrawText($"Population: {Fmt(simulation.Particles.Count)}", uiX, uiY, 20, Color.White);
    uiY += 22;
    float medianGeneration = 0f;
    if (simulation.Particles.Count > 0)
    {
        var gens = simulation.Particles.Select(p => p.Generation).OrderBy(g => g).ToList();
        int n = gens.Count;
        medianGeneration = n % 2 == 1 ? gens[n / 2] : (gens[n / 2 - 1] + gens[n / 2]) / 2f;
    }
    Raylib.DrawText($"Generation: {Fmt((int)MathF.Round(medianGeneration))}", uiX, uiY, 20, Color.White);
    uiY += 22;
    Raylib.DrawText($"Births/s: {Fmt((int)MathF.Round(birthsPerSecond))}", uiX, uiY, 20, Color.White);
    uiY += 22;
    Raylib.DrawText($"Total food: {Fmt((long)MathF.Round(simulation.TotalFood))}", uiX, uiY, 20, Color.White);
    uiY += 22;
    Raylib.DrawText($"Max food/s: {Fmt((int)MathF.Round((float)Simulation.MaxFoodPerSecond))}", uiX, uiY, 20, Color.White);
    uiY += 28;
    const int barWidth = 4;
    const int histHeight = 60;
    for (int i = 0; i < HueBins; i++)
    {
        int barH = maxHueCount > 0 ? (int)Math.Max(1, (float)hueCounts[i] / maxHueCount * histHeight) : 0;
        float hue = (i + 0.5f) / HueBins * 360f;
        var barColor = HueToColor(hue);
        Raylib.DrawRectangle(uiX + i * barWidth, uiY + histHeight - barH, barWidth, barH, barColor);
    }
    Raylib.DrawRectangleLines(uiX, uiY, HueBins * barWidth, histHeight, new Color(80, 80, 90, 255));

    const int chartWidth = HueBins * barWidth;
    const int chartHeight = 60;
    int chartX = uiX;
    int chartY = uiY + histHeight + 8;
    float maxFoodPerSec = Simulation.MaxFoodPerSecond;
    float yMax = maxFoodPerSec * 1.2f;
    if (birthsPerSecondHistory.Count > 0)
    {
        float histMax = birthsPerSecondHistory.Max();
        if (histMax > yMax) yMax = histMax * 1.1f;
    }
    if (yMax < 1f) yMax = 1f;

    Raylib.DrawRectangle(chartX, chartY, chartWidth, chartHeight, new Color(25, 25, 32, 255));
    Raylib.DrawRectangleLines(chartX, chartY, chartWidth, chartHeight, new Color(80, 80, 90, 255));

    float refY = chartY + chartHeight - (maxFoodPerSec / yMax) * chartHeight;
    Raylib.DrawLine(chartX, (int)refY, chartX + chartWidth, (int)refY, new Color(100, 180, 100, 255));

    if (birthsPerSecondHistory.Count >= 2)
    {
        int n = birthsPerSecondHistory.Count;
        for (int i = 0; i < n - 1; i++)
        {
            float v0 = birthsPerSecondHistory[i];
            float v1 = birthsPerSecondHistory[i + 1];
            float x0 = chartX + (float)i / (n - 1) * (chartWidth - 1);
            float x1 = chartX + (float)(i + 1) / (n - 1) * (chartWidth - 1);
            float y0 = chartY + chartHeight - (v0 / yMax) * chartHeight;
            float y1 = chartY + chartHeight - (v1 / yMax) * chartHeight;
            Raylib.DrawLine((int)x0, (int)y0, (int)x1, (int)y1, Color.White);
        }
    }

    uiY = chartY + chartHeight + 8;

    if (selectedParticle != null)
    {
        if (!simulation.Particles.Contains(selectedParticle))
            selectedParticle = null;
        else
        {
        uiY += 16;
        Raylib.DrawText("Selected particle", uiX, uiY, 18, Color.White);
        uiY += 22;
        Raylib.DrawText($"  Position: ({Fmt((int)MathF.Round(selectedParticle.Position.X))}, {Fmt((int)MathF.Round(selectedParticle.Position.Y))})", uiX, uiY, 14, new Color(200, 200, 200, 255));
        uiY += 18;
        Raylib.DrawText($"  Hue: {Fmt((int)MathF.Round(selectedParticle.Hue))}", uiX, uiY, 14, new Color(200, 200, 200, 255));
        uiY += 18;
        Raylib.DrawText($"  Bonds: {Fmt(selectedParticle.Bonds.Count)}", uiX, uiY, 14, new Color(200, 200, 200, 255));
        uiY += 20;
        var genes = selectedParticle.Genome.GetGeneValues();
        if (genes.Count > 0)
        {
            Raylib.DrawText("  Genome:", uiX, uiY, 14, Color.White);
            uiY += 18;
            foreach (var (name, value) in genes.OrderBy(kv => kv.Key))
            {
                Raylib.DrawText($"    {name}: {value:F2}", uiX, uiY, 14, new Color(180, 180, 180, 255));
                uiY += 16;
            }
        }
        uiY += 20;
        var lastOut = selectedParticle.LastOutput;
        if (lastOut != null)
        {
            Raylib.DrawText("  Last output:", uiX, uiY, 14, Color.White);
            uiY += 18;
            Raylib.DrawText($"    ForceX/Y: ({lastOut.ForceX:F2}, {lastOut.ForceY:F2})", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    Hue: {lastOut.Hue:F2}  BondStrength: {lastOut.BondStrength:F2}  SpringDistance: {lastOut.SpringDistance:F2}", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    MaxBondingPartners: {lastOut.MaxBondingPartners:F2}", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    BindTargets: {Fmt(lastOut.BindTargets.Count)}  UnbondTargets: {Fmt(lastOut.UnbondTargets.Count)}", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    ReproduceWithTarget: {(lastOut.ReproduceWithTarget != null ? "yes" : "no")}", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    ReproductionBondTimeNorm: {lastOut.ReproductionBondTimeNorm:F2}", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    MutationRateNorm: {lastOut.MutationRateNorm:F2}", uiX, uiY, 14, new Color(180, 180, 180, 255));
            uiY += 16;
            Raylib.DrawText($"    NearbyAttraction: {(lastOut.NearbyAttraction != null ? $"{lastOut.NearbyAttraction.Length} slots" : "null")}", uiX, uiY, 14, new Color(180, 180, 180, 255));
        }
        uiY += 8;
        var netLayout = selectedParticle.Genome.GetNetworkLayout();
        if (netLayout != null)
        {
            const int netW = 380, netH = 200;
            const int margin = 20;
            int netX = w - netW - margin;
            int netY = h - netH - margin - 18;
            Raylib.DrawText("Network", netX, netY - 16, 14, Color.White);
            var layers = netLayout.LayerSizes;
            int numLayers = layers.Length;
            if (numLayers >= 2)
            {
                float[] colX = new float[numLayers];
                for (int l = 0; l < numLayers; l++)
                    colX[l] = netX + (l + 0.5f) / numLayers * netW;
                float[][] nodeY = new float[numLayers][];
                for (int l = 0; l < numLayers; l++)
                {
                    nodeY[l] = new float[layers[l]];
                    for (int i = 0; i < layers[l]; i++)
                        nodeY[l][i] = netY + 14 + (i + 0.5f) / layers[l] * (netH - 14);
                }
                const int maxEdgesPerLayer = 120;
                var layerActivations = netLayout.LayerActivations;
                for (int l = 0; l < numLayers - 1; l++)
                {
                    int inSize = layers[l];
                    int outSize = layers[l + 1];
                    float[] layerWeights = netLayout.Weights[l];
                    float[]? srcAct = layerActivations != null && l < layerActivations.Length ? layerActivations[l] : null;
                    int totalEdges = inSize * outSize;
                    int step = Math.Max(1, totalEdges / maxEdgesPerLayer);
                    for (int idx = 0; idx < totalEdges; idx += step)
                    {
                        int outIdx = idx / inSize;
                        int inIdx = idx % inSize;
                        float weight = layerWeights[outIdx * inSize + inIdx];
                        float flow = srcAct != null && inIdx < srcAct.Length ? weight * srcAct[inIdx] : weight;
                        float tFlow = Math.Clamp(MathF.Abs(flow) * 4f, 0f, 1f);
                        float t = Math.Clamp(MathF.Abs(weight) * 2f, 0f, 1f);
                        byte r = (byte)(weight < 0 ? (byte)(t * (0.3f + 0.7f * tFlow) * 255) : (byte)0);
                        byte g = (byte)(weight >= 0 ? (byte)(t * (0.3f + 0.7f * tFlow) * 255) : (byte)0);
                        byte alpha = (byte)(80 + (int)(175 * tFlow));
                        var edgeColor = new Color(r, g, (byte)80, alpha);
                        Raylib.DrawLineEx(
                            new Vector2(colX[l], nodeY[l][inIdx]),
                            new Vector2(colX[l + 1], nodeY[l + 1][outIdx]),
                            0.5f + 1.5f * tFlow,
                            edgeColor);
                    }
                }
                var layerActivationsForNodes = netLayout.LayerActivations;
                for (int l = 0; l < numLayers; l++)
                {
                    for (int i = 0; i < layers[l]; i++)
                    {
                        float act = 0f;
                        if (layerActivationsForNodes != null && l < layerActivationsForNodes.Length && i < layerActivationsForNodes[l].Length)
                            act = Math.Clamp(layerActivationsForNodes[l][i], -1f, 1f);
                        byte r = (byte)(80 + (int)(175 * Math.Max(0f, -act)));
                        byte g = (byte)(80 + (int)(175 * Math.Max(0f, act)));
                        byte b = (byte)80;
                        var nodeColor = new Color(r, g, b, (byte)255);
                        Raylib.DrawCircle((int)colX[l], (int)nodeY[l][i], 3, nodeColor);
                        Raylib.DrawCircleLines((int)colX[l], (int)nodeY[l][i], 3f, Color.White);
                    }
                }
            }
        }
        }
    }

    string seedText = $"Seed: {Simulation.RunSeed}";
    int seedW = Raylib.MeasureText(seedText, 18);
    int seedDrawX = w - seedW - 12;
    bool seedHighlight = (float)Raylib.GetTime() < seedHighlightUntil;
    Raylib.DrawText(seedText, seedDrawX, 12, 18, seedHighlight ? Color.Lime : new Color(200, 200, 200, 255));
    string genomeText = $"Genome: {genomeType ?? "particlegene"}";
    int genomeW = Raylib.MeasureText(genomeText, 14);
    Raylib.DrawText(genomeText, w - genomeW - 12, 34, 14, new Color(200, 200, 200, 255));
    int fps = Raylib.GetFPS();
    string fpsText = $"{fps} FPS";
    int fpsW = Raylib.MeasureText(fpsText, 20);
    Raylib.DrawText(fpsText, w - fpsW - 12, 52, 20, new Color(200, 200, 200, 255));

    Raylib.EndDrawing();
}

Raylib.CloseWindow();
