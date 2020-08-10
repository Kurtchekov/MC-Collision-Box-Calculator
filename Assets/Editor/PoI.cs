using System;
using UnityEditor;
[Serializable]
public class PoI {

    public int pos = -1;
    public string id = "";
    public Facing facing = Facing.FORWARD;
    public bool isValid = false;
    public bool hide = false;

    public void HideArrow(bool hide) {
        if (this.hide == hide) return;
        this.hide = hide;
        SceneView.RepaintAll();
    }

    public void SetFacing(Facing newFacing) {
        if(newFacing == facing) return;
        facing = newFacing;
        SceneView.RepaintAll();
    }
}
