using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Linq;

public class RacingLineGenerator : MonoBehaviour
{
    [Header("Input / Output Splines")]
    public SplineContainer trackSplineContainer;
    public SplineContainer racingLineContainer;

    [Header("Generation Parameters")]
    public float roadWidth = 10f;          // No racing‐line point > roadWidth/2 from center
    public float simplicationTolerance = 1f;

    [Header("Curvature‐Based Offset")]
    public float curvatureThreshold = 0.1f; // The curvature level at which we apply max offset

    [Header("Smoothing / Cleanup")]
    public int smoothingWindow = 5;
    public float overlapThreshold = 0.01f;  // Additional distance filter after smoothing

    [ContextMenu("Generate Racing Line")]
    void GenerateRacingLine()
    {
        if (trackSplineContainer == null || racingLineContainer == null)
        {
            Debug.LogError("Please assign trackSplineContainer and racingLineContainer");
            return;
        }

        // Grab the track spline
        Spline trackSpline = trackSplineContainer.Spline;

        // 1) Sample track at high resolution
        int sampleResolution = 500;
        float step = 1f / sampleResolution;

        var centers = new List<float3>();
        var tangents = new List<float3>();
        var normalsUp = new List<float3>();  // "Up" (banking normal)
        var curvatures = new List<float>();

        for (int i = 0; i <= sampleResolution; i++)
        {
            float t = i * step;
            if (trackSpline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up))
            {
                centers.Add(pos);
                tangents.Add(math.normalize(tangent));
                normalsUp.Add(math.normalize(up));
            }
        }

        int n = centers.Count;

        // 2) Compute simple curvature measure
        for (int i = 0; i < n; i++)
        {
            if (i == 0 || i == n - 1)
            {
                curvatures.Add(0f);
            }
            else
            {
                float3 tA = tangents[i - 1];
                float3 tB = tangents[i + 1];
                float dotVal = math.clamp(math.dot(tA, tB), -1f, 1f);
                float angle = math.acos(dotVal);
                curvatures.Add(angle);
            }
        }

        // 3) Build a racing line where each point is offset inside or stays center
        //    We clamp offset to roadWidth/2 so it never goes out of road bounds
        float halfWidth = math.max(0f, roadWidth * 0.5f);
        var racingPoints = new List<float3>(n);

        for (int i = 0; i < n-1; i++)
        {
            float3 cPos = centers[i];
            float3 cTng = tangents[i];
            float3 cUp = normalsUp[i];

            // If user sets roadWidth=0 => offset=0, we just store center
            if (halfWidth < 1e-6f)
            {
                racingPoints.Add(cPos);
                continue;
            }

            // Right vector in plane
            float3 right = math.normalize(math.cross(cUp, cTng));

            // Determine corner sign using cross(t_i, t_{i+1})
            int nextIdx = math.min(i + 1, n - 1);
            float cornerSign = math.dot(math.cross(cTng, tangents[nextIdx]), cUp);
            cornerSign = math.sign(cornerSign);
            if (cornerSign == 0f) cornerSign = 1f; // default

            // Figure out how big of an offset factor to apply based on how big curvature is.
            // Example: if curvature >= curvatureThreshold => offsetFactor=1
            // if curvature=0 => offsetFactor=0
            float curv = curvatures[i];
            float offsetFactor = math.saturate(curv / curvatureThreshold);
            // offsetFactor in [0..1], so offset= offsetFactor * halfWidth

            float offsetDist = offsetFactor * halfWidth;
            // cornerSign>0 => offset to "left" boundary, cornerSign<0 => "right" boundary
            float3 offsetPos = cPos + (cornerSign * offsetDist) * right;

            racingPoints.Add(offsetPos);
        }

        // 4a) Smooth the final offsets with a small moving‐average
        var smoothed = SmoothList(racingPoints, smoothingWindow);

        // 4b) Remove overlapping or very close consecutive points
        var cleaned = RemoveOverlappingPoints(smoothed, overlapThreshold);

        // 5) Build the racing line spline from those cleaned points
        var newSpline = new Spline();
        foreach (var pt in cleaned)
            newSpline.Add(new BezierKnot((Vector3)pt), TangentMode.AutoSmooth);
        newSpline.Closed = trackSpline.Closed;

        racingLineContainer.Spline = newSpline;

        Debug.Log($"Racing line generated. After smoothing & cleanup, total points: {cleaned.Count}.");
    }

    // Simple moving‐average smoothing
    private List<float3> SmoothList(List<float3> list, int window)
    {
        if (window < 1) return list;
        var output = new List<float3>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            int start = math.max(0, i - window);
            int end = math.min(list.Count - 1, i + window);
            float3 sum = float3.zero;
            int count = 0;
            for (int j = start; j <= end; j++)
            {
                sum += list[j];
                count++;
            }
            output.Add(sum / math.max(1, count));
        }
        return output;
    }

    // Removes points that are too close to the previous one
    // so we don't get duplicates or super close overlapping
    private List<float3> RemoveOverlappingPoints(List<float3> points, float minDist)
    {
        if (points.Count < 2 || minDist <= 0f)
            return points;

        var result = new List<float3>(points.Count);
        result.Add(points[0]); // always keep the first

        for (int i = 1; i < points.Count; i++)
        {
            float3 prev = result[result.Count - 1];
            float3 curr = points[i];
            float dist = math.distance(prev, curr);

            if (dist > minDist)
            {
                // keep it
                result.Add(curr);
            }
            else
            {
                // skip
            }
        }
        return result;
    }

    [ContextMenu("Simplify Spline")]
    public void SimplifySpline()
    {
        var spline = racingLineContainer.Spline;
        if (spline == null || spline.Knots.Count() == 0)
        {
            Debug.LogWarning("No racing line to simplify.");
            return;
        }

        List<float3> points = new List<float3>();
        foreach (var knot in spline.Knots)
            points.Add((float3)knot.Position);

        List<float3> simplified = SplineUtility.ReducePoints(points, simplicationTolerance);

        var newSpline = new Spline();
        foreach (var pt in simplified)
            newSpline.Add(new BezierKnot((Vector3)pt), TangentMode.AutoSmooth);
        newSpline.Closed = spline.Closed;

        racingLineContainer.Spline = newSpline;
        Debug.Log($"Simplified from {points.Count} to {simplified.Count} points.");
    }

    void OnDrawGizmosSelected()
    {
        // Draw the input track in yellow
        if (trackSplineContainer != null && trackSplineContainer.Spline != null)
        {
            float3 trackOffset = trackSplineContainer.transform.position;

            Gizmos.color = Color.yellow;
            foreach (var knot in trackSplineContainer.Spline.Knots)
                Gizmos.DrawSphere(knot.Position + trackOffset, 0.3f);

            for (int i = 0; i < trackSplineContainer.Spline.Knots.Count(); i++)
            {
                float3 a = trackSplineContainer.Spline.Knots.ElementAt(i).Position + trackOffset;
                float3 b = trackSplineContainer.Spline.Knots.ElementAt((i + 1) % trackSplineContainer.Spline.Knots.Count()).Position + trackOffset;
                Gizmos.DrawLine(a, b);
            }
        }

        // Draw the generated racing line in cyan
        if (racingLineContainer != null && racingLineContainer.Spline != null)
        {
            float3 lineOffset = racingLineContainer.transform.position;

            Gizmos.color = Color.cyan;
            foreach (var knot in racingLineContainer.Spline.Knots)
                Gizmos.DrawSphere(knot.Position + lineOffset, 0.3f);

            for (int i = 0; i < racingLineContainer.Spline.Knots.Count(); i++)
            {
                float3 a = racingLineContainer.Spline.Knots.ElementAt(i).Position + lineOffset;
                float3 b = racingLineContainer.Spline.Knots.ElementAt((i + 1) % racingLineContainer.Spline.Knots.Count()).Position + lineOffset;
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
