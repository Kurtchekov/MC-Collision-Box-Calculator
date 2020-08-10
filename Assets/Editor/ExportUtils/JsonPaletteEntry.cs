using System;

[Serializable]
public struct JsonPaletteEntry {

    public string mod, name, character;
    public int meta;

    public JsonPaletteEntry(string mod, string name, string character, int meta) {
        this.mod = mod;
        this.name = name;
        this.character = character;
        this.meta = meta;
    }
}
