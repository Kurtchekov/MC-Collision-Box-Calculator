using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System;
using System.Text;
using System.Linq;
using NUnit.Framework;

public enum Order { XYZ, XZY, ZXY, ZYX, YZX, YXZ };
public enum PaintingMode { SelectedOnly, InvalidOnly, All };
public enum Facing { DOWN, UP, FORWARD, BACK, LEFT, RIGHT };

class AABB : EditorWindow {
    [MenuItem("Window/AABB")]

    public static void ShowWindow() {
        EditorWindow.GetWindow(typeof(AABB));
    }

    [Serializable]
    public class EditorSettings {
        public int precision = 8;
        public bool invertX = true, invertY, invertZ = true;
        public int acceptableAABB = 4;
        public Order axisOrder = Order.XZY;
        public string outputFormat = "{8},{11},{0},{3},{1},{4}";
    }

    EditorSettings editorSettings = new EditorSettings();

    [Serializable]
    public class Structure {
        public string structureUID = "";
        public int masterPos = 0;
        public List<PaletteEntry> palette = new List<PaletteEntry>();
        public List<PoI> pointsOfInterest = new List<PoI>();
        public List<Block> validFullBlocks;
        public List<BlockPosition> allSubAABBs;
        public List<int> ignoredPositions = new List<int>();
        public HashSet<int> ignoredPositions2 = new HashSet<int>();
        public int totalCollisions;
    }

    Structure structure = new Structure();

    public int precision {
        get { return editorSettings.precision; }
        set {
            if(value != editorSettings.precision) {
                editorSettings.precision = value;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
                Repaint();
            }
        }
    }

    public List<Bounds> selectedSubAABBs;

    MeshFilter meshFilter;
    Mesh mesh;
    Vector3 size;

    bool drawFull, drawSubAABB, drawAll, structureMenu, showEntries;

    bool _drawStructurePos;
    bool drawStructurePos {
        get { return _drawStructurePos; }
        set { if(value != _drawStructurePos) {
                _drawStructurePos = value;
                SceneView.RepaintAll();
                Repaint();
            }
        }
    }

    int _currentBlock = 0;
    int currentBlock {
        get { return _currentBlock; }
        set { if(value != _currentBlock) {
                _currentBlock = value;
                ResetBlockPaletteValue();
                SceneView.RepaintAll();
                Repaint();
            }
        }
    }

    int _pos = 0;
    int pos {
        get { return _pos; }
        set {
            if(value != _pos) {
                _pos = value;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
                Repaint();
            }
        }
    }

    PaintingMode paintingMode;

    Vector3 posx;
    
    Vector2 palettePosition = new Vector2();
    Vector2 PoIPosition = new Vector2();

    readonly AABBGenerator calculator = new AABBGenerator();
    Thread calculatorThread;
    int progress;

    void Update() {
        if(calculatorThread != null) {
            if(calculatorThread.IsAlive) { 
                if (calculator.progress != progress) {
                    progress = calculator.progress;
                    structure.totalCollisions = calculator.totalCollisions;
                    Repaint();
                }
            } else {
                calculatorThread = null;
                structure.allSubAABBs = calculator.allBounds;
                structure.totalCollisions = calculator.totalCollisions;
                SceneView.RepaintAll();
                Repaint();
            }
        }

        Repaint();
    }

    void Reset(bool fullReset = true) {
        if (meshFilter != null) {
            mesh = meshFilter.sharedMesh;
            size = new Vector3(Mathf.Ceil(mesh.bounds.size.x), Mathf.Ceil(mesh.bounds.size.y), Mathf.Ceil(mesh.bounds.size.z));
        } else {
            mesh = null;
        }

        selectedSubAABBs = null;
        currentBlock = 0;
        calculator.Reset(mesh, editorSettings.invertX, editorSettings.invertY, editorSettings.invertZ, editorSettings.axisOrder, editorSettings.acceptableAABB);

        if(fullReset) {
            structure.allSubAABBs = null;
            structure.totalCollisions = 0;
            structure.validFullBlocks = calculator.CalculateAllBlocks();
            ResetBlockPaletteValue();
        }

        Repaint();
    }

    void OnGUI() {
        if(calculatorThread != null)
            GUI.enabled = false;
        MeshFilter newMesh;
        newMesh = (MeshFilter)EditorGUILayout.ObjectField("Object: ", meshFilter, typeof(MeshFilter), true);
        if(newMesh != meshFilter) {
            meshFilter = newMesh;
            Reset();
            return;
        }
        GUI.enabled = true;

        if(mesh == null)
            return;

        GUILayout.Label(string.Format("Size: {0} x {1} x {2}", size.x, size.y, size.z));

        if(GUILayout.Toggle(drawFull, "Show Blocks", "Button") != drawFull) {
            drawFull = !drawFull;
            SceneView.RepaintAll();
        }

        DisplayGUI();

        CalculateGUI();

        ConfigGUI();

        PaletteGUI();

        CompositionGUI();

        StructureDetailsGUI();

        ExportGUI();

        float x0 = editorSettings.invertX ? Mathf.Ceil(mesh.bounds.max.x) - 0.5f : Mathf.Floor(mesh.bounds.min.x) + 0.5f;
        float y0 = editorSettings.invertY ? Mathf.Ceil(mesh.bounds.max.y) - 0.5f : Mathf.Floor(mesh.bounds.min.y) + 0.5f;
        float z0 = editorSettings.invertZ ? Mathf.Ceil(mesh.bounds.max.z) - 0.5f : Mathf.Floor(mesh.bounds.min.z) + 0.5f;

        
        posx = new Vector3(
            x0 + (AABBGenerator.RelativeX(pos, editorSettings.axisOrder, size) * (editorSettings.invertX ? -1 : 1)),
            y0 + (AABBGenerator.RelativeY(pos, editorSettings.axisOrder, size) * (editorSettings.invertY ? -1 : 1)),
            z0 + (AABBGenerator.RelativeZ(pos, editorSettings.axisOrder, size) * (editorSettings.invertZ ? -1 : 1))
        );
    }

    void LoadEditorSettings() {
        string path = EditorUtility.OpenFilePanel("Load Editor Settings...", Application.dataPath, "json");
        if(path.Length == 0)
            return;
        using(StreamReader reader = new StreamReader(path)) {
            JsonUtility.FromJsonOverwrite(reader.ReadToEnd(), editorSettings);
        }
        Repaint();
    }

    void SaveEditorSettings() {
        string path = EditorUtility.SaveFilePanelInProject("Save Editor Settings...", "editor_settings", "json", "Save...");
        if(path.Length == 0)
            return;
        using(StreamWriter writer = new StreamWriter(path)) {
            writer.Write(JsonUtility.ToJson(editorSettings, true));
            writer.Flush();
            writer.Close();
        }
    }

    void LoadStructure() {
        string path = EditorUtility.OpenFilePanel("Load Structure...", Application.dataPath, "json");
        if(path.Length == 0)
            return;
        Reset(false);
        using(StreamReader reader = new StreamReader(path)) {
            JsonUtility.FromJsonOverwrite(reader.ReadToEnd(), structure);
            structure.ignoredPositions2 = new HashSet<int>(structure.ignoredPositions);
        }
        Repaint();
    }

    void SaveStructure() {
        string path = EditorUtility.SaveFilePanelInProject("Save Structure...", "structure", "json", "Save...");
        if(path.Length == 0)
            return;
        using(StreamWriter writer = new StreamWriter(path)) {
            structure.ignoredPositions = structure.ignoredPositions2.ToList();
            writer.Write(JsonUtility.ToJson(structure, true));
            writer.Flush();
            writer.Close();
        }
    }

    void DisplayGUI() {
        GUILayout.BeginVertical("box");
        if(GUILayout.Toggle(drawSubAABB, "Show Sub AABBs", "Button") != drawSubAABB) {
            drawSubAABB = !drawSubAABB;
            SceneView.RepaintAll();
        }

        if(drawSubAABB) {
            pos = Mathf.Clamp(EditorGUILayout.DelayedIntField("Pos: ", pos), 0, (int)size.x * (int)size.y * (int)size.z - 1);

            GUILayout.BeginHorizontal();
            if(pos == 0)
                GUI.enabled = false;
            if(GUILayout.Button("-", GUILayout.ExpandWidth(false)))
                pos--;
            GUI.enabled = true;

            pos = (int)Mathf.Floor(GUILayout.HorizontalSlider(pos, 0, size.x * size.y * size.z - 1));

            if(pos == size.x * size.y * size.z - 1)
                GUI.enabled = false;
            if(GUILayout.Button("+", GUILayout.ExpandWidth(false)))
                pos++;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        if(drawSubAABB) {
            precision = Mathf.Clamp(EditorGUILayout.DelayedIntField("Precision: ", precision), 1, 16);

            GUILayout.BeginHorizontal();
            if(precision <= 1)
                GUI.enabled = false;
            if(GUILayout.Button("-", GUILayout.ExpandWidth(false)))
                precision /= 2;
            GUI.enabled = true;

            precision = (int)Mathf.Floor(GUILayout.HorizontalSlider(precision, 1, 16));

            if(precision > 8)
                GUI.enabled = false;
            if(GUILayout.Button("+", GUILayout.ExpandWidth(false)))
                precision *= 2;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        if(drawSubAABB && selectedSubAABBs != null)
            GUILayout.Label(string.Format("Collision Boxes: {0}", selectedSubAABBs.Count));

        GUILayout.EndVertical();
    }

    void CalculateGUI() {
        GUILayout.BeginVertical("box");
        if(calculatorThread == null && GUILayout.Button("Calculate All")) {
            progress = 0;
            calculator.Reset(mesh, editorSettings.invertX, editorSettings.invertY, editorSettings.invertZ, editorSettings.axisOrder, editorSettings.acceptableAABB);
            calculatorThread = new Thread(calculator.CalculateAll);
            calculatorThread.Start();
        } else if(calculatorThread != null && GUILayout.Button("Cancel")) {
            calculatorThread.Abort();
        }

        if(calculatorThread != null) {
            GUILayout.Label(calculatorThread.IsAlive ? string.Format("running... [{0}/{1}]", progress, size.x * size.y * size.z - 1) : "Done!");
        }

        if(calculatorThread != null)
            GUI.enabled = false;
        editorSettings.acceptableAABB = Mathf.Clamp(EditorGUILayout.DelayedIntField("Acceptable # of AABBs", editorSettings.acceptableAABB), 1, 10);
        GUI.enabled = true;

        GUILayout.Label(string.Format("Total AABBs: {0}", structure.totalCollisions));

        if(structure.allSubAABBs == null)
            GUI.enabled = false;
        if(GUILayout.Toggle(drawAll, "Show All AABBs", "Button") != drawAll) {
            drawAll = !drawAll;
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        GUILayout.EndVertical();
    }

    void ExportGUI() {
        GUILayout.BeginVertical("box");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Structure"))
            SaveStructure();
        if(GUILayout.Button("Load Structure"))
            LoadStructure();
        GUILayout.EndHorizontal();

        if(structure.allSubAABBs == null)
            GUI.enabled = false;
        if(GUILayout.Button("Export"))
            Export();
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("OutputFormat:",
@"{0} = x0,
{1} = y0,
{2} = z0, 
{3} = x1, 
{4} = y1, 
{5} = z1,
{6} = Flipped x0,
{7} = Flipped y0,
{8} = Flipped z0,
{9} = Flipped x1,
{10} = Flipped y1,
{11} = Flipped z1"));
        editorSettings.outputFormat = GUILayout.TextField(editorSettings.outputFormat);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    void ConfigGUI() {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Editor Settings");
        if(calculatorThread != null)
            GUI.enabled = false;
        GUILayout.BeginHorizontal();
        if(GUILayout.Toggle(editorSettings.invertX, "Invert X", "Button") != editorSettings.invertX) {
            editorSettings.invertX = !editorSettings.invertX;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }
        if(GUILayout.Toggle(editorSettings.invertY, "Invert Y", "Button") != editorSettings.invertY) {
            editorSettings.invertY = !editorSettings.invertY;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }
        if(GUILayout.Toggle(editorSettings.invertZ, "Invert Z", "Button") != editorSettings.invertZ) {
            editorSettings.invertZ = !editorSettings.invertZ;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }

        GUILayout.EndHorizontal();

        Order newAxisOrder = (Order)EditorGUILayout.EnumPopup("Axis Order: ", editorSettings.axisOrder);
        if(newAxisOrder != editorSettings.axisOrder) {
            editorSettings.axisOrder = newAxisOrder;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Save Editor Settings"))
            SaveEditorSettings();
        if(GUILayout.Button("Load Editor Settings"))
            LoadEditorSettings();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    void PaletteGUI() {
        GUILayout.BeginVertical("box");
        showEntries = GUILayout.Toggle(showEntries, "Palette", "button");
        if(showEntries) {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Add OreDict Entry"))
                structure.palette.Add(new PaletteEntry().MakeOreDict());
            if(GUILayout.Button("Add ItemStack Entry"))
                structure.palette.Add(new PaletteEntry());
            GUILayout.EndHorizontal();
            palettePosition = EditorGUILayout.BeginScrollView(palettePosition);
            foreach(PaletteEntry item in structure.palette) {
                GUI.backgroundColor = item.isValid ? Color.green : Color.red;
                GUILayout.BeginVertical("box");
                GUI.backgroundColor = Color.white;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Identifier:");
                string id = GUILayout.TextField(item.identifier);
                if(id != item.identifier) {
                    item.identifier = id.Length > 1 ? id.Substring(0, 1) : id;
                    CheckPaletteIsValid();
                }

                GUILayout.EndHorizontal();
                if(!item.isOreDict) {
                    string mod = EditorGUILayout.TextField("Mod:", item.mod);
                    if(mod != item.mod) {
                        item.mod = mod;
                        CheckPaletteIsValid();
                    }
                }

                string name = EditorGUILayout.TextField(item.isOreDict? "OreDict Name:" : "Name:", item.name);
                if(name != item.name) {
                    item.name = name;
                    CheckPaletteIsValid();
                }

                if(!item.isOreDict) {
                    int meta = EditorGUILayout.IntField("Meta:", item.meta);
                    if(meta != item.meta && meta >= 0) {
                        item.meta = meta;
                    }
                }

                if(GUILayout.Button("Remove Entry")) {
                    structure.palette.Remove(item);
                    CheckPaletteIsValid();
                    return;
                }
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        
        GUILayout.EndVertical();
    }

    void CompositionGUI() {
        GUILayout.BeginVertical("box");
        drawStructurePos = GUILayout.Toggle(drawStructurePos, "Composition", "Button", GUILayout.ExpandWidth(true));
        if(drawStructurePos && structure.validFullBlocks != null) {
            int selectedPos = structure.validFullBlocks[currentBlock].pos;
            GUILayout.Label(string.Format("Selected position: {0}", selectedPos));
            currentBlock = EditorGUILayout.IntSlider(currentBlock, 0, structure.validFullBlocks.Count - 1);
            GUILayout.BeginHorizontal();
            if(currentBlock == 0)
                GUI.enabled = false;
            if(GUILayout.Button("Previous block"))
                currentBlock--;
            GUI.enabled = currentBlock < structure.validFullBlocks.Count - 1;
            if(GUILayout.Button("Next block"))
                currentBlock++;
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if(GUILayout.Button("Go to invalid block"))
                FindInvalidBlock();

            if(structure.palette.Count == 0)
                GUI.enabled = false;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Change all to..."))
                DisplayPaletteMenu(PaintingMode.All);
            if (GUILayout.Button("Change invalid to..."))
                DisplayPaletteMenu(PaintingMode.InvalidOnly);
            GUILayout.EndHorizontal();

            GUIStyle red = new GUIStyle(EditorStyles.textArea);
            GUIStyle green = new GUIStyle(EditorStyles.textArea);
            red.normal.textColor = Color.red;
            green.normal.textColor = Color.green;

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Palette value: {0}", structure.validFullBlocks[currentBlock].displayValue), (structure.validFullBlocks[currentBlock].IsInvalid())? red : green);
            if(GUILayout.Button("Change to..."))
                DisplayPaletteMenu(PaintingMode.SelectedOnly);
            GUILayout.EndHorizontal();

            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            bool isMaster = structure.masterPos == selectedPos;
            if(GUILayout.Toggle(isMaster, "Make master", "Button") && structure.masterPos != selectedPos)
                structure.masterPos = selectedPos;
            bool isDisabled = structure.ignoredPositions2.Contains(selectedPos);
            if(GUILayout.Toggle(isDisabled, "Disable position", "Button") != isDisabled) {
                isDisabled = !isDisabled;
                if(isDisabled && !structure.ignoredPositions2.Contains(selectedPos)) {
                    structure.ignoredPositions2.Add(selectedPos);
                    if(structure.masterPos == selectedPos)
                        structure.masterPos = 0;
                } else if (structure.ignoredPositions2.Contains(selectedPos))
                    structure.ignoredPositions2.Remove(selectedPos);
            }

            if (GUILayout.Button("Add PoI Entry")) {
                PoI newEntry = new PoI();
                newEntry.pos = selectedPos;
                structure.pointsOfInterest.Add(newEntry);
                SceneView.RepaintAll();
            }
            GUILayout.EndHorizontal();

            PoIPosition = EditorGUILayout.BeginScrollView(PoIPosition);
            foreach (PoI point in structure.pointsOfInterest) {
                if(point.pos != selectedPos)
                    continue;
                GUI.backgroundColor = point.isValid ? Color.green : Color.red;
                GUILayout.BeginVertical("box");
                GUI.backgroundColor = Color.white;
                point.id = EditorGUILayout.TextField("ID:", point.id);
                point.isValid = point.id.Length > 0;
                point.SetFacing((Facing)EditorGUILayout.EnumPopup("Facing:", point.facing));

                GUILayout.BeginHorizontal();
                point.HideArrow(GUILayout.Toggle(point.hide, "Hide Arrow", "Button"));

                if (GUILayout.Button("Remove Entry")) {
                    structure.pointsOfInterest.Remove(point);
                    SceneView.RepaintAll();
                    return;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        GUILayout.EndVertical();
    }

    void StructureDetailsGUI() {
        GUILayout.BeginVertical("box");
        structureMenu = GUILayout.Toggle(structureMenu, "Structure Details", "Button", GUILayout.ExpandWidth(true));
        if (structureMenu) {
            structure.structureUID = EditorGUILayout.TextField("Structure ID:", structure.structureUID);
        }
        GUILayout.EndVertical();
    }

    void FindInvalidBlock() {
        for (int index = 0; index < structure.validFullBlocks.Count; index++) {
            if(!structure.validFullBlocks[index].IsInvalid())
                continue;
            currentBlock = index;
        }
    }

    void DisplayPaletteMenu(PaintingMode mode) {
        paintingMode = mode;
        GenericMenu menu = new GenericMenu();
        foreach(PaletteEntry entry in structure.palette) {
            if(!entry.isValid)
                continue;
            string text;
            if(!entry.isOreDict) {
                text = string.Format("[{0}] {1}:{2}", entry.identifier, entry.mod, entry.name);
            } else
                text = string.Format("[{0}] {1}", entry.identifier, entry.name);
            bool isSelected = paintingMode == PaintingMode.SelectedOnly ? entry.identifier == structure.validFullBlocks[currentBlock].paletteValue : false;
            menu.AddItem(new GUIContent(text), isSelected, OnPaletteSelected, entry);
        }
        menu.ShowAsContext();
    }

    void OnPaletteSelected(object selected) {
        switch(paintingMode) {
            case PaintingMode.SelectedOnly:
                structure.validFullBlocks[currentBlock].SetValues(selected);
                break;
            case PaintingMode.InvalidOnly:
                foreach (Block block in structure.validFullBlocks) block.SetValuesIfInvalid(selected);
                break;
            case PaintingMode.All:
                foreach(Block block in structure.validFullBlocks) block.SetValues(selected);
                break;
        }
    }

    void ResetBlockPaletteValue() {
        if(structure.validFullBlocks == null)
            return;
        foreach (PaletteEntry entry in structure.palette) {
            if(!entry.isValid || entry.identifier != structure.validFullBlocks[currentBlock].paletteValue)
                continue;
            structure.validFullBlocks[currentBlock].SetValues(entry);
            return;
        }
        structure.validFullBlocks[currentBlock].SetValues(null);
    }

    void CheckPaletteIsValid() {
        foreach(PaletteEntry item in structure.palette) {
            foreach(PaletteEntry other in structure.palette) {
                if (item != other && item.identifier == other.identifier) {
                    item.isValid = false;
                    goto outer;
                }
            }
            if(item.name == string.Empty || item.identifier == string.Empty || !item.isOreDict && item.mod == string.Empty) {
                item.isValid = false;
                goto outer;
            }
            item.isValid = true;
            outer:
                continue;
        }
        ResetBlockPaletteValue();
    }

    void Export() {
        string path = EditorUtility.SaveFilePanelInProject("Export structure...", "struct_file", "json", "Export...");
        if(path.Length == 0)
            return;

        JsonStructure toExport = new JsonStructure();
        if (structure.structureUID == string.Empty) {
            Debug.LogError("Missing Structure ID! Check Structure Details category.");
            return;
        }
        toExport.uniqueName = structure.structureUID;
        toExport.width = (int)size.x;
        toExport.height = (int)size.y;
        toExport.length = (int)size.z;

        toExport.palette = new List<JsonPaletteEntry>();
        foreach (PaletteEntry entry in structure.palette) {
            if (!entry.isValid) {
                Debug.LogError("Invalid palette entry! Check Palette category.");
                return;
            }
            toExport.palette.Add(new JsonPaletteEntry(entry.isOreDict? "ore" : entry.mod, entry.name, entry.identifier, entry.meta));
        }

        Block master = structure.validFullBlocks.FirstOrDefault(x => x.pos == structure.masterPos);
        if (master == null) {
            Debug.LogError("Master block not set or missing palette value! Check Composition category.");
            return;
        }

        PaletteEntry masterPaletteEntry = structure.palette.FirstOrDefault(x => x.identifier == master.paletteValue);
        if(masterPaletteEntry == null) {
            Debug.LogError("Master block is missing valid palette value! Check Composition and Palette categories.");
            return;
        }

        toExport.master = new JsonMaster(
            calculator.BlockPosToLocalX(master.pos),
            calculator.BlockPosToLocalY(master.pos),
            calculator.BlockPosToLocalZ(master.pos), 
            masterPaletteEntry.isOreDict ? "ore" : 
            masterPaletteEntry.mod, masterPaletteEntry.name, masterPaletteEntry.meta);

        toExport.pointsOfInterest = new List<JsonPoI>();
        foreach (PoI poi in structure.pointsOfInterest) {
            if(structure.ignoredPositions2.Contains(poi.pos) || !structure.validFullBlocks.Any(x => x.pos == poi.pos))
                continue;
            toExport.pointsOfInterest.Add(new JsonPoI() { position = poi.pos, name = poi.id, facing = poi.facing });
        }

        int firstDimension = AABBGenerator.FirstDimension((int)size.x, (int)size.y, (int)size.z, editorSettings.axisOrder);
        int secondDimension = AABBGenerator.SecondDimension((int)size.x, (int)size.y, (int)size.z, editorSettings.axisOrder);
        int thirdDimension = AABBGenerator.ThirdDimension((int)size.x, (int)size.y, (int)size.z, editorSettings.axisOrder);

        toExport.structure = new List<string>();
        string emptyLine = new string(' ', firstDimension);
        for (int line = 0; line < secondDimension * thirdDimension; line++) {
            toExport.structure.Add(emptyLine);
        }
        foreach (Block block in structure.validFullBlocks) {
            if(structure.ignoredPositions2.Contains(block.pos))
                continue;
            if (block.IsInvalid()) {
                Debug.LogError(string.Format("Block {0} is missing valid palette value! Check Composition and Palette categories.", block.pos));
                return;
            }
            int firstPos = AABBGenerator.RelativeX(block.pos, editorSettings.axisOrder, size);
            int secondPos = AABBGenerator.RelativeZ(block.pos, editorSettings.axisOrder, size);
            int thirdPos = AABBGenerator.RelativeY(block.pos, editorSettings.axisOrder, size);
            //Debug.Log(string.Format("{0}, {1}, {2}, {3}, {4}", firstPos, secondPos, thirdPos, toExport.structure[thirdPos * secondDimension + secondPos].Length, block.paletteValue));
            toExport.structure[thirdPos * secondDimension + secondPos] = ReplaceAt(toExport.structure[thirdPos * secondDimension + secondPos], firstPos, block.paletteValue);
        }

        toExport.AABB = new List<byte>();
        List<Box>[] orderedList = new List<Box>[(int)(size.x * size.y * size.z)];

        foreach (BlockPosition aabb in structure.allSubAABBs) {
            if (orderedList[aabb.pos] == null)
                orderedList[aabb.pos] = new List<Box>();
            orderedList[aabb.pos].Add(aabb.normalizedBox);
        }

        Box fullBox = new Box(0, 0, 0, 16, 16, 16);

        StringBuilder builder = new StringBuilder();
        for (int index = 0; index < orderedList.Length; index++) {
            if(structure.ignoredPositions2.Contains(index) || orderedList[index] == null) { //no collision boxes
                builder.Append("null");
            } else if (orderedList[index].Count == 1 && orderedList[index][0] == fullBox) { //full collision box
                builder.Append("[]");
            } else { //partial collision boxes
                List<string> floats = new List<string>();
                foreach(Box box in orderedList[index]) floats.Add(box.ToString(editorSettings.outputFormat));
                builder.Append("[" + string.Join(",", floats) + "]");
            }
            builder.Append(",");
        }

        builder.Length -= 1;

        string json = JsonUtility.ToJson(toExport, false);
        json = json.Insert(json.LastIndexOf('[') + 1, builder.ToString());

        using(StreamWriter writer = new StreamWriter(path)) {
            writer.Write(json);
            writer.Flush();
            writer.Close();
        }
    }

    public static string ReplaceAt(string text, int pos, string toAdd) {
        StringBuilder builder = new StringBuilder(text);
        builder.Remove(pos, 1);
        builder.Insert(pos, toAdd);
        return builder.ToString();
    }

    void OnEnable() {
        Reset(false);
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDestroy() {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sceneView) {
        if (mesh == null)
            return;
        if (drawFull)
            DrawFullBlocks();
        if (drawSubAABB)
            DrawSubAABB();
        if (drawAll && structure.allSubAABBs != null)
            DrawAll();
        if (drawStructurePos)
            DrawStructurePos();
    }

    void DrawStructurePos() {
        int selectedPos = structure.validFullBlocks[currentBlock].pos;
        Handles.color = Color.green;
        Vector3 worldPos = calculator.BlockPosToWorldVector3(selectedPos);
        Handles.DrawWireCube(worldPos, new Vector3(1f, 1f, 1f));
        foreach (PoI point in structure.pointsOfInterest) {
            if(point.hide || point.pos != selectedPos)
                continue;
            Handles.ArrowHandleCap(0, worldPos, Quaternion.LookRotation(DirectionFromFacing(point.facing)), 1, EventType.Repaint);
        }
    }

    Vector3 DirectionFromFacing(Facing facing) {
        switch(facing) {
            case Facing.BACK: return Vector3.forward;
            case Facing.FORWARD: return Vector3.back;
            case Facing.LEFT: return Vector3.right;
            case Facing.RIGHT: return Vector3.left;
            case Facing.UP: return Vector3.up;
            case Facing.DOWN: return Vector3.down;
        }
        return Vector3.zero;
    }

    void DrawFullBlocks() {
        Handles.color = Color.yellow;
        foreach (Block block in structure.validFullBlocks) {
            Handles.DrawWireCube(calculator.BlockPosToWorldVector3(block.pos), new Vector3(1, 1, 1));
        }
    }

    void DrawSubAABB() {
        if(selectedSubAABBs == null && calculatorThread == null) {
            calculator.Reset(mesh, editorSettings.invertX, editorSettings.invertY, editorSettings.invertZ, editorSettings.axisOrder, editorSettings.acceptableAABB);
            selectedSubAABBs = calculator.CalculateSingle(posx.x, posx.y, posx.z, precision);
        }
        Handles.color = Color.green;
        foreach(Bounds bounds in selectedSubAABBs) {
            Handles.DrawWireCube(bounds.center, bounds.size);
        }
    }

    void DrawAll() {
        Handles.color = Color.green;
        foreach (BlockPosition pos in structure.allSubAABBs) {
            Handles.DrawWireCube(pos.bounds.center, pos.bounds.size);
        }
    }
}
