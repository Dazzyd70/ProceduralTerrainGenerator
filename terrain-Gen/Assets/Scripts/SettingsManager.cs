using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Linq;

// Manages all UI settings and ties them to the procedural world generation.
// Lets the user edit, regenerate, and reset world and village parameters at runtime.

public class SettingsManager : MonoBehaviour
{
    [Header("Level Settings")]
    public LevelGeneration levelGen;
    public GenerateNoiseMap noiseMapGenerator;
    public TMP_InputField widthInput;
    public TMP_InputField heightInput;
    public TMP_InputField objectDensityInput;
    public Toggle allowWaterToggle;
    public Button regenerateButton;
    public Button resetButton;

    [Header("Noise Settings")]
    public TMP_InputField noiseScaleInput;
    public TMP_InputField heightMultiplierInput;
    public TMP_InputField octavesInput;

    [Header("Village Settings")]
    public TMP_InputField villageCountInput;
    public TMP_InputField villageRadiusInput;
    public TMP_InputField villageRadiusMultiplierInput;
    public TMP_InputField villageGraceInput;
    public TMP_InputField furnitureDensityInput;
    public TMP_InputField maxHouseAttemptsInput;

    [Header("VisMode")]
    public TMP_Dropdown visualizationModeDropdown;

    [SerializeField] private GameObject sidePanel;

    // Internal: default values for quick reset
    int defaultWidth, defaultHeight, defaultVillageCount, defaultOctaves, defaultMaxHouseAttempts;
    float defaultObjectDensity, defaultNoiseScale, defaultHeightMultiplier, defaultGrace, defaultFurnitureDensity, defaultBiomeRandomness;
    bool defaultAllowWater;
    Vector2 defaultRadiusMultiplier;
    float defaultVillageRadius;
    float[] defaultBiomeThresholds;

    void Start()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sidePanel.GetComponent<RectTransform>());
        CacheDefaultsAndInitInputs();
    }

    private void CacheDefaultsAndInitInputs()
    {
        // Grab all default values for the "Reset" button
        defaultWidth = levelGen.levelWidthInTiles;
        defaultHeight = levelGen.levelDepthInTiles;
        defaultObjectDensity = levelGen.objectDensity;
        defaultAllowWater = levelGen.allowWater;
        defaultNoiseScale = levelGen.noiseScale;
        defaultHeightMultiplier = levelGen.heightMultiplier;
        defaultOctaves = levelGen.octaves;
        defaultBiomeThresholds = levelGen.biomeThresholds.ToArray();
        defaultBiomeRandomness = levelGen.biomeRandomness;
        defaultVillageCount = levelGen.villageCount;
        defaultVillageRadius = levelGen.villageRadius;
        defaultRadiusMultiplier = levelGen.villageRadiusMultiplierRange;
        defaultGrace = levelGen.villageGraceTiles;
        defaultFurnitureDensity = levelGen.furnitureDensity;
        defaultMaxHouseAttempts = levelGen.maxHouseAttempts;

        // Populate UI inputs with default values
        widthInput.text = defaultWidth.ToString();
        heightInput.text = defaultHeight.ToString();
        objectDensityInput.text = defaultObjectDensity.ToString("F2");
        allowWaterToggle.isOn = defaultAllowWater;
        noiseScaleInput.text = defaultNoiseScale.ToString("F2");
        heightMultiplierInput.text = defaultHeightMultiplier.ToString("F2");
        octavesInput.text = defaultOctaves.ToString();
        villageCountInput.text = defaultVillageCount.ToString();
        villageRadiusInput.text = defaultVillageRadius.ToString("F1");
        villageRadiusMultiplierInput.text = $"{defaultRadiusMultiplier.x:F2},{defaultRadiusMultiplier.y:F2}";
        villageGraceInput.text = defaultGrace.ToString("F1");
        furnitureDensityInput.text = defaultFurnitureDensity.ToString("F2");
        maxHouseAttemptsInput.text = defaultMaxHouseAttempts.ToString();

        visualizationModeDropdown.onValueChanged.AddListener(OnVisualizationModeChanged);

        // Connect UI events
        widthInput.onEndEdit.AddListener(OnWidthChanged);
        heightInput.onEndEdit.AddListener(OnHeightChanged);
        objectDensityInput.onEndEdit.AddListener(OnObjectDensityChanged);
        allowWaterToggle.onValueChanged.AddListener(OnAllowWaterChanged);

        noiseScaleInput.onEndEdit.AddListener(OnNoiseScaleChanged);
        heightMultiplierInput.onEndEdit.AddListener(OnHeightMultiplierChanged);
        octavesInput.onEndEdit.AddListener(OnOctavesChanged);

        villageCountInput.onEndEdit.AddListener(OnVillageCountChanged);
        villageRadiusInput.onEndEdit.AddListener(OnVillageRadiusChanged);
        villageRadiusMultiplierInput.onEndEdit.AddListener(OnVillageRadiusMultiplierChanged);
        villageGraceInput.onEndEdit.AddListener(OnVillageGraceChanged);
        furnitureDensityInput.onEndEdit.AddListener(OnFurnitureDensityChanged);
        maxHouseAttemptsInput.onEndEdit.AddListener(OnMaxHouseAttemptsChanged);

        regenerateButton.onClick.AddListener(OnRegenerate);
        resetButton.onClick.AddListener(OnReset);
    }

    // --- Level Settings ---
    void OnWidthChanged(string value)
    {
        if (int.TryParse(value, out int width) && width > 0)
            levelGen.levelWidthInTiles = width;
        widthInput.text = levelGen.levelWidthInTiles.ToString();
    }

    void OnHeightChanged(string value)
    {
        if (int.TryParse(value, out int height) && height > 0)
            levelGen.levelDepthInTiles = height;
        heightInput.text = levelGen.levelDepthInTiles.ToString();
    }

    void OnObjectDensityChanged(string value)
    {
        if (float.TryParse(value, out float density) && density >= 0f)
            levelGen.objectDensity = density;
        objectDensityInput.text = levelGen.objectDensity.ToString("F2");
    }

    void OnAllowWaterChanged(bool allow)
    {
        levelGen.allowWater = allow;
    }

    // --- Noise Settings ---
    void OnNoiseScaleChanged(string value)
    {
        if (float.TryParse(value, out float nScale) && nScale > 0)
            levelGen.noiseScale = nScale;
        noiseScaleInput.text = levelGen.noiseScale.ToString("F2");
    }

    void OnHeightMultiplierChanged(string value)
    {
        if (float.TryParse(value, out float hMult) && hMult > 0)
            levelGen.heightMultiplier = hMult;
        heightMultiplierInput.text = levelGen.heightMultiplier.ToString("F2");
    }

    void OnOctavesChanged(string value)
    {
        if (int.TryParse(value, out int oct) && oct >= 1)
            levelGen.octaves = oct;
        octavesInput.text = levelGen.octaves.ToString();
    }

    // --- Village Settings ---
    void OnVillageCountChanged(string value)
    {
        if (int.TryParse(value, out int count) && count >= 0)
            levelGen.villageCount = count;
        villageCountInput.text = levelGen.villageCount.ToString();
    }

    void OnVillageRadiusChanged(string value)
    {
        if (float.TryParse(value, out float radius) && radius >= 0)
            levelGen.villageRadius = radius;
        villageRadiusInput.text = levelGen.villageRadius.ToString("F1");
    }

    void OnVillageRadiusMultiplierChanged(string value)
    {
        var parts = value.Split(',');
        if (parts.Length == 2 &&
            float.TryParse(parts[0], out float min) &&
            float.TryParse(parts[1], out float max) &&
            min >= 0 && max >= min)
        {
            levelGen.villageRadiusMultiplierRange = new Vector2(min, max);
        }
        var r = levelGen.villageRadiusMultiplierRange;
        villageRadiusMultiplierInput.text = $"{r.x:F2},{r.y:F2}";
    }

    void OnVillageGraceChanged(string value)
    {
        if (float.TryParse(value, out float grace) && grace >= 0)
            levelGen.villageGraceTiles = grace;
        villageGraceInput.text = levelGen.villageGraceTiles.ToString("F1");
    }

    void OnFurnitureDensityChanged(string value)
    {
        if (float.TryParse(value, out float density) && density >= 0 && density <= 1)
            levelGen.furnitureDensity = density;
        furnitureDensityInput.text = levelGen.furnitureDensity.ToString("F2");
    }

    void OnMaxHouseAttemptsChanged(string value)
    {
        if (int.TryParse(value, out int attempts) && attempts >= 0)
            levelGen.maxHouseAttempts = attempts;
        maxHouseAttemptsInput.text = levelGen.maxHouseAttempts.ToString();
    }

    // --- Visualization Mode --- 

    void OnVisualizationModeChanged(int index)
    {
        VisualizationMode selectedMode = (VisualizationMode)index;

        var tiles = FindObjectsOfType<TileGeneration>();

        foreach (var tile in tiles)
        {
            tile.visualizationMode = selectedMode;

            // Defensive: Skip if tile isn't ready yet
            if (tile.lastHeightMap == null)
                continue;

            switch (selectedMode)
            {
                case VisualizationMode.Height:
                    if (tile.chosenHeightTypes != null)
                        tile.tileRenderer.material.mainTexture = tile.BuildTexture(
                            tile.lastHeightMap,
                            tile.heightTerrainTypes,
                            tile.chosenHeightTypes
                        );
                    break;

                case VisualizationMode.Heat:
                    if (tile.chosenHeatTypes != null)
                        tile.tileRenderer.material.mainTexture = tile.BuildTexture(
                            tile.lastHeightMap, // Use lastHeatMap if you have it
                            tile.heatTerrainTypes,
                            tile.chosenHeatTypes
                        );
                    break;

                case VisualizationMode.Moisture:
                    if (tile.chosenMoistureTypes != null)
                        tile.tileRenderer.material.mainTexture = tile.BuildTexture(
                            tile.lastHeightMap, // Use lastMoistureMap if you have it
                            tile.moistureTerrainTypes,
                            tile.chosenMoistureTypes
                        );
                    break;

                case VisualizationMode.Biome:
                    if (tile.chosenHeightTypes != null && tile.chosenHeatTypes != null &&
                        tile.chosenMoistureTypes != null && tile.lastChosenBiomes != null)
                    {
                        tile.tileRenderer.material.mainTexture = tile.BuildBiomeTexture(
                            tile.chosenHeightTypes,
                            tile.chosenHeatTypes,
                            tile.chosenMoistureTypes,
                            tile.lastChosenBiomes
                        );
                    }
                    break;

                case VisualizationMode.Terrain:
                    if (tile.lastChosenBiomes != null)
                        tile.tileRenderer.material.mainTexture = tile.BuildTerrainTexture(
                            tile.lastHeightMap,
                            tile.lastChosenBiomes,
                            new bool[tile.lastHeightMap.GetLength(0), tile.lastHeightMap.GetLength(1)],
                            tile.roadColor
                        );
                    break;
            }
        }
    }



    // --- Buttons ---
    void OnRegenerate()
    {
        // Update noise settings and regenerate the map with current parameters
        noiseMapGenerator.octaves = levelGen.octaves;
        noiseMapGenerator.noiseScale = levelGen.noiseScale;
        noiseMapGenerator.RegenAllOctaves();

        levelGen.visualizationMode = (VisualizationMode)visualizationModeDropdown.value;
        levelGen.GenerateMap();

        visualizationModeDropdown.value = (int)levelGen.visualizationMode;
        OnVisualizationModeChanged(visualizationModeDropdown.value);
    }

    void OnReset()
    {
        // Restore all settings and UI elements to their defaults
        widthInput.text = defaultWidth.ToString();
        heightInput.text = defaultHeight.ToString();
        objectDensityInput.text = defaultObjectDensity.ToString("F2");
        allowWaterToggle.isOn = defaultAllowWater;
        noiseScaleInput.text = defaultNoiseScale.ToString("F2");
        heightMultiplierInput.text = defaultHeightMultiplier.ToString("F2");
        octavesInput.text = defaultOctaves.ToString();

        villageCountInput.text = defaultVillageCount.ToString();
        villageRadiusInput.text = defaultVillageRadius.ToString("F1");
        villageRadiusMultiplierInput.text = $"{defaultRadiusMultiplier.x:F2},{defaultRadiusMultiplier.y:F2}";
        villageGraceInput.text = defaultGrace.ToString("F1");
        furnitureDensityInput.text = defaultFurnitureDensity.ToString("F2");
        maxHouseAttemptsInput.text = defaultMaxHouseAttempts.ToString();

        // Set values back on the levelGen instance
        levelGen.levelWidthInTiles = defaultWidth;
        levelGen.levelDepthInTiles = defaultHeight;
        levelGen.objectDensity = defaultObjectDensity;
        levelGen.allowWater = defaultAllowWater;
        levelGen.noiseScale = defaultNoiseScale;
        levelGen.heightMultiplier = defaultHeightMultiplier;
        levelGen.octaves = defaultOctaves;

        levelGen.villageCount = defaultVillageCount;
        levelGen.villageRadius = defaultVillageRadius;
        levelGen.villageRadiusMultiplierRange = defaultRadiusMultiplier;
        levelGen.villageGraceTiles = defaultGrace;
        levelGen.furnitureDensity = defaultFurnitureDensity;
        levelGen.maxHouseAttempts = defaultMaxHouseAttempts;

        visualizationModeDropdown.value = (int)VisualizationMode.Terrain;
        levelGen.visualizationMode = (VisualizationMode)visualizationModeDropdown.value;
        OnVisualizationModeChanged(visualizationModeDropdown.value);

        // Also update noise and regenerate
        noiseMapGenerator.octaves = levelGen.octaves;
        noiseMapGenerator.noiseScale = levelGen.noiseScale;
        noiseMapGenerator.RegenAllOctaves();
        levelGen.GenerateMap();
    }
}
