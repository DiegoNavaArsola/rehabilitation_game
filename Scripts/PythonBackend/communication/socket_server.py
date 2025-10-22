# communication/socket_server.py

import socket
import json
from threading import Thread

class GameSocketServer:
    def __init__(self, host='127.0.0.1', port=5005):
        self.host = host
        self.port = port
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.bind((self.host, self.port))
        self.server.listen(1)
        print(f"Servidor activo en {self.host}:{self.port}")

    def start(self, callback):
        """Escucha mensajes entrantes y llama a callback(data, conn)."""
        while True:
            conn, addr = self.server.accept()
            print(f"Conexión establecida con {addr}")
            Thread(target=self.handle_client, args=(conn, callback)).start()

    def handle_client(self, conn, callback):
        """Maneja los mensajes entrantes de un cliente."""
        with conn:
            while True:
                data = conn.recv(4096)
                if not data:
                    break
                try:
                    msg = data.decode('utf-8')
                    callback(msg, conn)
                except Exception as e:
                    print(f"Error al procesar mensaje: {e}")

    def send(self, conn, data):
        """Envía un mensaje (dict) a Unity en formato JSON."""
        try:
            msg = json.dumps(data).encode('utf-8')
            conn.sendall(msg)
        except Exception as e:
            print(f"Error al enviar datos: {e}")
