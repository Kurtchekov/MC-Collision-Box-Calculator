using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System;

public enum Order { XYZ, XZY, ZXY, ZYX, YZX, YXZ };

class AABB : EditorWindow {
    [MenuItem("Window/AABB")]

    public static void ShowWindow() {
        EditorWindow.GetWindow(typeof(AABB));
    }

    MeshFilter meshFilter;
    Mesh mesh;
    Vector3 size;

    bool drawFull, drawSelected, drawSubAABB, drawAll;

    bool invertX, invertY, invertZ = true;

    int acceptableAABB = 4;

    Order axisOrder = Order.XZY;

    int pos = 1;
    int precision = 8;
    int collisions = 0;
    Vector3 posx;

    List<Bounds> allFullBlocks;
    List<Bounds> selectedSubAABBs;
    List<List<Bounds>> allSubAABBs;
    List<List<Box>> normalizedBoxes;
    int totalCollisions;

    readonly AABBGenerator calculator = new AABBGenerator();
    Thread calculatorThread;
    int progress;

    string outputFormat = "{8}, {11}, {0}, {3}, {1}, {4}";

    void Update() {
        if(calculatorThread != null) {
            if(calculatorThread.IsAlive) { 
                if (calculator.progress != progress) {
                    progress = calculator.progress;
                    totalCollisions = calculator.totalCollisions;
                    Repaint();
                }
            } else {
                calculatorThread = null;
                allSubAABBs = calculator.allBounds;
                normalizedBoxes = calculator.normalizedBoxes;
                totalCollisions = calculator.totalCollisions;
                SceneView.RepaintAll();
                Repaint();
            }
        }

        Repaint();
    }

    void Reset() {
        if (meshFilter != null) {
            mesh = meshFilter.sharedMesh;
            size = new Vector3(Mathf.Ceil(mesh.bounds.size.x), Mathf.Ceil(mesh.bounds.size.y), Mathf.Ceil(mesh.bounds.size.z));
        } else {
            mesh = null;
        }

        allFullBlocks = null;
        selectedSubAABBs = null;
        allSubAABBs = null;
        normalizedBoxes = null;
        collisions = 0;
        totalCollisions = 0;  
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

        GUILayout.BeginVertical("box");
        if(GUILayout.Toggle(drawSelected, "Show Selected Pos", "Button") != drawSelected) {
            drawSelected = !drawSelected;
            SceneView.RepaintAll();
        }
        if(GUILayout.Toggle(drawSubAABB, "Show Sub AABBs", "Button") != drawSubAABB) {
            drawSubAABB = !drawSubAABB;
            SceneView.RepaintAll();
        }

        if (drawSelected || drawSubAABB) {
            int newPos = Mathf.Clamp(EditorGUILayout.DelayedIntField("Pos: ", pos), 0, (int)size.x * (int)size.y * (int)size.z - 1);

            if(newPos != pos) {
                pos = newPos;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }

            GUILayout.BeginHorizontal();
            if(pos == 0)
                GUI.enabled = false;
            if(GUILayout.Button("-", GUILayout.ExpandWidth(false))) {
                pos -= 1;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }
            GUI.enabled = true;

            newPos = (int)Mathf.Floor(GUILayout.HorizontalSlider(pos, 0, size.x * size.y * size.z - 1));
            if(newPos != pos) {
                pos = newPos;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }

            if(pos == size.x * size.y * size.z - 1)
                GUI.enabled = false;
            if(GUILayout.Button("+", GUILayout.ExpandWidth(false))) {
                pos += 1;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        if(drawSubAABB) {
            int newPrecision = Mathf.Clamp(EditorGUILayout.DelayedIntField("Precision: ", precision), 1, 16);
            if(newPrecision != precision) {
                precision = newPrecision;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }

            GUILayout.BeginHorizontal();
            if(precision <= 1)
                GUI.enabled = false;
            if(GUILayout.Button("-", GUILayout.ExpandWidth(false))) {
                precision /= 2;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }
            GUI.enabled = true;

            newPrecision = (int)Mathf.Floor(GUILayout.HorizontalSlider(precision, 1, 16));
            if(newPrecision != precision) {
                precision = newPrecision;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }

            if(precision > 8)
                GUI.enabled = false;
            if(GUILayout.Button("+", GUILayout.ExpandWidth(false))) {
                precision *= 2;
                selectedSubAABBs = null;
                SceneView.RepaintAll();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();

        GUILayout.BeginVertical("box");
        if (calculatorThread == null && GUILayout.Button("Calculate All")) {
            progress = 0;
            calculator.Reset(mesh, invertX, invertY, invertZ, axisOrder, acceptableAABB);
            calculatorThread = new Thread(calculator.CalculateAll);
            calculatorThread.Start();
        } else if (calculatorThread != null && GUILayout.Button("Cancel")) {
            calculatorThread.Abort();
        }

        if (calculatorThread != null) {
            GUILayout.Label(calculatorThread.IsAlive ? string.Format("running... [{0}/{1}]", progress, size.x * size.y * size.z - 1) : "Done!");
        }

        if(calculatorThread != null)
            GUI.enabled = false;
        acceptableAABB = Mathf.Clamp(EditorGUILayout.DelayedIntField("Acceptable # of AABBs", acceptableAABB), 1, 10);
        GUI.enabled = true;

        GUILayout.Label(string.Format("Total AABBs: {0}", totalCollisions));

        if (allSubAABBs == null)
            GUI.enabled = false;
        if (GUILayout.Toggle(drawAll, "Show All AABBs", "Button") != drawAll) {
            drawAll = !drawAll;
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        if(drawSubAABB)
            GUILayout.Label(string.Format("Collision Boxes: {0}", collisions));
        GUILayout.EndVertical();

        GUILayout.BeginVertical("box");
        if(calculatorThread != null)
            GUI.enabled = false;
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(invertX, "Invert X", "Button") != invertX) {
            invertX = !invertX;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }
        if (GUILayout.Toggle(invertY, "Invert Y", "Button") != invertY) {
            invertY = !invertY;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }
        if (GUILayout.Toggle(invertZ, "Invert Z", "Button") != invertZ) {
            invertZ = !invertZ;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }

        GUILayout.EndHorizontal();

        Order newAxisOrder = (Order)EditorGUILayout.EnumPopup("Axis Order: ", axisOrder);
        if (newAxisOrder != axisOrder) {
            axisOrder = newAxisOrder;
            selectedSubAABBs = null;
            SceneView.RepaintAll();
        }
        GUI.enabled = true;
        GUILayout.EndVertical();

        GUILayout.BeginVertical("box");
        if(normalizedBoxes == null)
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
        outputFormat = GUILayout.TextField(outputFormat);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        float x0 = invertX ? Mathf.Ceil(mesh.bounds.max.x) - 0.5f : Mathf.Floor(mesh.bounds.min.x) + 0.5f;
        float y0 = invertY ? Mathf.Ceil(mesh.bounds.max.y) - 0.5f : Mathf.Floor(mesh.bounds.min.y) + 0.5f;
        float z0 = invertZ ? Mathf.Ceil(mesh.bounds.max.z) - 0.5f : Mathf.Floor(mesh.bounds.min.z) + 0.5f;

        
        posx = new Vector3(
            x0 + (AABBGenerator.RelativeX(pos, axisOrder, size) * (invertX ? -1 : 1)),
            y0 + (AABBGenerator.RelativeY(pos, axisOrder, size) * (invertY ? -1 : 1)),
            z0 + (AABBGenerator.RelativeZ(pos, axisOrder, size) * (invertZ ? -1 : 1))
        );
    }

    void Export() {
        Box fullBox = new Box(0, 0, 0, 16, 16, 16);
        string path = EditorUtility.SaveFilePanelInProject("Save Collision Boxes...", "AABB", "txt", "Save...");
        if(path.Length == 0)
            return;
        using(StreamWriter writer = new StreamWriter(path)) {
            for (int index = 0; index < normalizedBoxes.Count; index++) {
                if(normalizedBoxes[index].Count == 0) { //no collision boxes
                    writer.Write("null");
                } else if (normalizedBoxes[index].Count == 1 && normalizedBoxes[index][0] == fullBox) { //full collision box
                    writer.Write("{}");
                } else { //partial collision boxes
                    List<string> floats = new List<string>();
                    foreach(Box box in normalizedBoxes[index]) floats.Add(box.ToString(outputFormat));
                    writer.Write("{" + string.Join(",", floats) + "}");
                }
                if(index < normalizedBoxes.Count - 1)
                    writer.Write(",");
                writer.WriteLine();
            }
            writer.Flush();
        }
    }

    void OnEnable() {
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
        if (drawSelected)
            DrawSelectedBlock();
        if (drawSubAABB)
            DrawSubAABB();
        if (drawAll && allSubAABBs != null)
            DrawAll();
    }

    void DrawFullBlocks() {
        if (allFullBlocks == null && calculatorThread == null) {
            calculator.Reset(mesh, invertX, invertY, invertZ, axisOrder, acceptableAABB);
            allFullBlocks = calculator.CalculateAllBlocks();
        }
        Handles.color = Color.yellow;
        foreach(Bounds blockBounds in allFullBlocks) {
            Handles.DrawWireCube(blockBounds.center, blockBounds.size);
        }
    }

    void DrawSelectedBlock() {
        Handles.color = Color.yellow;
        Handles.DrawWireCube(posx, new Vector3(1f, 1f, 1f));
    }

    void DrawSubAABB() {
        if(selectedSubAABBs == null && calculatorThread == null) {
            calculator.Reset(mesh, invertX, invertY, invertZ, axisOrder, acceptableAABB);
            selectedSubAABBs = calculator.CalculateSingle(posx.x, posx.y, posx.z, precision);
        }
        Handles.color = Color.green;
        foreach(Bounds bounds in selectedSubAABBs) {
            Handles.DrawWireCube(bounds.center, bounds.size);
        }
    }

    void DrawAll() {
        Handles.color = Color.green;
        foreach (List<Bounds> blockBounds in allSubAABBs) {
            foreach (Bounds bounds in blockBounds) {
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}
