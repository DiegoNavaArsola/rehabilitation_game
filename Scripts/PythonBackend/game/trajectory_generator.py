import numpy as np


def trajectory_line(num_points=20, length=10):
    start_pos = num_points // 2
    t = np.linspace(-1/2, 1/2, num_points)
    return [[i * length, 0, 0] for i in t]

def trajectory_sine(num_points=50, length=10, amplitude=2):
    t = np.linspace(-1/2, 1/2, num_points)
    return [[i * length, 0, np.sin(i * np.pi * 2) * amplitude] for i in t]

def trajectory_circle(num_points=50, radius=5):
    t = np.linspace(0, 2*np.pi, num_points)
    return [[np.cos(a) * radius, 0, np.sin(a) * radius] for a in t]

def trajectory_infinity(num_points=100, scale=5):
    t = np.linspace(0, 2*np.pi, num_points)
    return [[(scale * np.cos(a)) / (1 + np.sin(a)**2),
             0,
             (scale * np.cos(a) * np.sin(a)) / (1 + np.sin(a)**2)]
            for a in t]

def trajectory_spiral(num_points=100, radius=5, turns=3):
    t = np.linspace(0, 2*np.pi*turns, num_points)
    r = np.linspace(0, radius, num_points)
    return [[r[i] * np.cos(t[i]), 0, r[i] * np.sin(t[i])] for i in range(num_points)]

def generate_trajectory(traj_type="line", **kwargs):
    if traj_type == "line":
        return trajectory_line(**kwargs)
    elif traj_type == "circle":
        return trajectory_circle(**kwargs)
    elif traj_type == "infinity":
        return trajectory_infinity(**kwargs)
    elif traj_type == "spiral":
        return trajectory_spiral(**kwargs)
    elif traj_type == "sine":
        return trajectory_sine(**kwargs)
    else:
        raise ValueError("Trayectoria no soportada: " + traj_type)
