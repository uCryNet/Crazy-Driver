using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using System.Collections.Generic;

namespace EasyLine
{
    [CustomEditor(typeof(BezierSpline))]
    public class BezierSplineEditor : Editor
    {
        private BezierSpline spline;
        private Transform handleTransform;
        private Quaternion handleRotation;
    
        private const int lineSteps = 20;
        private int selectedIndex = -1;
    
        private SplineMeshDeformer[] activeDeformers;
    
        // Notify any SplineMeshDeformer using this spline to auto-update
        private void NotifyConnectedDeformers(bool forceNotDragging = false)
        {
            bool interacting = GUIUtility.hotControl != 0;

            // Refresh the cache on every discrete action (button, inspector edit, end of drag), but
            // reuse it while a handle is held. The user cannot add or remove a deformer mid-drag, so
            // the set is guaranteed stable there - and that is the one path where this runs per mouse
            // event, so a full scene scan would be far too expensive. Without the refresh a deformer
            // added while the spline stays selected would never auto-update, because OnEnable only
            // runs on (re)selection.
            if (activeDeformers == null || !interacting)
                activeDeformers = Object.FindObjectsByType<SplineMeshDeformer>();

            bool isDragging = !forceNotDragging && interacting;

            foreach (var d in activeDeformers)
            {
                if (d != null && d.spline == spline && d.autoDeform)
                {
                    d.Deform(isDragging);
                }
            }
        }
    
        private void OnEnable()
        {
            spline = target as BezierSpline;
            if (spline.points == null || spline.points.Length == 0)
            {
                spline.Reset();
            }
            activeDeformers = Object.FindObjectsByType<SplineMeshDeformer>();
        }
    
        public override void OnInspectorGUI()
        {
            spline = target as BezierSpline;
    
            // Auto-repair anchorRolls and anchorScales arrays
            int expectedAnchorCount = spline.CurveCount + 1;
            if (spline.anchorRolls == null || spline.anchorRolls.Length != expectedAnchorCount)
            {
                float[] newRolls = new float[expectedAnchorCount];
                if (spline.anchorRolls != null)
                {
                    for (int i = 0; i < Mathf.Min(spline.anchorRolls.Length, expectedAnchorCount); i++)
                        newRolls[i] = spline.anchorRolls[i];
                }
                spline.anchorRolls = newRolls;
                EditorUtility.SetDirty(spline); spline.MarkDirty();
            }
    
            if (spline.anchorScales == null || spline.anchorScales.Length != expectedAnchorCount)
            {
                Vector3[] newScales = new Vector3[expectedAnchorCount];
                for (int i = 0; i < expectedAnchorCount; i++) newScales[i] = Vector3.one;
    
                if (spline.anchorScales != null)
                {
                    for (int i = 0; i < Mathf.Min(spline.anchorScales.Length, expectedAnchorCount); i++)
                        newScales[i] = spline.anchorScales[i];
                }
                spline.anchorScales = newScales;
                EditorUtility.SetDirty(spline); spline.MarkDirty();
            }
    
            EditorGUI.BeginChangeCheck();
            bool loop = EditorGUILayout.Toggle("Loop Curve", spline.loop);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Toggle Loop");
                spline.loop = loop;
                if (loop)
                {
                    spline.points[spline.points.Length - 1] = spline.points[0];
                    AlignLoopTangents();
                }
                EditorUtility.SetDirty(spline); spline.MarkDirty();
                NotifyConnectedDeformers();
            }
    
            DrawDefaultInspector();
    
            GUILayout.Space(10);
    
            // --- Curve Info ---
            EditorGUILayout.LabelField("Curve Info", EditorStyles.boldLabel);
            if (spline.points != null && spline.points.Length >= 4)
            {
                BezierSpline.CurveDistanceMapper mapper = spline.GetMapper(0f, 100);
                float totalLength = mapper.GetTotalPhysicalLength();
                EditorGUILayout.HelpBox(
                    $"Segments: {spline.CurveCount}  |  Points: {spline.points.Length}  |  Length: {totalLength:F2} units", 
                    MessageType.Info
                );
            }
    
            GUILayout.Space(5);
    
            // --- Add / Remove Segment Buttons ---
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("+ Add Segment", GUILayout.Height(28)))
            {
                Undo.RecordObject(spline, "Add Curve Segment");
                AddSegment();
                EditorUtility.SetDirty(spline); spline.MarkDirty();
                NotifyConnectedDeformers();
            }
    
            GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
            EditorGUI.BeginDisabledGroup(spline.points == null || spline.points.Length <= 4);
            if (GUILayout.Button("- Remove Last", GUILayout.Height(28)))
            {
                Undo.RecordObject(spline, "Remove Last Segment");
                RemoveLastSegment();
                EditorUtility.SetDirty(spline); spline.MarkDirty();
                NotifyConnectedDeformers();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
    
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Smooth All Tangents", GUILayout.Height(22)))
            {
                Undo.RecordObject(spline, "Smooth All Tangents");
                SmoothAllTangents();
                EditorUtility.SetDirty(spline); spline.MarkDirty();
                NotifyConnectedDeformers();
            }
            if (GUILayout.Button("Reset All Scales", GUILayout.Height(22)))
            {
                Undo.RecordObject(spline, "Reset All Scales");
                for (int i = 0; i < spline.anchorScales.Length; i++) spline.anchorScales[i] = Vector3.one;
                EditorUtility.SetDirty(spline); spline.MarkDirty();
                NotifyConnectedDeformers();
            }
            EditorGUILayout.EndHorizontal();
    
            GUI.backgroundColor = Color.white;
    
            GUILayout.Space(5);
    
            // --- Reset Button ---
            if (GUILayout.Button("Reset Curve", GUILayout.Height(25)))
            {
                Undo.RecordObject(spline, "Reset Curve");
                spline.Reset();
                selectedIndex = -1;
                EditorUtility.SetDirty(spline); spline.MarkDirty();
                NotifyConnectedDeformers();
            }
    
            // --- Selected Point Info ---
            if (selectedIndex >= 0 && selectedIndex < spline.points.Length)
            {
                GUILayout.Space(10);
                
                EditorGUILayout.BeginVertical(GUI.skin.box); // Wrap in a nice box
                
                bool isAnchor = selectedIndex % 3 == 0;
                string pointName = isAnchor ? "Anchor Point " + (selectedIndex / 3) : "Control Point " + selectedIndex;
                
                EditorGUILayout.LabelField($"Selected: {pointName}", EditorStyles.boldLabel);
                GUILayout.Space(2);
                
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = EditorGUILayout.Vector3Field("Position", spline.points[selectedIndex]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(spline, "Move Point");
                    spline.points[selectedIndex] = newPos;
                    EditorUtility.SetDirty(spline); spline.MarkDirty();
                    NotifyConnectedDeformers();
                }
    
                if (isAnchor)
                {
                    int anchorIndex = selectedIndex / 3;
                    if (spline.anchorRolls != null && anchorIndex < spline.anchorRolls.Length)
                    {
                        GUILayout.Space(8);
                        EditorGUILayout.LabelField("Banking \u21BA", EditorStyles.boldLabel); // \u21BA is a curving arrow symbol
                        
                        EditorGUI.BeginChangeCheck();
                        float newRoll = EditorGUILayout.Slider("Roll Angle", spline.anchorRolls[anchorIndex], -180f, 180f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(spline, "Change Bank Angle");
                            spline.anchorRolls[anchorIndex] = newRoll;
                            EditorUtility.SetDirty(spline); spline.MarkDirty();
                            NotifyConnectedDeformers();
                        }
    
                        if (spline.anchorScales != null && anchorIndex < spline.anchorScales.Length)
                        {
                            GUILayout.Space(8);
                            EditorGUILayout.LabelField("Scale \u21F1", EditorStyles.boldLabel);
                            EditorGUI.BeginChangeCheck();
                            Vector3 newScale = EditorGUILayout.Vector3Field("Scale", spline.anchorScales[anchorIndex]);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(spline, "Change Anchor Scale");
                                spline.anchorScales[anchorIndex] = newScale;
                                EditorUtility.SetDirty(spline); spline.MarkDirty();
                                NotifyConnectedDeformers();
                            }
                        }
                    }
                }
                
                EditorGUILayout.EndVertical();
            }
        }
    
        private void AddSegment()
        {
            if (spline.points == null || spline.points.Length < 4) return;
            
            Vector3 lastPoint = spline.points[spline.points.Length - 1];
            Vector3 lastDir = (lastPoint - spline.points[spline.points.Length - 2]).normalized;
            if (lastDir.sqrMagnitude < 0.001f) lastDir = Vector3.forward;
            
            float segmentLength = 2f;
            
            List<Vector3> pts = new List<Vector3>(spline.points);
            pts.Add(lastPoint + lastDir * segmentLength * 0.33f);     // Control 1
            pts.Add(lastPoint + lastDir * segmentLength * 0.66f);     // Control 2
            pts.Add(lastPoint + lastDir * segmentLength);              // New Anchor
            spline.points = pts.ToArray();
            
            if (spline.anchorRolls != null)
            {
                List<float> rolls = new List<float>(spline.anchorRolls);
                rolls.Add(0f);
                spline.anchorRolls = rolls.ToArray();
            }
            
            if (spline.anchorScales != null)
            {
                List<Vector3> scales = new List<Vector3>(spline.anchorScales);
                scales.Add(Vector3.one);
                spline.anchorScales = scales.ToArray();
            }
            
            if (spline.loop) spline.loop = false;
        }
    
        private void RemoveLastSegment()
        {
            if (spline.points == null || spline.points.Length <= 4) return;
            
            List<Vector3> pts = new List<Vector3>(spline.points);
            pts.RemoveRange(pts.Count - 3, 3);
            spline.points = pts.ToArray();
            
            if (spline.anchorRolls != null && spline.anchorRolls.Length > 2)
            {
                List<float> rolls = new List<float>(spline.anchorRolls);
                rolls.RemoveAt(rolls.Count - 1);
                spline.anchorRolls = rolls.ToArray();
            }
    
            if (spline.anchorScales != null && spline.anchorScales.Length > 2)
            {
                List<Vector3> scales = new List<Vector3>(spline.anchorScales);
                scales.RemoveAt(scales.Count - 1);
                spline.anchorScales = scales.ToArray();
            }
            
            if (selectedIndex >= spline.points.Length) selectedIndex = -1;
        }
    
        private bool wasDragging = false;
        
        private void OnSceneGUI()
        {
            spline = target as BezierSpline;
            handleTransform = spline.transform;
            handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;
    
            // --- 1. TOOL CONFLICT PREVENTION ---
            // If we are currently DRAWING with the SplineDrawTool, the editor must NOT draw 
            // interactive handles that steal clicks from the tool (especially at A0).
            bool isDrawingToolActive = ToolManager.activeToolType == typeof(SplineDrawTool);
    
            bool isDragging = GUIUtility.hotControl != 0;
            if (wasDragging && !isDragging)
            {
                // Drag just ended! Force a full update to cook the static colliders
                NotifyConnectedDeformers(false);
                wasDragging = false;
            }
            else if (isDragging)
            {
                wasDragging = true;
            }
    
            if (spline.points == null || spline.points.Length < 4) return;
    
            // Draw control point tangent lines and handles
            Vector3 p0 = ShowPoint(0, isDrawingToolActive);
            for (int i = 1; i < spline.points.Length; i += 3)
            {
                Vector3 p1 = ShowPoint(i, isDrawingToolActive);
                Vector3 p2 = ShowPoint(i + 1, isDrawingToolActive);
                Vector3 p3 = ShowPoint(i + 2, isDrawingToolActive);
    
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                if (i < spline.points.Length) Handles.DrawDottedLine(p0, p1, 4f);
                if (i + 2 < spline.points.Length) Handles.DrawDottedLine(p2, p3, 4f);
    
                p0 = p3;
            }
    
            // Draw the curve itself
            int stepsPerCurve = 30; // Increased for smoothness
            int totalSteps = spline.CurveCount * stepsPerCurve;
            Vector3 lineStartWorld = handleTransform.TransformPoint(spline.GetPoint(0f));
            
            for (int i = 1; i <= totalSteps; i++)
            {
                float t = i / (float)totalSteps;
                Vector3 lineEndWorld = handleTransform.TransformPoint(spline.GetPoint(t));
                
                // Color gradient along curve: green -> cyan -> blue
                float hue = Mathf.Lerp(0.35f, 0.55f, t);
                Handles.color = Color.HSVToRGB(hue, 0.8f, 1f);
                
                // --- Optional projection line (commented out or could be a separate toggle) ---
                // Handles.color = new Color(1,1,1, 0.2f);
                // Handles.DrawLine(lineEndWorld, ProjectToSurface(lineEndWorld));

                Handles.color = Color.HSVToRGB(hue, 0.8f, 1f);
                Handles.DrawLine(lineStartWorld, lineEndWorld);
                lineStartWorld = lineEndWorld;
            }
    
            // Draw length label at midpoint
            if (spline.CurveCount > 0)
            {
                Vector3 midPoint = handleTransform.TransformPoint(spline.GetPoint(0.5f));
                BezierSpline.CurveDistanceMapper mapper = spline.GetMapper(0f, 50);
                float len = mapper.GetTotalPhysicalLength();
                
                GUIStyle lengthStyle = new GUIStyle();
                lengthStyle.normal.textColor = new Color(0.5f, 1f, 1f);
                lengthStyle.fontStyle = FontStyle.Bold;
                lengthStyle.fontSize = 12;
                Handles.Label(midPoint + Vector3.up * 0.5f, $"Length: {len:F1}", lengthStyle);
            }
    
            // Handle click-on-curve to insert point
            HandleCurveClick();
        }
    
        private void HandleCurveClick()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && e.shift) // Shift + Right Click
            {
                // Find closest point on curve to mouse
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                
                float bestDist = float.MaxValue;
                float bestT = 0f;
                int steps = 100;
                
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector3 curvePt = handleTransform.TransformPoint(spline.GetPoint(t));
                    float dist = HandleUtility.DistancePointLine(curvePt, ray.origin, ray.origin + ray.direction * 1000f);
                    
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestT = t;
                    }
                }
                
                // Only insert if reasonably close 
                if (bestDist < 2f)
                {
                    Undo.RecordObject(spline, "Insert Curve Point");
                    InsertPointAtT(bestT);
                    EditorUtility.SetDirty(spline); spline.MarkDirty();
                    e.Use();
                }
            }
        }
    
        private void InsertPointAtT(float t)
        {
            // Find which segment this t falls into
            int segmentIndex = Mathf.FloorToInt(t * spline.CurveCount);
            segmentIndex = Mathf.Clamp(segmentIndex, 0, spline.CurveCount - 1);
            
            // De Casteljau subdivision at local t within this segment
            float localT = (t * spline.CurveCount) - segmentIndex;
            int i = segmentIndex * 3;
            
            Vector3 p0 = spline.points[i];
            Vector3 p1 = spline.points[i + 1];
            Vector3 p2 = spline.points[i + 2];
            Vector3 p3 = spline.points[i + 3];
            
            // De Casteljau algorithm - split bezier at localT
            Vector3 a = Vector3.Lerp(p0, p1, localT);
            Vector3 b = Vector3.Lerp(p1, p2, localT);
            Vector3 c = Vector3.Lerp(p2, p3, localT);
            Vector3 d = Vector3.Lerp(a, b, localT);
            Vector3 ee = Vector3.Lerp(b, c, localT);
            Vector3 f = Vector3.Lerp(d, ee, localT); // The new anchor point
            
            // Build new points array: replace original 4 points with 7 points (two segments)
            List<Vector3> pts = new List<Vector3>(spline.points);
            
            // Remove original control points (but keep anchors positions via replacement)
            pts[i + 1] = a;
            pts[i + 2] = d;
            // Insert: new anchor + new controls + adjust existing next control
            pts.Insert(i + 3, f);      // New anchor
            pts.Insert(i + 4, ee);     // New control 2 for second sub-segment
            pts.Insert(i + 5, c);      // Adjusted control for original next anchor
            // The original p3 is now at i + 6
            
            spline.points = pts.ToArray();
        }
    
        private Vector3 ShowPoint(int index, bool disableInteraction = false)
        {
            if (spline.points == null || index < 0 || index >= spline.points.Length) 
                return Vector3.zero;
    
            Vector3 point = handleTransform.TransformPoint(spline.points[index]);
            float size = HandleUtility.GetHandleSize(point);
            
            bool isAnchor = (index % 3 == 0);
            bool isSelected = (index == selectedIndex);
    
            if (index == 0)
            {
                size *= 1.5f;
                Handles.color = isSelected ? Color.white : Color.green;
            }
            else if (isAnchor)
            {
                size *= 1.2f;
                Handles.color = isSelected ? Color.white : Color.red;
            }
            else
            {
                size *= 0.8f;
                Handles.color = isSelected ? Color.white : Color.yellow;
            }
    
            // Draw caps
            if (!disableInteraction)
            {
                if (isAnchor)
                {
                    if (Handles.Button(point, handleRotation, size * 0.12f, size * 0.15f, Handles.SphereHandleCap))
                    {
                        selectedIndex = index;
                        Repaint();
                    }
                }
                else
                {
                    if (Handles.Button(point, handleRotation, size * 0.06f, size * 0.08f, Handles.DotHandleCap))
                    {
                        selectedIndex = index;
                        Repaint();
                    }
                }
            }
            else
            {
                // Just draw the cap without the button logic
                if (isAnchor) Handles.SphereHandleCap(0, point, handleRotation, size * 0.12f, EventType.Repaint);
                else Handles.DotHandleCap(0, point, handleRotation, size * 0.06f, EventType.Repaint);
            }
    
            // Draw Text Label
            GUIStyle style = new GUIStyle();
            style.normal.textColor = isAnchor ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            style.fontStyle = isAnchor ? FontStyle.Bold : FontStyle.Normal;
            style.fontSize = isAnchor ? 11 : 9;
            
            string labelText = isAnchor ? $"A{index / 3}" : $"C{index}";
            Handles.Label(point + Vector3.up * size * 0.25f, labelText, style);
    
            // Position/Scale handle only for selected point
            if (selectedIndex == index)
            {
                EditorGUI.BeginChangeCheck();
                if (Tools.current == Tool.Scale && isAnchor)
                {
                    int anchorIndex = index / 3;
                    Vector3 currentScale = spline.anchorScales[anchorIndex];
                    
                    float t = spline.CurveCount > 0 ? (float)anchorIndex / spline.CurveCount : 0f;
                    Vector3 tangent = handleTransform.TransformDirection(spline.GetDirection(t));
                    if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
                    Vector3 refUp = Vector3.up;
                    if (Mathf.Abs(Vector3.Dot(tangent, refUp)) > 0.99f) refUp = Vector3.forward;
                    Vector3 right = Vector3.Cross(refUp, tangent).normalized;
                    Vector3 up = Vector3.Cross(tangent, right).normalized;
                    
                    // Add the current roll to the UI rotation so it matches the tilt tool
                    float currentRoll = spline.anchorRolls[anchorIndex];
                    Quaternion uiRotation = Quaternion.AngleAxis(currentRoll, tangent) * Quaternion.LookRotation(tangent, up);
                    Vector3 actualUp = uiRotation * Vector3.up;
                    Vector3 actualRight = uiRotation * Vector3.right;
                    
                    Vector3 newScale = currentScale;
                    
                    Handles.color = Handles.xAxisColor;
                    newScale.x = Handles.ScaleSlider(newScale.x, point, actualRight, uiRotation, size * 0.8f, 0f);
                    
                    Handles.color = Handles.yAxisColor;
                    newScale.y = Handles.ScaleSlider(newScale.y, point, actualUp, uiRotation, size * 0.8f, 0f);
                    
                    Handles.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    float centralScale = Handles.ScaleValueHandle(newScale.x, point, uiRotation, size * 0.15f, Handles.CubeHandleCap, 0f);
                    if (Mathf.Abs(centralScale - newScale.x) > 0.001f)
                    {
                        float delta = (newScale.x != 0) ? centralScale / newScale.x : 1f;
                        newScale.x *= delta;
                        newScale.y *= delta;
                    }
                    
                    newScale.z = 1f; // Force Z to 1
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(spline, "Scale Point");
                        spline.anchorScales[anchorIndex] = newScale;
                        EditorUtility.SetDirty(spline); spline.MarkDirty();
                        NotifyConnectedDeformers();
                    }
                }
                else if ((Tools.current == Tool.Rotate || Tools.current == Tool.Transform) && isAnchor)
                {
                    int anchorIndex = index / 3;
                    float currentRoll = spline.anchorRolls[anchorIndex];
                    float t = spline.CurveCount > 0 ? (float)anchorIndex / spline.CurveCount : 0f;
                    Vector3 tangent = handleTransform.TransformDirection(spline.GetDirection(t));
                    if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
    
                    // Calculate consistent 'up' vector for reference
                    Vector3 refUp = Vector3.up;
                    if (Mathf.Abs(Vector3.Dot(tangent, refUp)) > 0.99f) refUp = Vector3.forward;
                    Vector3 right = Vector3.Cross(refUp, tangent).normalized;
                    Vector3 up = Vector3.Cross(tangent, right).normalized;
    
                    // Current visual rotation of the 'up' vector
                    Quaternion visualRot = Quaternion.AngleAxis(currentRoll, tangent);
                    
                    Handles.color = Color.cyan;
                    Handles.DrawLine(point, point + (visualRot * up) * (size * 1.0f));
                    Handles.Label(point + (visualRot * up) * (size * 1.1f), $"Tilt: {currentRoll:F1}°", EditorStyles.boldLabel);
                    
                    EditorGUI.BeginChangeCheck();
                    // Use a slightly larger disc and show it even if not perfectly aligned
                    Quaternion newRot = Handles.Disc(visualRot, point, tangent, size * 1.0f, false, 0f);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(spline, "Change Spline Tilt");
                        float newRoll = Vector3.SignedAngle(up, newRot * up, tangent);
                        spline.anchorRolls[anchorIndex] = newRoll;
                        EditorUtility.SetDirty(spline); spline.MarkDirty();
                        NotifyConnectedDeformers();
                    }
    
                    // If in Transform tool, also show position handle
                    if (Tools.current == Tool.Transform)
                    {
                        EditorGUI.BeginChangeCheck();
                        point = Handles.DoPositionHandle(point, handleRotation);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(spline, "Move Point (Transform Tool)");
                            spline.points[index] = handleTransform.InverseTransformPoint(point);
                            EditorUtility.SetDirty(spline); spline.MarkDirty();
                            NotifyConnectedDeformers();
                        }
                    }
                }
                else
                {
                    point = Handles.DoPositionHandle(point, handleRotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(spline, "Move Point");
                        EditorUtility.SetDirty(spline); spline.MarkDirty();
                        Vector3 newLocalPos = handleTransform.InverseTransformPoint(point);
                        spline.points[index] = newLocalPos;
    
                        // --- Smart Looping Logic ---
                        float snapDist = 1.0f; // Snapping threshold
                        int lastIdx = spline.points.Length - 1;
    
                        if (!spline.loop && (index == lastIdx || index == 0))
                        {
                            float d = Vector3.Distance(spline.points[lastIdx], spline.points[0]);
                            if (d < snapDist)
                            {
                                spline.loop = true;
                                if (index == lastIdx) spline.points[lastIdx] = spline.points[0];
                                else spline.points[0] = spline.points[lastIdx];
                                AlignLoopTangents();
                            }
                        }
                        else if (spline.loop)
                        {
                            if (index == 0 || index == lastIdx)
                            {
                                float d = Vector3.Distance(spline.points[lastIdx], spline.points[0]);
                                if (d > snapDist * 1.5f) // Hysteresis to prevent flickering
                                {
                                    spline.loop = false;
                                }
                                else
                                {
                                    // Keep them snapped if close
                                    if (index == 0) spline.points[lastIdx] = spline.points[0];
                                    else spline.points[0] = spline.points[lastIdx];
                                    AlignLoopTangents();
                                }
                            }
                            else if (index == 1 || index == lastIdx - 1)
                            {
                                // Moving entry/exit tangents: sync them for smoothness
                                AlignLoopTangents(index == 1 ? 1 : lastIdx - 1);
                            }
                        }
    
                        NotifyConnectedDeformers();
                    }
                }
            }
            return point;
        }
    
        private void AlignLoopTangents(int masterControlIndex = -1)
        {
            if (spline.points.Length < 4) return;
            int lastIdx = spline.points.Length - 1;
            
            // C1 continuity: P0-C1 must be aligned with Cn-Pn
            Vector3 anchor = spline.points[0];
            
            if (masterControlIndex == 1) // C1 moved, align Cn-1
            {
                Vector3 dir = (anchor - spline.points[1]).normalized;
                float mag = Vector3.Distance(anchor, spline.points[lastIdx - 1]);
                spline.points[lastIdx - 1] = anchor + dir * mag;
            }
            else if (masterControlIndex == lastIdx - 1) // Cn-1 moved, align C1
            {
                Vector3 dir = (anchor - spline.points[lastIdx - 1]).normalized;
                float mag = Vector3.Distance(anchor, spline.points[1]);
                spline.points[1] = anchor + dir * mag;
            }
            else // Auto align (initial loop)
            {
                Vector3 dir = (spline.points[lastIdx - 1] - anchor).normalized;
                float mag = Vector3.Distance(anchor, spline.points[1]);
                spline.points[1] = anchor - dir * mag;
            }
        }
    
        private void SmoothAllTangents()
        {
            for (int i = 3; i < spline.points.Length - 3; i += 3)
            {
                Vector3 anchor = spline.points[i];
                Vector3 inDir = (anchor - spline.points[i - 1]);
                Vector3 outDir = (spline.points[i + 1] - anchor);
                
                // Average the directions
                Vector3 avgDir = (inDir.normalized + outDir.normalized).normalized;
                float inMag = inDir.magnitude;
                float outMag = outDir.magnitude;
                
                spline.points[i - 1] = anchor - avgDir * inMag;
                spline.points[i + 1] = anchor + avgDir * outMag;
            }
        }

        private Vector3 ProjectToSurface(Vector3 worldPoint)
        {
            // Sample down from 500m above - massive range for large terrains
            Ray ray = new Ray(worldPoint + Vector3.up * 500f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return hit.point + Vector3.up * 0.05f;
            }
            return worldPoint;
        }
    }
}
