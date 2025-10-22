using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public TrajectoryReceiver receiver;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Jugador pas� por un checkpoint!");
            //receiver.OnCheckpointReached();
            Destroy(gameObject); // opcional: desaparece el checkpoint
        }
    }
}