using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using System.Collections.Generic;

namespace EasyLine
{
[EditorTool("Draw Spline")] 
public class SplineDrawTool : EditorTool
{
    private GUIContent toolIcon;
    private bool isDragging = false;
    private bool isNewPointDrag = false;
    private Vector3 dragStartWorld;
    private int dragAnchorIndex = -1;
    
    // The active spline we are drawing into
    private BezierSpline activeSpline;
    
    private bool isContinuing = false;
    private bool formingFirstSegment = false;
    
    private const float NewSplineDistanceThreshold = 100f; 
    private const float SegmentProximityScreenDistance = 15f;

    public override GUIContent toolbarIcon
    {
        get
        {
            if (toolIcon == null)
            {
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/EasyLine/Icons/SplineToolIcon.png");
                toolIcon = new GUIContent(icon, "EasyLine Draw Tool");
            }
            return toolIcon;
        }
    }

    public override void OnActivated()
    {
        isContinuing = false;
        formingFirstSegment = false;
        base.OnActivated();
    }

    public override void OnToolGUI(EditorWindow window)
    {
        if (!(window is SceneView)) return;
        
        UpdateActiveSpline();

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        Event e = Event.current;

        if (activeSpline == null)
        {
            DrawEmptyStateGUI(e);
            return;
        }

        // --- VISIBILITY: Always draw all handles ---
        DrawSplineCurve(activeSpline);
        DrawAllPointHandles();
        
        if (isDragging && e.type == EventType.MouseDrag && e.button == 0)
        {
            HandlePointDrag(e);
            e.Use();
        }
        
        if (isDragging && e.type == EventType.MouseUp && e.button == 0)
        {
            isDragging = false;
            isNewPointDrag = false;
            
            // If we just finished dragging the LAST anchor, automatically resume drawing from it
            if (activeSpline != null && dragAnchorIndex == activeSpline.points.Length - 1)
            {
                isContinuing = true;
            }
            
            dragAnchorIndex = -1;
            e.Use();
        }

        if (!isDragging)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            // Priority Check for any existing points (Anchor or Tangent)
            bool isNearAnyPoint = CheckAllPointsProximity(out int nearIdx);
            bool isNearA0 = isNearAnyPoint && nearIdx == 0 && !formingFirstSegment && activeSpline.CurveCount >= 2;

            if (isNearA0)
            {
                DrawA0SnapPreview(activeSpline.transform.TransformPoint(activeSpline.points[0]));
                DrawUIBox(activeSpline.name, "CLICK = Close Loop | Esc = Finish", Color.yellow);
            }
            else if (isNearAnyPoint)
            {
                Vector3 ptWorld = activeSpline.transform.TransformPoint(activeSpline.points[nearIdx]);
                bool isAnchor = (nearIdx % 3 == 0);
                DrawPointHighlight(ptWorld, isAnchor);
                DrawUIBox(activeSpline.name, $"LMP = Drag {(isAnchor ? "Anchor" : "Curve Handle")} | Esc = Finish", new Color(0.8f, 0.8f, 1f));
            }
            else if (RaycastGround(mouseRay, out Vector3 mouseWorld, activeSpline.transform.position.y))
            {
                Vector3 lastAnchorWorld = activeSpline.transform.TransformPoint(activeSpline.points[activeSpline.points.Length - 1]);
                float distToLast = Vector3.Distance(mouseWorld, lastAnchorWorld);
                
                bool isNearSegment = CheckSegmentProximity(mouseRay, out int segIdx, out float t, out Vector3 pos);
                bool isFarAway = !isContinuing && (distToLast > NewSplineDistanceThreshold);

                if (isNearSegment)
                {
                    DrawInsertionPreview(pos);
                    DrawUIBox(activeSpline.name, "CLICK = Insert point | Esc = Finish", Color.green);
                }
                else if (isFarAway)
                {
                    DrawNewSplinePreview(mouseWorld);
                    DrawUIBox(activeSpline.name, "CLICK = Start NEW spline | Esc = Exit", new Color(1f, 0.5f, 0f));
                }
                else
                {
                    DrawExtensionPreview(lastAnchorWorld, mouseWorld);
                    string msg = formingFirstSegment ? "CLICK = Place end" : (isContinuing ? "🔗 Continuing" : "CLICK = Add point");
                    DrawUIBox(activeSpline.name, $"{msg} | Esc = Finish", new Color(0f, 1f, 0.5f));
                }
            }

            SceneView.RepaintAll();

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (isNearA0) { CloseLoop(activeSpline); }
                else if (isNearAnyPoint)
                {
                    dragAnchorIndex = nearIdx;
                    isDragging = true;
                    dragStartWorld = activeSpline.transform.TransformPoint(activeSpline.points[nearIdx]);
                    e.Use();
                }
                else if (RaycastGround(mouseRay, out Vector3 hitPoint))
                {
                    if (CheckSegmentProximity(mouseRay, out int sIdx, out float st, out Vector3 sPos)) { InsertPointAt(activeSpline, sIdx, st); }
                    else if (!isContinuing && Vector3.Distance(hitPoint, activeSpline.transform.TransformPoint(activeSpline.points[activeSpline.points.Length - 1])) > NewSplineDistanceThreshold) { CreateNewSplineObject(hitPoint); }
                    else
                    {
                        if (formingFirstSegment) { UpdateSegmentEnd(activeSpline, hitPoint); formingFirstSegment = false; }
                        else { PlacePoint(activeSpline, hitPoint); }
                    }
                    
                    isDragging = true;
                    dragStartWorld = hitPoint;
                    e.Use();
                }
            }
        }

            if (e.type == EventType.Repaint && isDragging && isNewPointDrag)
            {
                // Drawing visual guide line from anchor to mouse during placement
                int prevAnchor = (dragAnchorIndex >= 3) ? dragAnchorIndex - 3 : 0;
                if (prevAnchor >= 0 && prevAnchor < activeSpline.points.Length)
                {
                    Vector3 anchorWorld = activeSpline.transform.TransformPoint(activeSpline.points[prevAnchor]);
                    Handles.color = Color.yellow;
                    Handles.DrawDottedLine(anchorWorld, dragStartWorld, 4f); 
                    float s = HandleUtility.GetHandleSize(dragStartWorld) * 0.15f;
                    Handles.SphereHandleCap(0, dragStartWorld, Quaternion.identity, s, EventType.Repaint);
                }
            }

        // --- Hotkeys (Restored) ---
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                isContinuing = false;
                formingFirstSegment = false;
                ToolManager.RestorePreviousTool();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                if (isContinuing || formingFirstSegment || activeSpline != null)
                {
                    isContinuing = false;
                    formingFirstSegment = false;
                    Selection.activeGameObject = null;
                    activeSpline = null;
                }
                else
                {
                    ToolManager.RestorePreviousTool();
                }
                e.Use();
            }
        }
    }

    private void HandlePointDrag(Event e)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (RaycastGround(ray, out Vector3 dragCurrent, dragStartWorld.y))
        {
            if (dragAnchorIndex >= 0 && dragAnchorIndex < activeSpline.points.Length)
            {
                bool isAnchor = (dragAnchorIndex % 3 == 0);
                
                if (isNewPointDrag)
                {
                    // ABSOLUTE MODE: Tangent is the mirror of the mouse across the anchor
                    int anchorIdx = dragAnchorIndex + 1;
                    Vector3 anchorWorld = activeSpline.transform.TransformPoint(activeSpline.points[anchorIdx]);
                    
                    // Incoming Tangent = Anchor - (Mouse - Anchor)
                    Vector3 mirroredTangentWorld = anchorWorld - (dragCurrent - anchorWorld);
                    activeSpline.points[dragAnchorIndex] = activeSpline.transform.InverseTransformPoint(mirroredTangentWorld);
                    
                    // Since it's symmetric, outgoing tangent (if next segment existed) would be at dragCurrent
                    // Here we only define the incoming tangent for a clean C-curve
                }
                else
                {
                    // DELTA MODE: For existing points
                    Vector3 dragDelta = dragCurrent - dragStartWorld;
                    Vector3 localDelta = activeSpline.transform.InverseTransformVector(dragDelta);
                    
                    if (isAnchor)
                    {
                        activeSpline.points[dragAnchorIndex] += localDelta;
                        
                        // Sync tangents of this anchor
                        if (dragAnchorIndex > 0) activeSpline.points[dragAnchorIndex - 1] += localDelta;
                        if (dragAnchorIndex + 1 < activeSpline.points.Length) activeSpline.points[dragAnchorIndex + 1] += localDelta;

                        // LOOP SYNC: If we move A0, also move A_last (and vice versa)
                        if (activeSpline.loop)
                        {
                            int lastIdx = activeSpline.points.Length - 1;
                            if (dragAnchorIndex == 0)
                            {
                                activeSpline.points[lastIdx] += localDelta;
                                activeSpline.points[lastIdx - 1] += localDelta; // Loop incoming tangent
                            }
                            else if (dragAnchorIndex == lastIdx)
                            {
                                activeSpline.points[0] += localDelta;
                                activeSpline.points[1] += localDelta; // A0 outgoing tangent
                            }
                            
                            // SYNC Scales and Rolls for loops
                            if (activeSpline.anchorScales != null && activeSpline.anchorScales.Length > 1)
                            {
                                int aIdx = dragAnchorIndex / 3;
                                int lAIdx = activeSpline.anchorScales.Length - 1;
                                if (aIdx == 0) activeSpline.anchorScales[lAIdx] = activeSpline.anchorScales[0];
                                else if (aIdx == lAIdx) activeSpline.anchorScales[0] = activeSpline.anchorScales[lAIdx];
                            }
                            if (activeSpline.anchorRolls != null && activeSpline.anchorRolls.Length > 1)
                            {
                                int aIdx = dragAnchorIndex / 3;
                                int lAIdx = activeSpline.anchorRolls.Length - 1;
                                if (aIdx == 0) activeSpline.anchorRolls[lAIdx] = activeSpline.anchorRolls[0];
                                else if (aIdx == lAIdx) activeSpline.anchorRolls[0] = activeSpline.anchorRolls[lAIdx];
                            }
                        }
                    }
                    else
                    {
                        activeSpline.points[dragAnchorIndex] += localDelta;
                        
                        int localAnchorIdx = (dragAnchorIndex % 3 == 2) ? dragAnchorIndex + 1 : dragAnchorIndex - 1;
                        int oppositeIdx = 2 * localAnchorIdx - dragAnchorIndex;
                        
                        // Handle Loop seam mirroring
                        if (activeSpline.loop)
                        {
                            int lastIdx = activeSpline.points.Length - 1;
                            if (dragAnchorIndex == 1) { oppositeIdx = lastIdx - 1; localAnchorIdx = 0; }
                            else if (dragAnchorIndex == lastIdx - 1) { oppositeIdx = 1; localAnchorIdx = 0; }
                        }

                        if (oppositeIdx >= 0 && oppositeIdx < activeSpline.points.Length)
                        {
                            Vector3 anchorLocal = activeSpline.points[localAnchorIdx];
                            Vector3 dir = (anchorLocal - activeSpline.points[dragAnchorIndex]);
                            activeSpline.points[oppositeIdx] = anchorLocal + dir;
                        }
                    }
                }
                
                dragStartWorld = dragCurrent;
                EditorUtility.SetDirty(activeSpline);
                NotifyDeformers(activeSpline);
            }
        }
    }

    private void DrawUIBox(string splineName, string instructions, Color tint)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 350, 60));
        var boxStyle = new GUIStyle(GUI.skin.box) { normal = { textColor = Color.white }, fontSize = 11, padding = new RectOffset(10,10,5,5) };
        GUI.backgroundColor = tint;
        string status = instructions.Contains("NEW") ? "New Spline" : (instructions.Contains("Insert") ? "Insert Point" : "Drawing");
        GUILayout.Box($"🖊 {status}: {splineName}\n{instructions}", boxStyle);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void DrawExtensionPreview(Vector3 lastAnchor, Vector3 mouseWorld)
    {
        Handles.color = new Color(0f, 1f, 0.5f, 0.6f);
        Handles.DrawDottedLine(lastAnchor, mouseWorld, 4f);
        float size = HandleUtility.GetHandleSize(mouseWorld) * 0.15f;
        Handles.SphereHandleCap(0, mouseWorld, Quaternion.identity, size, EventType.Repaint);
    }

    private void DrawNewSplinePreview(Vector3 mouseWorld)
    {
        Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
        float size = HandleUtility.GetHandleSize(mouseWorld) * 0.15f;
        Handles.SphereHandleCap(0, mouseWorld, Quaternion.identity, size, EventType.Repaint);
    }

    private void DrawPointHighlight(Vector3 pos, bool isAnchor)
    {
        Handles.color = new Color(0.8f, 0.8f, 1f, 0.4f);
        float size = HandleUtility.GetHandleSize(pos) * (isAnchor ? 0.25f : 0.15f);
        Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
        Handles.color = new Color(0.8f, 0.8f, 1f, 1f);
        Handles.DrawWireDisc(pos, SceneView.currentDrawingSceneView.camera.transform.forward, size);
    }

    private void DrawInsertionPreview(Vector3 pos)
    {
        Handles.color = Color.green;
        float size = HandleUtility.GetHandleSize(pos) * 0.12f;
        Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
    }

    private void DrawA0SnapPreview(Vector3 pos)
    {
        Handles.color = Color.yellow;
        float size = HandleUtility.GetHandleSize(pos) * 0.2f;
        Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
        Handles.Label(pos + Vector3.up * size * 2f, "🔗 Close Loop");
    }

    private void DrawAllPointHandles()
    {
        if (activeSpline == null || activeSpline.points == null) return;

        for (int i = 0; i < activeSpline.points.Length; i++)
        {
            Vector3 worldPos = activeSpline.transform.TransformPoint(activeSpline.points[i]);
            float size = HandleUtility.GetHandleSize(worldPos);
            bool isAnchor = (i % 3 == 0);

            if (isAnchor)
            {
                Handles.color = (i == 0 || i == activeSpline.points.Length - 1) ? Color.cyan : Color.white;
                float s = size * 0.15f;
                Handles.SphereHandleCap(0, worldPos, Quaternion.identity, s, EventType.Repaint);
            }
            else
            {
                Handles.color = new Color(1f, 0.92f, 0.016f, 0.8f); // Soft yellow for tangents
                float s = size * 0.08f;
                Handles.CubeHandleCap(0, worldPos, Quaternion.identity, s, EventType.Repaint);

                // Draw line to parent anchor
                int anchorIdx = (i % 3 == 1) ? i - 1 : i + 1;
                if (anchorIdx >= 0 && anchorIdx < activeSpline.points.Length)
                {
                    Vector3 anchorPos = activeSpline.transform.TransformPoint(activeSpline.points[anchorIdx]);
                    Handles.color = new Color(1f, 1f, 1f, 0.3f);
                    Handles.DrawLine(worldPos, anchorPos);
                }
            }
        }
    }

    private bool CheckSegmentProximity(Ray mouseRay, out int segIdx, out float t, out Vector3 pos)
    {
        segIdx = -1; t = 0; pos = Vector3.zero;
        if (activeSpline == null) return false;
        float minScreenDist = SegmentProximityScreenDistance;
        for (int i = 0; i < activeSpline.CurveCount; i++)
        {
            for (int j = 0; j <= 20; j++)
            {
                float localT = (float)j / 20;
                int start = i * 3;
                Vector3 p0 = activeSpline.points[start];
                Vector3 p1 = activeSpline.points[start + 1];
                Vector3 p2 = activeSpline.points[start + 2];
                Vector3 p3 = activeSpline.points[start + 3];
                float omt = 1f - localT;
                Vector3 curvePtLocal = omt * omt * omt * p0 + 3f * omt * omt * localT * p1 + 3f * omt * localT * localT * p2 + localT * localT * localT * p3;
                Vector3 curvePtWorld = activeSpline.transform.TransformPoint(curvePtLocal);
                float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(curvePtWorld), Event.current.mousePosition);
                if (d < minScreenDist) { minScreenDist = d; segIdx = i; t = localT; pos = curvePtWorld; }
            }
        }
        return segIdx != -1;
    }

    private bool CheckAllPointsProximity(out int pointIdx)
    {
        pointIdx = -1;
        if (activeSpline == null || activeSpline.points == null) return false;
        
        float bestDist = SegmentProximityScreenDistance * 2.5f;
        for (int i = 0; i < activeSpline.points.Length; i++)
        {
            Vector3 worldPos = activeSpline.transform.TransformPoint(activeSpline.points[i]);
            float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(worldPos), Event.current.mousePosition);
            if (d < bestDist)
            {
                bestDist = d;
                pointIdx = i;
            }
        }
        return pointIdx != -1;
    }

    private bool CheckAnchorProximity(out int anchorIdx)
    {
        // This is now handled by CheckAllPointsProximity and filtering for node % 3 == 0
        anchorIdx = -1; return false;
    }

    private void InsertPointAt(BezierSpline s, int segIdx, float t)
    {
        Undo.RecordObject(s, "Insert Spline Point");
        int start = segIdx * 3;
        Vector3 p0 = s.points[start], p1 = s.points[start + 1], p2 = s.points[start + 2], p3 = s.points[start + 3];
        Vector3 p01 = Vector3.Lerp(p0, p1, t), p12 = Vector3.Lerp(p1, p2, t), p23 = Vector3.Lerp(p2, p3, t);
        Vector3 p012 = Vector3.Lerp(p01, p12, t), p123 = Vector3.Lerp(p12, p23, t), p0123 = Vector3.Lerp(p012, p123, t);
        List<Vector3> pts = new List<Vector3>(s.points);
        pts.RemoveAt(start + 1); pts.RemoveAt(start + 1);
        pts.Insert(start + 1, p01); pts.Insert(start + 2, p012); pts.Insert(start + 3, p0123); pts.Insert(start + 4, p123); pts.Insert(start + 5, p23);
        s.points = pts.ToArray();
        
        if (s.anchorRolls.Length > segIdx + 1) { List<float> r = new List<float>(s.anchorRolls); r.Insert(segIdx + 1, Mathf.Lerp(s.anchorRolls[segIdx], s.anchorRolls[segIdx + 1], t)); s.anchorRolls = r.ToArray(); }
        if (s.anchorScales.Length > segIdx + 1) { List<Vector3> sc = new List<Vector3>(s.anchorScales); sc.Insert(segIdx + 1, Vector3.Lerp(s.anchorScales[segIdx], s.anchorScales[segIdx+1], t)); s.anchorScales = sc.ToArray(); }
        
        dragAnchorIndex = start + 3; isContinuing = true;
        EditorUtility.SetDirty(s); NotifyDeformers(s);
    }

    private void DrawEmptyStateGUI(Event e)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 320, 60));
        GUILayout.Box("🖊 Drawing Tool: No spline selected.\nClick on ground to start a NEW spline object.", GUI.skin.box);
        GUILayout.EndArea();
        Handles.EndGUI();
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (RaycastGround(ray, out Vector3 hit)) { CreateNewSplineObject(hit); e.Use(); }
        }
    }

    private void UpdateActiveSpline()
    {
        if (isContinuing || formingFirstSegment) { if (activeSpline != null) return; isContinuing = false; formingFirstSegment = false; }
        GameObject go = Selection.activeGameObject;
        activeSpline = null;
        if (go != null) 
        { 
            activeSpline = go.GetComponent<BezierSpline>(); 
            if (activeSpline == null) { var d = go.GetComponent<SplineMeshDeformer>(); if (d != null) activeSpline = d.spline; } 
            
            // AUTO-REPAIR: Fix zero-scale or uninitialized arrays on existing splines
            if (activeSpline != null) SanitizeSpline(activeSpline);
        }
    }

    private void SanitizeSpline(BezierSpline s)
    {
        int count = s.CurveCount + 1;
        bool changed = false;

        // 1. Repair Scales
        if (s.anchorScales == null || s.anchorScales.Length != count)
        {
            List<Vector3> sc = new List<Vector3>(s.anchorScales ?? new Vector3[0]);
            while (sc.Count < count) sc.Add(Vector3.one);
            if (sc.Count > count) sc.RemoveRange(count, sc.Count - count);
            s.anchorScales = sc.ToArray();
            changed = true;
        }

        // 2. Fix All-Zero Scales (magnitude check)
        for (int i = 0; i < s.anchorScales.Length; i++)
        {
            if (s.anchorScales[i].sqrMagnitude < 0.0001f)
            {
                s.anchorScales[i] = Vector3.one;
                changed = true;
            }
        }

        // 3. Repair Rolls
        if (s.anchorRolls == null || s.anchorRolls.Length != count)
        {
            float[] nr = new float[count];
            for (int i = 0; i < count; i++) nr[i] = (s.anchorRolls != null && i < s.anchorRolls.Length) ? s.anchorRolls[i] : 0f;
            s.anchorRolls = nr;
            changed = true;
        }

        // 4. Sync Loop Seam (Scale, Roll & Tangent Symmetry)
        if (s.loop && s.anchorScales.Length > 1 && s.points.Length > 3)
        {
            int lastScaleIdx = s.anchorScales.Length - 1;
            if (s.anchorScales[lastScaleIdx] != s.anchorScales[0]) { s.anchorScales[lastScaleIdx] = s.anchorScales[0]; changed = true; }
            if (s.anchorRolls[lastScaleIdx] != s.anchorRolls[0]) { s.anchorRolls[lastScaleIdx] = s.anchorRolls[0]; changed = true; }
            
            // C1 Continuity: Ensure tangent[1] and tangent[last-1] are mirrored around A0/Alast
            int lastP = s.points.Length - 1;
            Vector3 a0 = s.points[0];
            Vector3 tOut = s.points[1] - a0;
            Vector3 tIn = s.points[lastP - 1] - a0;
            
            // If they are not roughly mirrored, we align tIn to be -tOut
            if (Vector3.Angle(tIn.normalized, -tOut.normalized) > 0.1f)
            {
                s.points[lastP - 1] = a0 - tOut;
                changed = true;
            }
        }

        // 5. Robust Orientation Repair: Fix Collapsed Tangents (The "Triangle" Cause)
        for (int i = 0; i < s.CurveCount; i++)
        {
            int startIdx = i * 3;
            Vector3 p0 = s.points[startIdx], p1 = s.points[startIdx+1], p2 = s.points[startIdx+2], p3 = s.points[startIdx+3];
            
            // If the anchor and tangent are identical, the direction is undefined (causes 90-degree flip)
            if (Vector3.Distance(p0, p1) < 0.001f)
            {
                // Nudge tangent towards the next anchor
                Vector3 dir = (p3 - p0).normalized;
                if (dir.sqrMagnitude < 0.1f) dir = Vector3.forward; // fallback if whole segment is zero
                s.points[startIdx + 1] = p0 + dir * 0.01f;
                changed = true;
            }
            if (Vector3.Distance(p2, p3) < 0.001f)
            {
                Vector3 dir = (p0 - p3).normalized;
                if (dir.sqrMagnitude < 0.1f) dir = -Vector3.forward;
                s.points[startIdx + 2] = p3 + dir * 0.01f;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(s);
            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
            NotifyDeformers(s);
        }
    }

    private void CreateNewSplineObject(Vector3 worldPos)
    {
        GameObject go = new GameObject("New Spline Path");
        go.transform.position = worldPos;
        BezierSpline s = go.AddComponent<BezierSpline>();
        SplineMeshDeformer d = go.AddComponent<SplineMeshDeformer>();
        d.spline = s; d.autoDeform = true;
        s.points = new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
        Selection.activeGameObject = go; activeSpline = s; isContinuing = true; formingFirstSegment = true; dragAnchorIndex = 0; 
        Undo.RegisterCreatedObjectUndo(go, "Create Spline");
    }

    private void UpdateSegmentEnd(BezierSpline s, Vector3 worldPos)
    {
        Undo.RecordObject(s, "Define First Segment");
        Vector3 lp = s.transform.InverseTransformPoint(worldPos);
        Vector3 dir = (lp - s.points[0]);
        
        // Project tangents forward/backward by 10%
        s.points[1] = s.points[0] + dir * 0.15f; 
        s.points[2] = lp - dir * 0.15f; 
        s.points[3] = lp;
        
        dragAnchorIndex = 2; 
        isNewPointDrag = true;
        EditorUtility.SetDirty(s); NotifyDeformers(s);
    }

    private void PlacePoint(BezierSpline s, Vector3 worldPos)
    {
        Undo.RecordObject(s, "Place Point");
        Vector3 lp = s.transform.InverseTransformPoint(worldPos);
        Vector3 last = s.points[s.points.Length - 1];
        Vector3 dir = (lp - last);
        if (dir.magnitude > 0.01f)
        {
            Vector3 nd = dir.normalized;
            List<Vector3> pts = new List<Vector3>(s.points);
            
            // 1. Initial outgoing tangent from the PREVIOUS anchor (last)
            Vector3 outTangentA;
            if (s.points.Length >= 4)
            {
                Vector3 inTangentA = s.points[s.points.Length - 2];
                outTangentA = last + (last - inTangentA);
            }
            else
            {
                outTangentA = last + nd * dir.magnitude * 0.2f;
            }
            pts.Add(outTangentA);

            // 2. Add tangents and anchor for the NEW point (lp)
            // BUGFIX: Tangents are now projected forward/backward to remain visible and maintain curve direction
            pts.Add(lp - nd * dir.magnitude * 0.15f); // Incoming tangent (T_new_in)
            pts.Add(lp); // The Anchor itself (A_new)
            
            s.points = pts.ToArray();
            
            List<Vector3> sc = new List<Vector3>(s.anchorScales ?? new Vector3[] {Vector3.one, Vector3.one}); 
            sc.Add(Vector3.one); s.anchorScales = sc.ToArray();
            
            List<float> r = new List<float>(s.anchorRolls ?? new float[] {0, 0}); 
            r.Add(0f); s.anchorRolls = r.ToArray();
            
            dragAnchorIndex = s.points.Length - 2; 
            isContinuing = true;
            isNewPointDrag = true;
        }
        EditorUtility.SetDirty(s); NotifyDeformers(s);
    }

    private void NotifyDeformers(BezierSpline s)
    {
        foreach (var d in Object.FindObjectsOfType<SplineMeshDeformer>()) if (d.spline == s && d.autoDeform) d.Deform();
    }

    private void CloseLoop(BezierSpline s)
    {
        Undo.RecordObject(s, "Close Loop");
        Vector3 a0 = s.points[0];
        PlacePoint(s, s.transform.TransformPoint(a0));
        s.loop = true; s.points[s.points.Length - 1] = a0;
        if (s.points.Length >= 4) { int last = s.points.Length - 1; Vector3 dir = (s.points[last - 1] - a0).normalized; float mag = Vector3.Distance(a0, s.points[1]); s.points[1] = a0 - dir * mag; }
        isContinuing = false; formingFirstSegment = false;
        ToolManager.RestorePreviousTool();
        EditorUtility.SetDirty(s); NotifyDeformers(s);
    }

    private bool RaycastGround(Ray ray, out Vector3 hitPoint, float fallbackHeight = 0f)
    {
        // 1. Try hitting the actual scene geometry (with colliders) - Massive 5km range
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f)) 
        { 
            hitPoint = hit.point; 
            return true; 
        }

        // 2. Fallback to the horizontal mathematical plane at the target height
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, fallbackHeight, 0));
        if (groundPlane.Raycast(ray, out float distance))
        {
            hitPoint = ray.GetPoint(distance);
            return true;
        }

        // 3. Absolute fallback: Project 20 units forward
        hitPoint = ray.GetPoint(20f); 
        return true;
    }

    private void DrawAllPointHandles(BezierSpline s)
    {
        if (s == null || s.points == null) return;
        for (int i = 0; i < s.points.Length; i++)
        {
            bool isAnchor = (i % 3 == 0);
            Vector3 worldPos = s.transform.TransformPoint(s.points[i]);
            float size = HandleUtility.GetHandleSize(worldPos) * (isAnchor ? 0.12f : 0.08f);
            
            // Draw tangent lines to anchors
            if (!isAnchor)
            {
                int anchorIdx = (i % 3 == 1) ? i - 1 : i + 1;
                if (anchorIdx >= 0 && anchorIdx < s.points.Length)
                {
                    Handles.color = new Color(1f, 0.9f, 0.3f, 0.4f);
                    Handles.DrawLine(worldPos, s.transform.TransformPoint(s.points[anchorIdx]));
                }
            }

            Handles.color = isAnchor ? Color.white : Color.yellow;
            if (isAnchor) Handles.SphereHandleCap(0, worldPos, Quaternion.identity, size, EventType.Repaint);
            else Handles.CubeHandleCap(0, worldPos, Quaternion.identity, size, EventType.Repaint);
        }
    }

    private void DrawSplineCurve(BezierSpline spline)
    {
        if (spline == null || spline.points == null || spline.points.Length < 4) return;

        int stepsPerCurve = 30; // Increased resolution for better surface conforming
        int totalSteps = spline.CurveCount * stepsPerCurve;
        
        Vector3 lineStartLocal = spline.GetPoint(0f);
        Vector3 lineStartWorld = ProjectToSurface(spline.transform.TransformPoint(lineStartLocal));
        
        for (int i = 1; i <= totalSteps; i++)
        {
            float t = (float)i / totalSteps;
            Vector3 lineEndLocal = spline.GetPoint(t);
            Vector3 lineEndWorld = ProjectToSurface(spline.transform.TransformPoint(lineEndLocal));
            
            // Draw conformed segment
            float hue = Mathf.Lerp(0.35f, 0.55f, t);
            Handles.color = Color.HSVToRGB(hue, 0.8f, 1f);
            Handles.DrawLine(lineStartWorld, lineEndWorld);
            lineStartWorld = lineEndWorld;
        }
    }

    private Vector3 ProjectToSurface(Vector3 worldPoint)
    {
        // Sample down from 500m above to find the surface - massive range for large terrains
        Ray ray = new Ray(worldPoint + Vector3.up * 500f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // Return hit point with a slight Z-bias
            return hit.point + Vector3.up * 0.05f;
        }
        return worldPoint;
    }
}
}
