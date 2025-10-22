"""
main.py
---------
Versión modular del backend del juego de rehabilitación.

• Se comunica con Unity mediante sockets TCP (GameSocketServer).
• Genera trayectorias aleatorias para cada solicitud ("REQUEST_TRAJECTORY").
• Deja preparada la estructura para integrar los bandidos contextuales.
"""

import json
import random
from communication.socket_server import GameSocketServer
from game.trajectory_generator import generate_trajectory
# from learning.difficulty_controller import DifficultyController
# import numpy as np

# ============================================================
# Configuración general
# ============================================================

HOST = "127.0.0.1"
PORT = 5005


# ============================================================
# Callback: manejo de mensajes desde Unity
# ============================================================

def handle_message(message, conn, server):
    """
    Procesa los mensajes recibidos desde Unity.
    """
    msg = message.strip()

    if msg == "REQUEST_TRAJECTORY":

        # ------------------------------------------------------------
        # Generación aleatoria de parámetros (versión actual)
        # ------------------------------------------------------------
        traj_type = random.choice(["line", "circle", "infinity", "spiral", "sine"])
        thickness = random.choice(["VeryThin", "Thin", "Normal", "Wide", "VeryWide"])
        num_points = 25

        points = generate_trajectory(traj_type, num_points=num_points)

        # ------------------------------------------------------------
        # Estructura del mensaje JSON a Unity
        # ------------------------------------------------------------
        response = {
            "type": "new_episode",
            "trajectory_type": traj_type,
            "thickness": thickness,
            "points": [{"x": p[0], "y": p[1], "z": p[2]} for p in points]
        }

        server.send(conn, response)
        print(f"Enviada nueva trayectoria: {traj_type} (ancho: {thickness})")

    elif msg.startswith("EPISODE_RESULT"):
        # Aquí podrías procesar resultados de desempeño
        # (por ejemplo, desviación promedio, tiempo usado, etc.)
        print(f"Resultado del episodio recibido: {msg}")

        # ------------------------------------------------------------
        # 🔹 (Futuro) integración con bandidos contextuales
        # ------------------------------------------------------------
        """
        # context = np.array([...])   # construir contexto desde métricas Unity
        # reward = ...
        # bandit.update(context, action, reward)
        """
    else:
        print(f"Mensaje desconocido: {msg}")


# ============================================================
# Función principal
# ============================================================

def main():
    print("Servidor de juego (GameSocketServer) iniciado.")
    print(f"Esperando conexión en {HOST}:{PORT}...")

    # ------------------------------------------------------------
    # Inicializar servidor
    # ------------------------------------------------------------
    server = GameSocketServer(host=HOST, port=PORT)

    # Iniciar escucha con el callback de mensajes
    server.start(callback=lambda msg, conn: handle_message(msg, conn, server))


# ============================================================
# Ejecución principal
# ============================================================

if __name__ == "__main__":
    main()

