using System;
using UnityEngine;

// Handles procedural noise generation for height, heat, and moisture maps.
// Provides Perlin/octave noise utilities for terrain and biome generation.
public class GenerateNoiseMap : MonoBehaviour
{
    [Serializable]
    public class Wave
    {
        [HideInInspector] public float seed;
        public float frequency = 1f;
        public float amplitude = 1f;
    }

    public float noiseScale = 20f;
    public int octaves = 3;

    [Header("Configured in runtime")]
    [Tooltip("Height-map octaves")]
    [SerializeField] public Wave[] heightWaves;
    [Tooltip("Heat-map octaves")]
    [SerializeField] public Wave[] heatWaves;
    [Tooltip("Moisture-map octaves")]
    [SerializeField] public Wave[] moistureWaves;

    // Always returns a valid array (never null)
    public Wave[] HeightWaves => heightWaves;
    public Wave[] HeatWaves => heatWaves;
    public Wave[] MoistureWaves => moistureWaves;

    private System.Random rng;

    private void Awake()
    {
        ReseedWaves();
    }

    // Randomizes all wave seeds for new map generation
    public void ReseedWaves()
    {
        rng = new System.Random();
        foreach (var w in heightWaves) w.seed = (float)rng.NextDouble() * 10000f;
        foreach (var w in heatWaves) w.seed = (float)rng.NextDouble() * 10000f;
        foreach (var w in moistureWaves) w.seed = (float)rng.NextDouble() * 10000f;
    }

    // Builds a set of octave waves (frequencies & amplitudes)
    public Wave[] BuildOctaves(int octaves)
    {
        var result = new Wave[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float freq = Mathf.Pow(2, i);     // 1, 2, 4, 8, ...
            float amp = 1f / freq;            // 1, 0.5, 0.25, ...
            result[i] = new Wave
            {
                seed = (float)rng.NextDouble() * 10000f,
                frequency = freq,
                amplitude = amp
            };
        }
        return result;
    }

    // Regenerates all octave arrays for a new map
    public void RegenAllOctaves()
    {
        heightWaves = BuildOctaves(octaves);
        ReseedWaves();
    }

    // Generates a Perlin-noise map using the provided waves (multi-octave).
    public float[,] GeneratePerlinNoiseMap(
        int mapDepth, int mapWidth,
        float scale, float offsetX, float offsetZ,
        Wave[] waves
    )
    {
        var noiseMap = new float[mapDepth, mapWidth];
        for (int z = 0; z < mapDepth; z++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float sampleX = (x + offsetX) / scale;
                float sampleZ = (z + offsetZ) / scale;
                float sum = 0f, norm = 0f;

                foreach (var w in waves)
                {
                    sum += w.amplitude * Mathf.PerlinNoise(
                        sampleX * w.frequency + w.seed,
                        sampleZ * w.frequency + w.seed
                    );
                    norm += w.amplitude;
                }

                noiseMap[z, x] = sum / norm;
            }
        }
        return noiseMap;
    }

    // Generates a linear vertical gradient (used for heat maps).
    public float[,] GenerateUniformNoiseMap(
        int mapDepth, int mapWidth,
        float centerVertexZ, float maxDistanceZ,
        float offsetZ
    )
    {
        var noiseMap = new float[mapDepth, mapWidth];
        for (int z = 0; z < mapDepth; z++)
        {
            float sampleZ = z + offsetZ;
            float noise = Mathf.Abs(sampleZ - centerVertexZ) / maxDistanceZ;

            for (int x = 0; x < mapWidth; x++)
                noiseMap[mapDepth - z - 1, x] = noise;
        }
        return noiseMap;
    }

    // Sample the "true" world height at an XZ point using the noise, curve, and multiplier.
    public float SampleWorldHeight(
        float x, float z,
        float levelScale,
        Wave[] waves,
        AnimationCurve heightCurve,
        float heightMultiplier
    )
    {
        float sx = x / levelScale;
        float sz = z / levelScale;
        float sum = 0f, norm = 0f;
        foreach (var w in waves)
        {
            sum += w.amplitude * Mathf.PerlinNoise(
                sx * w.frequency + w.seed,
                sz * w.frequency + w.seed
            );
            norm += w.amplitude;
        }
        float noise = sum / norm;
        float height = heightCurve.Evaluate(noise) * heightMultiplier;
        return height;
    }
}
