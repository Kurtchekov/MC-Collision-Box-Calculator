using System;

[Serializable]
public class Block {

    public string displayValue = "None";
    public string paletteValue = "";
    public int pos;

    public Block(int pos) {
        this.pos = pos;
    }

    public bool IsInvalid() {
        return paletteValue == "";
    }

    public void SetValuesIfInvalid(object entry) {
        if(!IsInvalid())
            return;
        SetValues(entry);
    }

    public void SetValues(object value) {
        if (value == null) {
            displayValue = "None";
            paletteValue = "";
        } else if(value is PaletteEntry entry) {
            if(!entry.isOreDict) {
                displayValue = string.Format("[{0}] {1}:{2}", entry.identifier, entry.mod, entry.name);
                paletteValue = entry.identifier;
            } else {
                displayValue = string.Format("[{0}] {1}", entry.identifier, entry.name);
                paletteValue = entry.identifier;
            }
        }
    }

}
