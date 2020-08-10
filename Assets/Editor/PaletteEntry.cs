using System;

[Serializable]
public class PaletteEntry {

    public string name = string.Empty;
    public string identifier = string.Empty;
    public int meta = 0;
    public bool isValid = false;
    public string mod = string.Empty;
    public bool isOreDict = false;

    public PaletteEntry MakeOreDict() {
        isOreDict = true;
        return this;
    }

}
