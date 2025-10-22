using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class TrajectoryReceiver : MonoBehaviour

// Recive los puntos de las trayectorias y genera la linea no suavizada 
{
    public GameObject checkpointPrefab;
    public float scale = 1.0f;

    public int serverPort = 5005;
    public string serverIP =  "127.0.0.1";

    //private TcpListener server;
    private TcpClient client;
    private NetworkStream stream;

    private LineRenderer lineRenderer;
    private bool connected = false;

    // ### Cola para mensajes entrantes desde el hilo de red
    private ConcurrentQueue<string> receivedMessages = new ConcurrentQueue<string>();

    private List<GameObject> checkpoints = new List<GameObject>();


    // Tipos de ancho de la trayectoria
    public enum LineThickness
    {
        VeryThin,
        Thin,
        Normal,
        Wide,
        VeryWide
    }
    public LineThickness lineThickness = LineThickness.Normal;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.yellow;
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.useWorldSpace = true;
        }
        lineRenderer.positionCount = 0;

        ConnectToPython();
    }

    void Update()
    {
        // ### Procesar mensajes en el hilo principal
        while (receivedMessages.TryDequeue(out string json))
        {
            HandleMessageOnMainThread(json);
        }

        // ### Tecla R para pedir nueva trayectoria
        if (Input.GetKeyDown(KeyCode.R) && connected)
        {
            Debug.Log("Solicitando nueva trayectoria...");
            SendMessageToPython("REQUEST_TRAJECTORY");
        }
    }

    void ConnectToPython()
    {
        try
        {
            client = new TcpClient(serverIP, serverPort);
            stream = client.GetStream();
            connected = true;
            Debug.Log($"Conectado a Python en {serverIP}:{serverPort}");

            // Comenzar a leer mensajes asíncronamente
            BeginRead();
        }
        catch (Exception e)
        {
            Debug.LogError("No se pudo conectar con Python: " + e.Message);
        }
    }

    void BeginRead()
    {
        byte[] buffer = new byte[8192];
        stream.BeginRead(buffer, 0, buffer.Length, ar =>
        {
            try
            {
                int bytesRead = stream.EndRead(ar);
                if (bytesRead > 0)
                {
                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    // ### Guardar en la cola para procesar luego
                    receivedMessages.Enqueue(data);
                    BeginRead(); // Sigue leyendo
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error en lectura de socket: " + e.Message);
                connected = false;
            }
        }, null);
    }

    // Ajusta la medida de ancho de la trayectoria 
    float GetLineWidth(LineThickness thickness)
    {
        switch (thickness)
        {
            case LineThickness.VeryThin:
                return 0.125f;
            case LineThickness.Thin:
                return 0.25f;
            case LineThickness.Normal:
                return 0.5f;
            case LineThickness.Wide:
                return 0.75f;
            case LineThickness.VeryWide:
                return 1.0f;
            default:
                return 0.5f;
        }
    }

    float GetLineWidthFromString(string t)
    {
        if (Enum.TryParse(t, true, out LineThickness parsed))
            return GetLineWidth(parsed);
        return GetLineWidth(LineThickness.Normal);
    }

    void HandleMessageOnMainThread(string json)
    {
        try
        {
            TrajectoryData traj = JsonUtility.FromJson<TrajectoryData>(json);
            DrawTrajectory(traj.points);

            float width = GetLineWidthFromString(traj.thickness);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }
        catch (Exception e)
        {
            Debug.LogError("Error al parsear datos: " + e.Message);
        }
    }

    void SendMessageToPython(string msg)
    {
        if (stream != null && stream.CanWrite)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
        }
    }

    void DrawTrajectory(List<Vector3Serializable> points)
    {
        ClearTrajectory();

        lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 pos = new Vector3(points[i].x, points[i].y, points[i].z) * scale;
            lineRenderer.SetPosition(i, pos);

            GameObject ckp = Instantiate(checkpointPrefab, pos, Quaternion.identity);
            checkpoints.Add(ckp);
        }

        Debug.Log($"Trayectoria recibida con {points.Count} puntos.");
    }

    void ClearTrajectory()
    {
        foreach (GameObject ckp in checkpoints)
            Destroy(ckp);
        checkpoints.Clear();
        lineRenderer.positionCount = 0;
    }
}

[Serializable]
public class Vector3Serializable
{
    public float x, y, z;
}

[Serializable]
public class TrajectoryData
{
    public List<Vector3Serializable> points;
    public string thickness;
}
