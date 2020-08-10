using System;
using System.Collections.Generic;

[Serializable]
public struct JsonStructure {

    public string uniqueName;
    public int width, height, length;
    public JsonMaster master;
    public List<JsonPoI> pointsOfInterest;
    public List<JsonPaletteEntry> palette;
    public List<String> structure;
    public List<byte> AABB;
    
}
