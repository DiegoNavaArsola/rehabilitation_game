#!/usr/bin/env python3
"""
Telemetry Server for Rehab Game (port 5006)
------------------------------------------
- Listens on 127.0.0.1:5006 for JSON telemetry sent from Unity (PlayerController).
- Accepts flexible payloads like:
    {"type": "telemetry",             # optional
      "pos": {"x": 1.2, "y": 0.0, "z": -3.4}  # or [1.2, 0.0, -3.4]
      "on_path": true,                  # bool (required for metrics)
      "timestamp": 1699999999.123       # optional (seconds). If missing, server timestamps on receipt.}
- Logs all samples to CSV under ./logs/
- Prints rolling metrics to console every 5 seconds per-connection:
    * samples, time_on_path, time_off_path, on_path_ratio, switches

Run:
    python telemetry_server.py

Stop with Ctrl+C. Safe to restart anytime.

Notes:
- If Unity sends newline-delimited JSON, it's optimal. Otherwise, we try to parse a stream of concatenated JSON objects.
- No external deps; pure stdlib.
"""
from __future__ import annotations
import csv
import json
import os
import queue
import socket
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Dict, Optional, Tuple

HOST = "127.0.0.1"
PORT = 5006
LOG_DIR = os.path.join(os.path.dirname(__file__), "logs")
PRINT_INTERVAL = 5.0  # seconds

os.makedirs(LOG_DIR, exist_ok=True)


# ----------------------------- Utilities ------------------------------------

def now() -> float:
    return time.time()


def coerce_position(pos: Any) -> Tuple[float, float, float]:
    """Accept dict {x,y,z} or list/tuple [x,y,z]; else return (nan, nan, nan)."""
    try:
        if isinstance(pos, dict):
            return float(pos.get("x", float("nan"))), float(pos.get("y", float("nan"))), float(pos.get("z", float("nan")))
        if isinstance(pos, (list, tuple)) and len(pos) >= 3:
            return float(pos[0]), float(pos[1]), float(pos[2])
    except Exception:
        pass
    return float("nan"), float("nan"), float("nan")


# ----------------------------- Metrics ---------------------------------------

@dataclass
class Metrics:
    started_at: float = field(default_factory=now)
    last_ts: Optional[float] = None
    last_on: Optional[bool] = None

    samples: int = 0
    time_on_path: float = 0.0
    time_off_path: float = 0.0
    switches: int = 0

    def update(self, ts: float, on_path: Optional[bool]):
        # First sample just initializes the timeline
        if self.last_ts is None:
            self.last_ts = ts
            self.last_on = on_path
            self.samples += 1
            return

        dt = max(0.0, ts - self.last_ts)
        if (self.last_on is True):
            self.time_on_path += dt
        elif (self.last_on is False):
            self.time_off_path += dt

        # Count switch
        if on_path is not None and self.last_on is not None and on_path != self.last_on:
            self.switches += 1

        self.last_ts = ts
        self.last_on = on_path
        self.samples += 1

    def snapshot(self) -> Dict[str, Any]:
        total_time = self.time_on_path + self.time_off_path
        on_ratio = (self.time_on_path / total_time) if total_time > 0 else None
        return {
            "uptime_s": now() - self.started_at,
            "samples": self.samples,
            "time_on_path_s": self.time_on_path,
            "time_off_path_s": self.time_off_path,
            "on_path_ratio": on_ratio,
            "switches": self.switches,
        }


# ----------------------------- Client Handler --------------------------------

class TelemetryClient(threading.Thread):
    def __init__(self, conn: socket.socket, addr: Tuple[str, int], client_id: int):
        super().__init__(daemon=True)
        self.conn = conn
        self.addr = addr
        self.client_id = client_id
        self.metrics = Metrics()
        ts_label = time.strftime("%Y%m%d_%H%M%S")
        self.csv_path = os.path.join(LOG_DIR, f"telemetry_{client_id}_{ts_label}.csv")
        self._stop = threading.Event()
        self._last_print = now()

        # Prepare CSV
        with open(self.csv_path, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["timestamp", "x", "y", "z", "on_path"])  # header

    def stop(self):
        self._stop.set()
        try:
            self.conn.shutdown(socket.SHUT_RDWR)
        except Exception:
            pass
        self.conn.close()

    def run(self):
        buffer = b""
        while not self._stop.is_set():
            try:
                chunk = self.conn.recv(4096)
                if not chunk:
                    break  # client closed
                buffer += chunk

                # Try newline-delimited JSON first
                while b"\n" in buffer:
                    line, buffer = buffer.split(b"\n", 1)
                    self._handle_json_bytes(line)

                # Also try to parse concatenated JSON objects without newline.
                # We scan for balanced top-level braces.
                buffer = self._consume_balanced_objects(buffer)

                # Periodic print
                if now() - self._last_print >= PRINT_INTERVAL:
                    self._last_print = now()
                    snap = self.metrics.snapshot()
                    print(f"[Client {self.client_id}] {self.addr} -> {snap}")

            except (ConnectionResetError, ConnectionAbortedError):
                break
            except Exception as e:
                print(f"[Client {self.client_id}] Error: {e}")
                # Keep going; malformed payloads shouldn't kill the server

        print(f"[Client {self.client_id}] disconnected. Final: {self.metrics.snapshot()}")

    # ------------------- helpers -------------------
    def _handle_json_bytes(self, data: bytes):
        if not data:
            return
        try:
            obj = json.loads(data.decode("utf-8"))
        except Exception:
            # Try to salvage by trimming non-JSON junk
            try:
                txt = data.decode("utf-8", errors="ignore")
                left = txt.find("{")
                right = txt.rfind("}")
                if 0 <= left < right:
                    obj = json.loads(txt[left:right+1])
                else:
                    return
            except Exception:
                return
        self._process_obj(obj)

    def _consume_balanced_objects(self, buffer: bytes) -> bytes:
        depth = 0
        start = None
        i = 0
        while i < len(buffer):
            c = buffer[i:i+1]
            if c == b"{" and depth == 0:
                start = i
                depth = 1
            elif c == b"{" and depth > 0:
                depth += 1
            elif c == b"}" and depth > 0:
                depth -= 1
                if depth == 0 and start is not None:
                    segment = buffer[start:i+1]
                    self._handle_json_bytes(segment)
                    # remove the consumed part
                    buffer = buffer[i+1:]
                    # reset scan
                    depth = 0
                    start = None
                    i = -1  # will become 0 after i+=1
            i += 1
        return buffer

    def _process_obj(self, obj: Dict[str, Any]):
        ts = float(obj.get("timestamp", now()))
        on_path = obj.get("on_path")
        if isinstance(on_path, str):
            on_path = on_path.lower() in ("1", "true", "t", "yes", "y")
        elif not isinstance(on_path, bool):
            on_path = None
        pos = coerce_position(obj.get("pos"))

        # Update metrics
        self.metrics.update(ts, on_path)

        # Append to CSV
        with open(self.csv_path, "a", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow([ts, pos[0], pos[1], pos[2], on_path])


# ----------------------------- Server ----------------------------------------

class TelemetryServer:
    def __init__(self, host: str = HOST, port: int = PORT):
        self.host = host
        self.port = port
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.clients: list[TelemetryClient] = []
        self.next_id = 1

    def start(self):
        self.sock.bind((self.host, self.port))
        self.sock.listen(5)
        print(f"TelemetryServer listening on {self.host}:{self.port} (Ctrl+C to stop)")
        try:
            while True:
                conn, addr = self.sock.accept()
                cid = self.next_id
                self.next_id += 1
                client = TelemetryClient(conn, addr, cid)
                client.start()
                self.clients.append(client)
                print(f"[Client {cid}] connected from {addr}")
        except KeyboardInterrupt:
            print("\nShutting down...")
        finally:
            for c in self.clients:
                c.stop()
            self.sock.close()
            print("TelemetryServer stopped.")


if __name__ == "__main__":
    TelemetryServer().start()
