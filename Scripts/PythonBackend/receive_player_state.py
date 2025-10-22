import socket
import json

HOST = '127.0.0.1'
PORT = 6000

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind((HOST, PORT))
    s.listen(1)
    print(f"Esperando conexión en {HOST}:{PORT}...")
    conn, addr = s.accept()
    print(f"Conectado a {addr}")

    with conn:
        while True:
            data = conn.recv(1024)
            if not data:
                break
            try:
                pos = json.loads(data.decode('utf-8'))
                print(f"Posición recibida: {pos}")
            except Exception as e:
                print(f"Error parseando datos: {e}")
