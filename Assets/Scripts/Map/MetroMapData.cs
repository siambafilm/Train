using System;

[Serializable]
public class MetroMapData
{
    public MapSettings settings;
    public PrefabRefs prefabs;
    public ColorEntry[] lineColors;
    public LineData[] lines;
}

[Serializable]
public class MapSettings
{
    public float schematicScale = 0.05f;
    public float heightStep = 4f;
    public float trackSpacing = 1.5f;
    public float tunnelSegmentLength = 2f;
    public int smoothingSubdivisions = 10;
    public float depotOffset = 6f;
    public float stationLength = 2f;
    public float depotLength = 3f;
    public float fallbackSphereSize = 1f;
}

[Serializable]
public class PrefabRefs
{
    public string station;
    public string transferStation;
    public string depot;
    public string tunnelSegment;
}

[Serializable]
public class ColorEntry
{
    public string key;
    public string hex;
}

[Serializable]
public class LineData
{
    public string id;
    public string name;
    public string colorKey;
    public int heightLayer = -1;
    public StationData[] stations;
}

[Serializable]
public class StationData
{
    public string id;
    public string name;
    public float[] pos;        // [x, y] в схемных координатах
    public string transferTo;  // id станции-пары на другой линии (опционально)
}