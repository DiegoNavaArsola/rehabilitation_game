
/*
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public string pythonHost = "127.0.0.1";
    public int pythonPort = 6000;
    public float sendInterval = 0.1f;

    private Vector3 movement;
    private TcpClient client;
    private NetworkStream stream;
    private Thread senderThread;
    private bool isConnected = false;
    private bool stopThread = false;

    void Start()
    {
        TryConnectPython();
        StartSenderThread();
    }

    void Update()
    {
        HandleKeyboardMovement();
        // Integración con OpenHaptics OpenHaptics
    }

    void HandleKeyboardMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Movimiento en el plano XZ (no XY)
        movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.World);
    }

    void TryConnectPython()
    {
        try
        {
            Debug.Log("Esperando conexión en " + pythonHost + ":" + pythonPort);
            client = new TcpClient(pythonHost, pythonPort);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("Conectado a Python en " + pythonHost + ":" + pythonPort);
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo conectar a Python: " + e.Message);
            isConnected = false;
        }
    }

    void StartSenderThread()
    {
        senderThread = new Thread(SendPositionLoop);
        senderThread.Start();
    }

    void SendPositionLoop()
    {
        while (!stopThread)
        {
            if (isConnected && stream != null)
            {
                try
                {
                    Vector3 pos = transform.position;
                    string json = $"{{\"x\":{pos.x:F3},\"y\":{pos.y:F3},\"z\":{pos.z:F3}}}";
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    stream.Write(data, 0, data.Length);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Error enviando posición: " + e.Message);
                    isConnected = false;
                }
            }
            Thread.Sleep((int)(sendInterval * 1000));
        }
    }

    void OnApplicationQuit()
    {
        stopThread = true;
        if (senderThread != null && senderThread.IsAlive)
            senderThread.Join();

        if (stream != null) stream.Close();
        if (client != null) client.Close();
    }
}

*/


using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public bool useKeyboard = true;

    [Header("Trajectory Detection")]
    public LayerMask trajectoryMask;
    public float maxDistanceFromPath = 0.5f;

    [Header("Python Connection")]
    public string serverIP = "127.0.0.1";       // Dirección IP del servidor Python
    public int serverPort = 5006;               // Puerto de comunicación
    private TcpClient client;
    private NetworkStream stream;
    private Thread socketThread;
    private bool socketConnected = false;

    [Header("Visual Effects")]
    public Color normalColor = Color.green;
    public Color offPathColor = Color.red;
    private Renderer rend;

    private Vector3 moveInput;
    private bool isOffPath = false;


    /* Gizmos para debug. COMENTAR LINEAS CUANDO SE TENGA LA SOLUCIÖN */
    // En tu componente
    public float maxDist = 3f;
    public LayerMask layerMask;


    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 dir = Vector3.down;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + dir * maxDist);

        if (Application.isPlaying &&
            Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, layerMask))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, hit.point);
            Gizmos.DrawWireSphere(hit.point, 0.05f);
        }
    }
    /* */



    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) rend.material.color = normalColor;

        // Iniciar conexión en hilo separado
        socketThread = new Thread(ConnectToPython);
        socketThread.IsBackground = true;
        socketThread.Start();
    }

    void Update()
    {
        
        HandleMovement();
        CheckIfOnPath();

        if (socketConnected)
        {
            SendPositionToPython(transform.position, !isOffPath);
        }



    }

    // Función para controlar la posición del jugador (Teclado o Robot)
    private void HandleMovement()
    {
        if (useKeyboard)
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            moveInput = new Vector3(moveX, 0f, moveZ);
            transform.position += moveInput * moveSpeed * Time.deltaTime;
        }
        else
        {
            // *** IMPLEMENTAR CONTROL CON PHANTOM ***
        }
    }

    // Función que detecta si el jugador se encuentra dentro la trayectoria (EN el plano XZ)
    private void CheckIfOnPath()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 2f, Vector3.down);
        RaycastHit hit;


        if (Physics.Raycast(ray, out hit, 4f, trajectoryMask))
        {
            float distance = hit.distance;
            if (distance > maxDistanceFromPath)
                SetOffPath(true);
            else
                SetOffPath(false);
        }
        else
        {
            SetOffPath(true);
        }
    }

    // Función que cambia el color de la esfera del jugador si se encuentra fuera de la trayectoria
    private void SetOffPath(bool state)
    {
        if (isOffPath != state)
        {
            isOffPath = state;
            if (rend != null)
                rend.material.color = isOffPath ? offPathColor : normalColor;

            if (isOffPath)
                Debug.Log("Jugador fuera de la trayectoria");
        }
    }

    // Función que destruye los obstáculos generados (Puntos de las trayectorias)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            Debug.Log("Obstáculo destruido: " + other.name);
            Destroy(other.gameObject);
        }
    }

    // *********************
    // CONEXIÓN CON PYTHON
    // *********************

    // Función para conectar a un server de Python
    private void ConnectToPython()
    {
        try
        {
            client = new TcpClient(serverIP, serverPort);
            stream = client.GetStream();
            socketConnected = true;
            Debug.Log("Conectado al servidor Python en " + serverIP + ":" + serverPort);
        }
        catch (SocketException e)
        {
            Debug.LogError("No se pudo conectar al servidor Python: " + e.Message);
        }
    }

    // Función para enviar la posición a Python, jutno con la información sobre si el jugador se encuentra dentro de la trayectoria
    private void SendPositionToPython(Vector3 pos, bool onPath)
    {
        if (stream == null || !stream.CanWrite) return;

        // Enviar JSON con características de la posición del jugador: {"x":..., "y":..., "z":..., "on_path":true/false}
        string jsonData = "{\"x\":" + pos.x.ToString("F3") +
                          ",\"y\":" + pos.y.ToString("F3") +
                          ",\"z\":" + pos.z.ToString("F3") +
                          ",\"on_path\":" + onPath.ToString().ToLower() + "}";

        byte[] data = Encoding.UTF8.GetBytes(jsonData + "\n");

        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Error enviando datos a Python: " + e.Message);
        }
    }

    // Función para termianr la conexión con Python
    private void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
        if (socketThread != null && socketThread.IsAlive) socketThread.Abort();
        socketConnected = false;
    }
}
