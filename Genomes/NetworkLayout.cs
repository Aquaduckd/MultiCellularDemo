namespace MultiCellularDemo.Genomes;

/// <summary>Layout and weights for drawing a feedforward network. LayerSizes[0]=input, last=output; Weights[l] is row-major [out, in] for layer l→l+1. When set, LayerActivations[l] holds the activation of each node in layer l from the last forward pass (for real-time lighting).</summary>
public class NetworkLayout
{
    public int[] LayerSizes { get; }
    public float[][] Weights { get; }
    public float[][] Biases { get; }
    /// <summary>Activations per layer from last Evaluate; null if not available. Used to light up edges by signal flow.</summary>
    public float[][]? LayerActivations { get; }

    public NetworkLayout(int[] layerSizes, float[][] weights, float[][] biases, float[][]? layerActivations = null)
    {
        LayerSizes = layerSizes;
        Weights = weights;
        Biases = biases;
        LayerActivations = layerActivations;
    }
}
