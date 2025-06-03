using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.ProBuilder;

// Describes which prefabs and spawn rules are used for each biome.
// The biomeName must match your Biome's name exactly.

[System.Serializable]
public class BiomeObjectSettings
{
    [Tooltip("Must exactly match your Biome.name")]
    public string biomeName;

    [Tooltip("Prefabs to choose from when spawning in this biome")]
    public GameObject[] prefabs;

    [Range(0, 1), Tooltip("Fraction of eligible vertices that get an instance")]
    public float density = 0.1f;
}

// Spawns objects such as trees, rocks, or props, according to biome type and spawn settings.
// Used after the world is generated.
public class BiomeObjectSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GenerateNoiseMap noiseMapGenerator;

    [Header("Spawn Controls")]
    [Range(0f, 1f)]
    public float globalDensity = 1f;
    [SerializeField] private BiomeObjectSettings[] biomeObjectSettings;
    [SerializeField] private float objectGlobalScale = 1f;

    [SerializeField, Tooltip("Should match the scale used in tile generation")]
    private float levelScale = 20f;

    private bool[,] roadMask;

    // Allows you to provide a mask that prevents objects from spawning on roads.
    public void SetRoadMask(bool[,] mask)
    {
        roadMask = mask;
    }

    // Spawn objects into the level after world and biome generation is complete.
    // Avoids villages and optionally roads.
    public void SpawnObjects(
        int totalDepth,
        int totalWidth,
        float vertexSpacing,
        LevelData levelData,
        bool[,] villageMask,
        Transform parent
    )
    {
        if (biomeObjectSettings == null || biomeObjectSettings.Length == 0)
        {
            Debug.LogWarning("BiomeObjectSpawner: No biomeObjectSettings assigned!");
            return;
        }

        // Used to optionally bias placement (slightly randomizes spawn pattern)
        float[,] placementNoise = noiseMapGenerator.GeneratePerlinNoiseMap(
            totalDepth, totalWidth, levelScale, 0, 0,
            new GenerateNoiseMap.Wave[] {
                new GenerateNoiseMap.Wave { seed = 0, frequency = 1, amplitude = 1 }
            });

        for (int z = 0; z < totalDepth; z++)
        {
            for (int x = 0; x < totalWidth; x++)
            {
                // Find which tile/vertex this is in world data
                TileCoordinate coord = levelData.ConvertToTileCoordinate(z, x);
                TileData tileData = levelData.tilesData[coord.tileZIndex, coord.tileXIndex];

                // Identify which biome this vertex is in
                Biome biome = tileData.chosenBiomes[coord.coordinateZIndex, coord.coordinateXIndex];
                if (biome == null)
                    continue;
                if (roadMask != null && roadMask[z, x])
                    continue;
                if (villageMask != null && villageMask[z, x])
                    continue;

                // Lookup settings for this biome
                var settings = biomeObjectSettings.FirstOrDefault(s => s.biomeName == biome.name);
                if (settings == null || settings.prefabs.Length == 0)
                    continue;

                // Roll to see if we should spawn here, factoring in density and placement noise
                float chance = settings.density * globalDensity;
                float noiseFactor = placementNoise[z, x]; // [0,1]
                if (Random.value > chance * noiseFactor)
                    continue;

                // Pick a random prefab for this biome
                GameObject prefab = settings.prefabs[Random.Range(0, settings.prefabs.Length)];

                // Figure out world position for this vertex
                int tileWidth = tileData.heightMap.GetLength(1);
                int vertexIndex = coord.coordinateZIndex * tileWidth + coord.coordinateXIndex;
                Vector3 localVertex = tileData.mesh.vertices[vertexIndex];
                Vector3 worldPosition = tileData.tileTransform.TransformPoint(localVertex);

                // Random Y rotation for variety
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                GameObject instance = Instantiate(prefab, worldPosition, rotation, parent);

                // Scale object globally (keeps prop size consistent across world)
                instance.transform.localScale = prefab.transform.localScale * objectGlobalScale;
            }
        }
    }
}
