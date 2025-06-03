using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Handles all world generation: terrain, biome assignment, village and prop placement, and player spawn.
public class LevelGeneration : MonoBehaviour
{
    // Used for noise octaves
    public class Wave
    {
        [HideInInspector] public float seed;
        public float frequency = 1f;
        public float amplitude = 1f;
    }

    [Header("Layout")]
    [SerializeField] public int levelWidthInTiles = 10;
    [SerializeField] public int levelDepthInTiles = 10;
    [SerializeField] private GameObject tilePrefab;

    [Header("Object Spawning")]
    [SerializeField] private BiomeObjectSpawner objectSpawner;
    [SerializeField] private GenerateNoiseMap noiseMapGenerator;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    private GameObject playerInstance;

    [Header("Village Settings")]
    [SerializeField, Tooltip("Number of villages to place")]
    public int villageCount = 3;
    [SerializeField, Tooltip("Village radius, in tiles")]
    public float villageRadius = 4;
    [SerializeField, Tooltip("Furniture prefabs to scatter after houses")]
    private GameObject[] villageFurniturePrefabs;
    [SerializeField] public Vector2 villageRadiusMultiplierRange = new Vector2(0.8f, 1.2f);
    [SerializeField] public float villageGraceTiles = 1f;

    [Range(0f, 1f), Tooltip("Fraction of furniture spots filled")]
    [SerializeField] public float furnitureDensity = 0.3f;

    [Tooltip("Max attempts to place houses per village")]
    [SerializeField] public int maxHouseAttempts = 500;

    private (Vector3 origin, float radiusWorld)[] villageData;

    private GameObject tilesParent;
    private GameObject villageParent;
    private GameObject biomeParent;

    [Header("Terrain Settings")]
    [SerializeField] public AnimationCurve heightCurve;
    [SerializeField] public float heightMultiplier = 12f;
    public VisualizationMode visualizationMode;


    public float objectDensity = 1f;
    public bool allowWater = true;
    public float[] biomeThresholds;
    public float biomeRandomness = 0.1f;

    public float noiseScale = 20f;
    public int octaves = 3;

    [SerializeField] public float levelScale = 20f;

    [System.Serializable]
    public class BuildingColorSet
    {
        [Tooltip("Name for this color set")]
        public string colorName;
        [Tooltip("House prefabs using this roof color")]
        public GameObject[] housePrefabs;
    }

    [Header("Village House Color Sets")]
    [SerializeField] private BuildingColorSet[] villageHouseColorSets;

    // Internal state
    private TileGeneration[,] tileGenerators;
    private LevelData levelData;
    private float vertexSpacing;
    private int tileWidth;
    private int tileDepth;
    private int vertsPerAxis;

    private void Start()
    {
        GenerateMap();
        SpawnPlayer();
    }

    // Instantiates tiles, builds LevelData, and spawns all objects and villages.
    public void GenerateMap()
    {
        if (tilesParent != null) Destroy(tilesParent);
        if (villageParent != null) Destroy(villageParent);
        if (biomeParent != null) Destroy(biomeParent);

        noiseMapGenerator.octaves = octaves;
        noiseMapGenerator.noiseScale = noiseScale;
        noiseMapGenerator.RegenAllOctaves();
        noiseMapGenerator.ReseedWaves();

        tilesParent = new GameObject("LevelTiles");
        villageParent = new GameObject("Villages");
        biomeParent = new GameObject("BiomeObjects");

        // Calculate sizes and spacing from the tile prefab mesh
        Vector3 tileSize = tilePrefab.GetComponent<MeshRenderer>().bounds.size;
        tileWidth = Mathf.RoundToInt(tileSize.x);
        tileDepth = Mathf.RoundToInt(tileSize.z);
        vertsPerAxis = GetVertsPerAxis(tilePrefab);
        vertexSpacing = tileDepth / (float)vertsPerAxis;

        float totalDepthVerts = levelDepthInTiles * vertsPerAxis;
        float centerVertexZ = (totalDepthVerts - 1f) / 2f;
        float maxDistanceZ = centerVertexZ;

        float originX = transform.position.x;
        float originZ = transform.position.z;
        float tileWorldW = tileSize.x;
        float tileWorldD = tileSize.z;
        float spacingX = tileWorldW / vertsPerAxis;
        float spacingZ = tileWorldD / vertsPerAxis;

        // Build LevelData for all tiles
        levelData = new LevelData(
            vertsPerAxis, vertsPerAxis,
            levelDepthInTiles, levelWidthInTiles
        );

        // Instantiate and generate each tile
        tileGenerators = new TileGeneration[levelDepthInTiles, levelWidthInTiles];
        for (int z = 0; z < levelDepthInTiles; z++)
        {
            for (int x = 0; x < levelWidthInTiles; x++)
            {
                Vector3 pos = new Vector3(
                    transform.position.x + x * tileWidth,
                    transform.position.y,
                    transform.position.z + z * tileDepth
                );

                GameObject tileObj = Instantiate(tilePrefab, pos, Quaternion.identity, tilesParent.transform);
                var gen = tileObj.GetComponent<TileGeneration>();

                gen.levelGen = this;
                gen.noiseMapGeneration = noiseMapGenerator;
                gen.heightMultiplier = heightMultiplier;
                gen.biomeThresholds = biomeThresholds;
                gen.biomeRandomness = biomeRandomness;

                var tileData = gen.GenerateTile(centerVertexZ, maxDistanceZ);
                levelData.AddTileData(tileData, z, x);
            }
        }

        float worldMinX = transform.position.x;
        float worldMaxX = transform.position.x + levelWidthInTiles * tileWidth;
        float worldMinZ = transform.position.z;
        float worldMaxZ = transform.position.z + levelDepthInTiles * tileDepth;

        int totalVertsDepth = levelDepthInTiles * vertsPerAxis;
        int totalVertsWidth = levelWidthInTiles * vertsPerAxis;
        bool[,] skipMask = new bool[totalVertsDepth, totalVertsWidth];
        float graceWorld = villageGraceTiles * Mathf.Max(spacingX, spacingZ);

        // Generate and place villages
        bool[,] villageMask = SpawnVillages(worldMinX, worldMinZ, tileWorldW, tileWorldD, spacingX, spacingZ, villageParent.transform);
        var villages = this.villageData;

        // Mark out any vertex that overlaps a village or is in grace range
        for (int zv = 0; zv < totalVertsDepth; zv++)
        {
            for (int xv = 0; xv < totalVertsWidth; xv++)
            {
                if (villageMask[zv, xv])
                {
                    skipMask[zv, xv] = true;
                    continue;
                }
                Vector3 worldP = new Vector3(
                    originX + xv * spacingX,
                    0f,
                    originZ + zv * spacingZ
                );
                foreach (var v in villages)
                {
                    float maxRad = v.radiusWorld + graceWorld;
                    if ((worldP - v.origin).sqrMagnitude <= maxRad * maxRad)
                    {
                        skipMask[zv, xv] = true;
                        break;
                    }
                }
            }
        }

        objectSpawner.globalDensity = objectDensity;
        objectSpawner.SpawnObjects(
            totalVertsDepth,
            totalVertsWidth,
            vertexSpacing,
            levelData,
            skipMask,
            biomeParent.transform
        );

        SpawnPlayer();
    }

    // Spawns the player at the center of the generated world.
    private void SpawnPlayer()
    {
        float halfW = (levelWidthInTiles * vertsPerAxis - 1) * vertexSpacing * 0.5f;
        float halfD = (levelDepthInTiles * vertsPerAxis - 1) * vertexSpacing * 0.5f;
        Vector3 spawnPos = new Vector3(
            transform.position.x + halfW,
            transform.position.y + 10f,
            transform.position.z + halfD
        );

        if (playerInstance != null)
            Destroy(playerInstance);

        playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        // Attach camera switcher
        var cam = playerInstance.GetComponentInChildren<Camera>();
        FindObjectOfType<UICameraSwitcher>().SetPlayerCamera(cam);
    }

    // Determines the number of vertices on one side of the mesh for spacing.
    private int GetVertsPerAxis(GameObject prefab)
    {
        var meshFilter = prefab.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
            return Mathf.RoundToInt(Mathf.Sqrt(meshFilter.sharedMesh.vertexCount));
        Debug.LogWarning("LevelGeneration: Could not determine vertex count on tile prefab.");
        return 0;
    }

    // Checks if a circle is entirely above water using downward raycasts.
    private bool IsDryCircle(float x, float z, float worldRadius)
    {
        float RayHeight(float X, float Z)
        {
            var from = new Vector3(X, 100.3f, Z);
            if (Physics.Raycast(from, Vector3.down, out var hit, 200f))
                return hit.point.y;
            return float.MinValue;
        }

        // Check center
        if (RayHeight(x, z) <= 0.3f) return false;
        // Check 8 points around circumference
        for (int i = 0; i < 8; i++)
        {
            float angle = i / 8f * Mathf.PI * 2f;
            float sx = x + Mathf.Cos(angle) * worldRadius;
            float sz = z + Mathf.Sin(angle) * worldRadius;
            if (RayHeight(sx, sz) <= 0.3f)
                return false;
        }
        return true;
    }

    // Places all villages and returns a mask of which vertices are in any village.
    private bool[,] SpawnVillages(float originX, float originZ, float tileWorldW, float tileWorldD, float spacingX, float spacingZ, Transform villageParent)
    {
        int totalVertsDepth = levelDepthInTiles * vertsPerAxis;
        int totalVertsWidth = levelWidthInTiles * vertsPerAxis;
        bool[,] villageMask = new bool[totalVertsDepth, totalVertsWidth];

        var rng = new System.Random();
        var villages = new List<(Vector2Int center, float radiusWorld)>();
        int attempts = 0;
        int maxAttempts = 1000;

        // Try to pick up to villageCount dry centers with varied radius
        while (villages.Count < villageCount && attempts < maxAttempts)
        {
            attempts++;
            var c = new Vector2Int(
                rng.Next(0, levelWidthInTiles),
                rng.Next(0, levelDepthInTiles)
            );
            float wx = originX + c.x * tileWidth;
            float wz = originZ + c.y * tileDepth;
            float factor = Random.Range(villageRadiusMultiplierRange.x, villageRadiusMultiplierRange.y);
            float radiusWorld = villageRadius * factor * vertexSpacing;
            if (IsDryCircle(wx, wz, radiusWorld))
                villages.Add((c, radiusWorld));
        }
        if (villages.Count < villageCount)
            Debug.LogWarning($"Only found {villages.Count}/{villageCount} dry centers after {attempts} tries.");

        int startColorIdx = Random.Range(0, villageHouseColorSets.Length);

        this.villageData = villages
            .Select(v => (
                origin: new Vector3(
                    originX + v.center.x * tileWidth,
                    transform.position.y,
                    originZ + v.center.y * tileDepth
                ),
                radiusWorld: v.radiusWorld
            ))
            .ToArray();

        // For each village, try to pack houses and furniture within its radius
        for (int vi = 0; vi < villageData.Length; vi++)
        {
            Vector3 origin = villageData[vi].origin;
            float radiusWorld = villageData[vi].radiusWorld;
            float radiusSq = radiusWorld * radiusWorld;

            int setIdx = (startColorIdx + vi) % villageHouseColorSets.Length;
            var housePrefabs = villageHouseColorSets[setIdx].housePrefabs;

            // Precompute prefab radii for overlap checks
            float[] houseRadii = new float[housePrefabs.Length];
            for (int i = 0; i < housePrefabs.Length; i++)
            {
                var rend = housePrefabs[i].GetComponentInChildren<Renderer>();
                houseRadii[i] = rend != null
                    ? Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z)
                    : 1f;
            }

            // Place houses with random placement & overlap checks
            var placed = new List<(Vector3 pos, float r)>();
            for (int attempt = 0; attempt < maxHouseAttempts; attempt++)
            {
                float t = Mathf.Sqrt(Random.value);
                float angle = Random.value * Mathf.PI * 2f;
                Vector3 posXZ = origin + new Vector3(
                    Mathf.Cos(angle) * t * radiusWorld,
                    0f,
                    Mathf.Sin(angle) * t * radiusWorld
                );

                int hi = Random.Range(0, housePrefabs.Length);
                float r = houseRadii[hi];
                if (t * radiusWorld + r > radiusWorld) continue;
                if (placed.Any(ph => Vector3.Distance(ph.pos, posXZ) < (ph.r + r)))
                    continue;

                float y = SampleTerrainMeshY(posXZ.x, posXZ.z);

                var houseInstance = Instantiate(
                    housePrefabs[hi],
                    new Vector3(posXZ.x, y, posXZ.z),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    villageParent
                );
                houseInstance.transform.localScale *= 2.5f;
                placed.Add((new Vector3(posXZ.x, y, posXZ.z), r * 3.5f));
            }

            // Mark mask & scatter furniture
            for (int z = 0; z < totalVertsDepth; z++)
            {
                for (int x = 0; x < totalVertsWidth; x++)
                {
                    Vector3 worldP = new Vector3(
                        originX + x * vertexSpacing,
                        0f,
                        originZ + z * vertexSpacing
                    );
                    if ((worldP - origin).sqrMagnitude <= radiusSq)
                    {
                        villageMask[z, x] = true;
                        bool inHouse = placed.Any(ph =>
                            Vector3.Distance(ph.pos, worldP) < ph.r + 0.1f
                        );
                        if (inHouse) continue;

                        if (Random.value < furnitureDensity && villageFurniturePrefabs.Length > 0)
                        {
                            float y = SampleTerrainMeshY(worldP.x, worldP.z);
                            var pf = villageFurniturePrefabs[
                                Random.Range(0, villageFurniturePrefabs.Length)
                            ];
                            var fgo = Instantiate(
                                pf,
                                new Vector3(worldP.x, y, worldP.z),
                                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                                villageParent
                            );
                            fgo.transform.localScale *= 2.5f;
                        }
                    }
                }
            }
        }
        return villageMask;
    }

    // Samples the mesh height at a world position using a downward raycast.
    float SampleTerrainMeshY(float worldX, float worldZ)
    {
        Vector3 rayOrigin = new Vector3(worldX, 9999f, worldZ);
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 20000f))
            return hit.point.y;

        Debug.LogWarning($"Raycast miss at ({worldX},{worldZ})");
        return 0f;
    }
}

// Data Containers

public class LevelData
{
    private int tileDepthInVertices, tileWidthInVertices;
    public TileData[,] tilesData;

    public LevelData(int tileDepthInVertices, int tileWidthInVertices, int levelDepthInTiles, int levelWidthInTiles)
    {
        this.tileDepthInVertices = tileDepthInVertices;
        this.tileWidthInVertices = tileWidthInVertices;
        tilesData = new TileData[
            tileDepthInVertices * levelDepthInTiles,
            tileWidthInVertices * levelWidthInTiles
        ];
    }

    public TileCoordinate ConvertToTileCoordinate(int zIndex, int xIndex)
    {
        int tileZIndex = Mathf.FloorToInt(zIndex / (float)tileDepthInVertices);
        int tileXIndex = Mathf.FloorToInt(xIndex / (float)tileWidthInVertices);

        int coordZ = tileDepthInVertices - (zIndex % tileDepthInVertices) - 1;
        int coordX = tileWidthInVertices - (xIndex % tileWidthInVertices) - 1;
        return new TileCoordinate(tileZIndex, tileXIndex, coordZ, coordX);
    }

    public void AddTileData(TileData tileData, int tileZIndex, int tileXIndex)
    {
        tilesData[tileZIndex, tileXIndex] = tileData;
    }
}

public class TileCoordinate
{
    public int tileZIndex, tileXIndex;
    public int coordinateZIndex, coordinateXIndex;

    public TileCoordinate(int tileZIndex, int tileXIndex, int coordinateZIndex, int coordinateXIndex)
    {
        this.tileZIndex = tileZIndex;
        this.tileXIndex = tileXIndex;
        this.coordinateZIndex = coordinateZIndex;
        this.coordinateXIndex = coordinateXIndex;
    }
}
