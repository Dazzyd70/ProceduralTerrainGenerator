using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileData
{
    public float[,] heightMap;
    public float[,] heatMap;
    public float[,] moistureMap;

    public TerrainType[,] chosenHeightTerrainTypes;
    public TerrainType[,] chosenHeatTerrainTypes;
    public TerrainType[,] chosenMoistureTerrainTypes;

    public Biome[,] chosenBiomes;

    public Transform tileTransform;
    public Mesh mesh;

    

    public TileData(
        float[,] heightMap,
        float[,] heatMap,
        float[,] moistureMap,
        TerrainType[,] chosenHeightTerrainTypes,
        TerrainType[,] chosenHeatTerrainTypes,
        TerrainType[,] chosenMoistureTerrainTypes,
        Biome[,] chosenBiomes,
        Transform tileTransform,
        Mesh mesh
    )
    {
        this.heightMap = heightMap;
        this.heatMap = heatMap;
        this.moistureMap = moistureMap;

        this.chosenHeightTerrainTypes = chosenHeightTerrainTypes;
        this.chosenHeatTerrainTypes = chosenHeatTerrainTypes;
        this.chosenMoistureTerrainTypes = chosenMoistureTerrainTypes;

        this.chosenBiomes = chosenBiomes;

        this.tileTransform = tileTransform;
        this.mesh = mesh;
    }
}

[System.Serializable]
public class TerrainType
{
    public string name;
    public float threshold;
    public Color color;
    public int index;
}

[System.Serializable]
public class Biome
{
    public string name;
    public Color color;
    public int index;
}

[System.Serializable]
public class BiomeRow
{
    public Biome[] biomes;
}
