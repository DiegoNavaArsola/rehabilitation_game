import socket
import json
import random
from game.trajectory_generator import generate

HOST = "127.0.0.1"
PORT = 5005

def wait_for_request():

    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.connect((HOST, PORT))
    print("Conectado al servidor Unity.")

    try:
        while True:
            data = s.recv(1024).decode("utf-8")
            if not data:
                break

            if data == "REQUEST_TRAJECTORY":

                """
                **** Implementar un script para generación de trayectorias y envío de datos ***
                """
                traj_type = random.choice(["line", "circle", "infinity", "spiral", "sine"])

                points = generate(traj_type, num_points=20)
                thickness = random.choice(["VeryThin", "Thin", "Normal", "Wide", "VeryWide"])

                print(f"Nueva trayectoria solicitada: {traj_type}. Ancho: {thickness}")

                json_data = json.dumps({
                    "points": [{"x": p[0], "y": p[1], "z": p[2]} for p in points],
                    "thickness": thickness
                })
                s.sendall(json_data.encode("utf-8"))

    except KeyboardInterrupt:
        print("Finalizando conexión...")
    finally:
        s.close()

if __name__ == "__main__":
    wait_for_request()
