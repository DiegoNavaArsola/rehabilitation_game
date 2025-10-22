using UnityEngine;
using System.Collections.Generic;

public class TrajectorySmoother : MonoBehaviour

    /* Suaviza las trayectorias generadas para  */
{
    public LineRenderer lineRenderer;
    public int pointsPerSegment = 20;

    public void DrawSmoothTrajectory(List<Vector3> controlPoints)
    {
        if (controlPoints.Count < 4) return;

        List<Vector3> smoothPoints = new List<Vector3>();

        for (int i = 0; i < controlPoints.Count - 3; i++)
        {
            Vector3 p0 = controlPoints[i];
            Vector3 p1 = controlPoints[i + 1];
            Vector3 p2 = controlPoints[i + 2];
            Vector3 p3 = controlPoints[i + 3];

            for (int j = 0; j < pointsPerSegment; j++)
            {
                float t = j / (float)pointsPerSegment;
                Vector3 point = 0.5f * (
                    (2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
                );
                smoothPoints.Add(point);
            }
        }

        lineRenderer.positionCount = smoothPoints.Count;
        lineRenderer.SetPositions(smoothPoints.ToArray());
    }
}

