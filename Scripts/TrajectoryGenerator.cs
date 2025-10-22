using UnityEngine;
using System.Collections.Generic;

public class TrajectoryGenerator : MonoBehaviour
{
    public LineRenderer lineRenderer;  // Para dibujar la trayectoria
    public int numPoints = 20;
    public float width = 0.1f;

    private List<Vector3> waypoints = new List<Vector3>();

    public void GenerateTrajectory(string type, float length, float amplitude)
    {
        waypoints.Clear();

        if (type == "linea")
        {
            for (int i = 0; i < numPoints; i++)
                waypoints.Add(new Vector3(i * length / numPoints, 0, 0));
        }
        else if (type == "zigzag")
        {
            for (int i = 0; i < numPoints; i++)
            {
                float x = i * length / numPoints;
                float y = (i % 2 == 0 ? amplitude : -amplitude);
                waypoints.Add(new Vector3(x, y, 0));
            }
        }
        else if (type == "curva")
        {
            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / (numPoints - 1);
                float x = t * length;
                float y = Mathf.Sin(t * Mathf.PI) * amplitude;
                waypoints.Add(new Vector3(x, y, 0));
            }
        }

        DrawTrajectory();
    }

    private void DrawTrajectory()
    {
        lineRenderer.positionCount = waypoints.Count;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.SetPositions(waypoints.ToArray());
    }

    public List<Vector3> GetWaypoints()
    {
        return waypoints;
    }
}