using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using static GenerateNoiseMap;

// Generates a single tile of terrain, handling mesh displacement, 
// noise map creation, biome assignment, and visualization modes.

public enum VisualizationMode
{
    Height,
    Heat,
    Moisture,
    Biome,
    Terrain
}
public class TileGeneration : MonoBehaviour
{
    [Header("References")]
    [HideInInspector] public GenerateNoiseMap noiseMapGeneration;
    [SerializeField] public MeshRenderer tileRenderer;
    [SerializeField] public MeshFilter meshFilter;
    [SerializeField] MeshCollider meshCollider;
    public LevelGeneration levelGen;

    [Header("Base Settings")]
    [SerializeField] public float levelScale;
    [SerializeField] public AnimationCurve heightCurve;
    [SerializeField] int biomeBlendStrength;

    [Header("Waves")]
    [SerializeField] private AnimationCurve heatCurve;
    [SerializeField] private AnimationCurve moistureCurve;

    [Header("Terrain Type Colours")]
    [SerializeField] public TerrainType[] heightTerrainTypes;
    [SerializeField] public TerrainType[] heatTerrainTypes;
    [SerializeField] public TerrainType[] moistureTerrainTypes;

    public TerrainType[,] chosenHeightTypes;
    public TerrainType[,] chosenHeatTypes;
    public TerrainType[,] chosenMoistureTypes;


    [Header("Biome Table (for Biome View & Terrain Main Colour)")]
    [SerializeField] BiomeRow[] biomes;

    [Header("Water Colour (always below 0.30)")]
    [SerializeField] private Color waterColor;

    [Header("Terrain View Height Bands")]
    [SerializeField, Range(0f, 1f)] private float waterThreshold = 0.30f;
    [SerializeField, Range(0f, 1f)] private float sandThreshold = 0.35f;
    [SerializeField, Range(0f, 1f)] private float mountainThreshold = 0.70f;
    [SerializeField, Range(0f, 1f)] private float peakThreshold = 0.80f;

    [Header("Terrain View Colours")]
    [SerializeField] public Color sandColor = new Color(0.76f, 0.70f, 0.50f);
    [SerializeField] public Color mountainFaceColor = new Color(0.45f, 0.45f, 0.45f);
    [SerializeField] public Color peakColor = Color.white;
    [SerializeField] public Color roadColor;
    [SerializeField, Range(16, 256)] private int splatResolution = 8;

    [SerializeField] public VisualizationMode visualizationMode;

    public float[,] lastHeightMap;
    public Biome[,] lastChosenBiomes;
    public float heightMultiplier;
    public float[] biomeThresholds;
    public float biomeRandomness;

    


    // Generates all noise maps, assigns types, biomes, and textures for this tile
    public TileData GenerateTile(float centerVertexZ, float maxDistanceZ)
    {
        // Mesh resolution
        Vector3[] meshVerts = meshFilter.mesh.vertices;
        int tileDepth = Mathf.RoundToInt(Mathf.Sqrt(meshVerts.Length));
        int tileWidth = tileDepth;
        heightMultiplier = levelGen.heightMultiplier;

        // World-to-noise coordinate helpers
        Vector3 tileDimensions = meshFilter.mesh.bounds.size;
        float distanceBetweenVertices = tileDimensions.z / (float)tileDepth;
        float vertexOffsetZ = transform.position.z / distanceBetweenVertices;

        // --- 1. Height Map: multi-octave Perlin noise ---
        float[,] heightMap = new float[tileDepth, tileWidth];
        for (int z = 0; z < tileDepth; z++)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                int vi = z * tileWidth + x;
                Vector3 worldP = transform.TransformPoint(meshVerts[vi]);
                float sx = worldP.x / levelScale;
                float sz = worldP.z / levelScale;

                float sum = 0f, norm = 0f;
                foreach (var w in noiseMapGeneration.HeightWaves)
                {
                    sum += w.amplitude * Mathf.PerlinNoise(sx * w.frequency + w.seed, sz * w.frequency + w.seed);
                    norm += w.amplitude;
                }
                heightMap[z, x] = sum / norm;
            }
        }

        // --- 2. Heat Map: uniform + perlin + height curve ---
        float[,] uniformHeat = noiseMapGeneration.GenerateUniformNoiseMap(
            tileDepth, tileWidth,
            centerVertexZ, maxDistanceZ,
            vertexOffsetZ
        );
        float[,] heatMap = new float[tileDepth, tileWidth];
        for (int z = 0; z < tileDepth; z++)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                int vi = z * tileWidth + x;
                Vector3 worldP = transform.TransformPoint(meshVerts[vi]);
                float sx = worldP.x / levelScale;
                float sz = worldP.z / levelScale;

                float rnd = 0f, norm = 0f;
                foreach (var w in noiseMapGeneration.HeatWaves)
                {
                    rnd += w.amplitude * Mathf.PerlinNoise(sx * w.frequency + w.seed, sz * w.frequency + w.seed);
                    norm += w.amplitude;
                }
                rnd /= norm;

                heatMap[z, x] = uniformHeat[z, x] * rnd + heatCurve.Evaluate(heightMap[z, x]) * heightMap[z, x];
            }
        }

        // --- 3. Moisture Map: perlin minus height curve ---
        float[,] moistureMap = new float[tileDepth, tileWidth];
        for (int z = 0; z < tileDepth; z++)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                int vi = z * tileWidth + x;
                Vector3 worldP = transform.TransformPoint(meshVerts[vi]);
                float sx = worldP.x / levelScale;
                float sz = worldP.z / levelScale;

                float rnd = 0f, norm = 0f;
                foreach (var w in noiseMapGeneration.MoistureWaves)
                {
                    rnd += w.amplitude * Mathf.PerlinNoise(sx * w.frequency + w.seed, sz * w.frequency + w.seed);
                    norm += w.amplitude;
                }
                rnd /= norm;

                moistureMap[z, x] = rnd - moistureCurve.Evaluate(heightMap[z, x]) * heightMap[z, x];
            }
        }

        // --- 4. Terrain type assignments and visualization textures ---
        TerrainType[,] chosenHeightTypes = new TerrainType[tileDepth, tileWidth];
        TerrainType[,] chosenHeatTypes = new TerrainType[tileDepth, tileWidth];
        TerrainType[,] chosenMoistureTypes = new TerrainType[tileDepth, tileWidth];

        this.chosenHeightTypes = chosenHeightTypes;
        this.chosenHeatTypes = chosenHeatTypes;
        this.chosenMoistureTypes = chosenMoistureTypes;


        float[,] displayHeightMap = heightMap;
        if (!levelGen.allowWater)
        {
            displayHeightMap = (float[,])heightMap.Clone();
            float minLand = waterThreshold;
            for (int z = 0; z < tileDepth; z++)
                for (int x = 0; x < tileWidth; x++)
                    if (displayHeightMap[z, x] < minLand)
                        displayHeightMap[z, x] = minLand;
        }

        Texture2D heightTex = BuildTexture(displayHeightMap, heightTerrainTypes, chosenHeightTypes);
        Texture2D heatTex = BuildTexture(heatMap, heatTerrainTypes, chosenHeatTypes);
        Texture2D moistureTex = BuildTexture(moistureMap, moistureTerrainTypes, chosenMoistureTypes);

        Biome[,] chosenBiomes = new Biome[tileDepth, tileWidth];
        Texture2D biomeTex = BuildBiomeTexture(chosenHeightTypes, chosenHeatTypes, chosenMoistureTypes, chosenBiomes);

        // --- 5. Final terrain view: blended bands and biome colors ---
        bool[,] emptyMask = new bool[tileDepth, tileWidth];
        Texture2D terrainTex = BuildTerrainTexture(displayHeightMap, chosenBiomes, emptyMask, roadColor);

        // --- 6. Apply visualization mode ---
        switch (visualizationMode)
        {
            case VisualizationMode.Height: tileRenderer.material.mainTexture = heightTex; break;
            case VisualizationMode.Heat: tileRenderer.material.mainTexture = heatTex; break;
            case VisualizationMode.Moisture: tileRenderer.material.mainTexture = moistureTex; break;
            case VisualizationMode.Biome: tileRenderer.material.mainTexture = biomeTex; break;
            case VisualizationMode.Terrain: tileRenderer.material.mainTexture = terrainTex; break;
        }

        // --- 7. Displace mesh vertices by the heightMap ---
        UpdateMeshVertices(heightMap);

        lastHeightMap = heightMap;
        lastChosenBiomes = chosenBiomes;



        // Return all data
        return new TileData(
            heightMap, heatMap, moistureMap,
            chosenHeightTypes, chosenHeatTypes, chosenMoistureTypes,
            chosenBiomes, transform, meshFilter.mesh
        );
    }

    // Maps value to TerrainType (with water skip if disabled)
    TerrainType ChooseTerrainType(float value, TerrainType[] terrainTypes)
    {
        foreach (TerrainType t in terrainTypes)
        {
            if (!levelGen.allowWater && t.name.ToLower() == "water")
                continue;
            if (value < t.threshold)
                return t;
        }
        // fallback: last non-water
        for (int i = terrainTypes.Length - 1; i >= 0; i--)
            if (levelGen.allowWater || terrainTypes[i].name.ToLower() != "water")
                return terrainTypes[i];
        return terrainTypes[terrainTypes.Length - 1];
    }

    // Assigns terrain type, returns texture for visualization
    public Texture2D BuildTexture(float[,] valueMap, TerrainType[] terrainTypes, TerrainType[,] chosenTypes)
    {
        int depth = valueMap.GetLength(0);
        int width = valueMap.GetLength(1);
        Color[] colorMap = new Color[depth * width];
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = z * width + x;
                float val = valueMap[z, x];
                TerrainType tType = ChooseTerrainType(val, terrainTypes);
                colorMap[idx] = tType.color;
                chosenTypes[z, x] = tType;
            }
        }
        Texture2D tex = new Texture2D(width, depth)
        {
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(colorMap);
        tex.Apply();
        return tex;
    }

    // Updates mesh vertices according to heightMap and animation curve
    public void UpdateMeshVertices(float[,] heightMap)
    {
        int depth = heightMap.GetLength(0);
        int width = heightMap.GetLength(1);
        Vector3[] meshVertices = this.meshFilter.mesh.vertices;

        int vertexIndex = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float height = heightMap[z, x];
                Vector3 vertex = meshVertices[vertexIndex];
                meshVertices[vertexIndex] = new Vector3(
                    vertex.x,
                    heightCurve.Evaluate(height) * heightMultiplier,
                    vertex.z
                );
                vertexIndex++;
            }
        }
        meshFilter.mesh.vertices = meshVertices;
        meshFilter.mesh.RecalculateBounds();
        meshFilter.mesh.RecalculateNormals();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = meshFilter.mesh;
    }

    // Assigns biomes and returns a biome texture for visualization
    public Texture2D BuildBiomeTexture(
        TerrainType[,] heightTypes,
        TerrainType[,] heatTypes,
        TerrainType[,] moistureTypes,
        Biome[,] chosenBiomes
    )
    {
        int depth = heatTypes.GetLength(0);
        int width = heatTypes.GetLength(1);
        Color[] colorMap = new Color[depth * width];
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = z * width + x;
                TerrainType ht = heightTypes[z, x];
                if (ht.name != "water")
                {
                    TerrainType heatT = heatTypes[z, x];
                    TerrainType moistT = moistureTypes[z, x];
                    Biome b = this.biomes[moistT.index].biomes[heatT.index];
                    colorMap[idx] = b.color;
                    chosenBiomes[z, x] = b;
                }
                else
                {
                    colorMap[idx] = waterColor;
                }
            }
        }
        Texture2D tex = new Texture2D(width, depth)
        {
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(colorMap);
        tex.Apply();
        return tex;
    }

    // Blends height bands, biomes for Terrain mode
    public Texture2D BuildTerrainTexture(float[,] heightMap, Biome[,] chosenBiomes, bool[,] roadMask, Color roadCol)
    {
        int depth = heightMap.GetLength(0);
        int width = heightMap.GetLength(1);
        Color[] pixels = new Color[depth * width];

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (roadMask[z, x])
                {
                    pixels[z * width + x] = roadCol;
                    continue;
                }
                float h = heightMap[z, x];
                Color c;
                if (h < waterThreshold)
                    c = waterColor;
                else if (h < sandThreshold)
                    c = sandColor;
                else if (h < mountainThreshold)
                    c = chosenBiomes[z, x] != null ? chosenBiomes[z, x].color : Color.magenta;
                else if (h < peakThreshold)
                    c = mountainFaceColor;
                else
                    c = peakColor;

                pixels[z * width + x] = c;
            }
        }

        Texture2D tex = new Texture2D(width, depth)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels(pixels);
        tex.Apply();
        BlurTexture(tex, biomeBlendStrength);
        return tex;
    }

    // Box blur utility for band transitions
    public void BlurTexture(Texture2D tex, int radius)
    {
        int w = tex.width, h = tex.height;
        Color[] orig = tex.GetPixels();
        Color[] result = new Color[orig.Length];
        int kernelSize = (2 * radius + 1) * (2 * radius + 1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.black;
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = Mathf.Clamp(x + dx, 0, w - 1);
                        int ny = Mathf.Clamp(y + dy, 0, h - 1);
                        sum += orig[ny * w + nx];
                    }
                result[y * w + x] = sum / kernelSize;
            }
        }
        tex.SetPixels(result);
        tex.Apply();
    }

    // Overlays roads for sharp road visuals
    public void ApplyRoadMask(
        List<List<Vector3>> globalPaths,
        float halfRoadWidth,
        float spacing,
        Color roadColor
    )
    {
        int d = lastHeightMap.GetLength(0);
        int w = lastHeightMap.GetLength(1);
        Vector3 origin = this.transform.position;
        bool[,] mask = new bool[d, w];

        for (int zi = 0; zi < d; zi++)
        {
            for (int xi = 0; xi < w; xi++)
            {
                Vector3 p = new Vector3(
                    origin.x + xi * spacing,
                    0,
                    origin.z + zi * spacing
                );
                bool isRoad = false;
                foreach (var path in globalPaths)
                {
                    if (path == null || path.Count < 2) continue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        float dist = DistancePointSegment(p, path[i], path[i + 1]);
                        if (dist <= halfRoadWidth)
                        {
                            isRoad = true;
                            break;
                        }
                    }
                    if (isRoad) break;
                }
                mask[zi, xi] = isRoad;
            }
        }
        Texture2D tex = BuildTerrainTexture(
            lastHeightMap,
            lastChosenBiomes,
            mask,
            roadColor
        );
        if (visualizationMode == VisualizationMode.Terrain)
            tileRenderer.material.mainTexture = tex;
    }

    // Utility: distance from point to segment (XZ plane)
    private float DistancePointSegment(Vector3 P, Vector3 A, Vector3 B)
    {
        Vector2 p = new Vector2(P.x, P.z);
        Vector2 a = new Vector2(A.x, A.z);
        Vector2 b = new Vector2(B.x, B.z);

        Vector2 ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 == 0f) return Vector2.Distance(p, a);

        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }

    // For blending roads into terrain
    public void ApplyRoadMaskSmooth(
        List<List<Vector3>> globalPaths,
        float halfRoadWidth,
        float spacing,
        float blendWidth,
        Color roadColor
    )
    {
        try
        {
            Texture2D tex = BuildTerrainTextureSmooth(
                lastHeightMap,
                lastChosenBiomes,
                globalPaths,
                halfRoadWidth,
                blendWidth,
                spacing,
                roadColor
            );
            tileRenderer.material.mainTexture = tex;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TileGen] ApplyRoadMaskSmooth failed: {ex}");
        }
    }

    // Like BuildTerrainTexture, but blends roads in for smooth transition
    private Texture2D BuildTerrainTextureSmooth(
        float[,] heightMap,
        Biome[,] chosenBiomes,
        List<List<Vector3>> globalPaths,
        float halfRoadWidth,
        float blendWidth,
        float spacing,
        Color roadColor
    )
    {
        int mapD = heightMap.GetLength(0);
        int mapW = heightMap.GetLength(1);
        Vector3 origin = transform.position;
        int RES = splatResolution;
        var pixels = new Color[RES * RES];

        for (int zi = 0; zi < RES; zi++)
        {
            float tZ = zi / (float)(RES - 1);
            float fZ = tZ * (mapD - 1);

            for (int xi = 0; xi < RES; xi++)
            {
                float tX = xi / (float)(RES - 1);
                float fX = tX * (mapW - 1);

                int zIdx = Mathf.Clamp(Mathf.RoundToInt(fZ), 0, mapD - 1);
                int xIdx = Mathf.Clamp(Mathf.RoundToInt(fX), 0, mapW - 1);
                float h = heightMap[zIdx, xIdx];

                // Pick base color
                Color baseC;
                if (h < waterThreshold) baseC = waterColor;
                else if (h < sandThreshold) baseC = sandColor;
                else if (h < mountainThreshold) baseC = chosenBiomes[zIdx, xIdx].color;
                else if (h < peakThreshold) baseC = mountainFaceColor;
                else baseC = peakColor;

                float worldX = origin.x + tX * (mapW - 1) * spacing;
                float worldZ = origin.z + tZ * (mapD - 1) * spacing;
                Vector3 worldPt = new Vector3(worldX, 0, worldZ);

                // Blend amount from nearest road segment
                float minDist = float.MaxValue;
                foreach (var path in globalPaths)
                    for (int i = 0; i + 1 < path.Count; i++)
                        minDist = Mathf.Min(
                            minDist,
                            DistancePointSegment(worldPt, path[i], path[i + 1])
                        );

                float t = (minDist - halfRoadWidth) / blendWidth;
                float m = 1f - Mathf.Clamp01(t);

                pixels[zi * RES + xi] = Color.Lerp(baseC, roadColor, m);
            }
        }

        var result = new Texture2D(RES, RES)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }
}
