using System;

[Serializable]
public struct JsonMaster {

    public int x, y, z, meta;
    public string mod, name;

    public JsonMaster(int x, int y, int z, string mod, string name, int meta) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.mod = mod;
        this.name = name;
        this.meta = meta;
    }

}
