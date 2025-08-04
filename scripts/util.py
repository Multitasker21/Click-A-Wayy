import numpy as np

def get_angle(a, b, c):
    """
    Calculate the angle formed at point 'b' between points a -> b -> c.
    Returns angle in degrees (0° to 180°).
    """
    radians = np.arctan2(c[1] - b[1], c[0] - b[0]) - np.arctan2(a[1] - b[1], a[0] - b[0])
    angle = np.abs(np.degrees(radians))
    if angle > 180:
        angle = 360 - angle
    return angle

def get_distance(a, b):
    """
    Calculate Euclidean distance between two 2D points a and b.
    """
    x1, y1 = a
    x2, y2 = b
    return np.hypot(x2 - x1, y2 - y1)

def get_velocity(prev_point, curr_point, dt):
    """
    Estimate the velocity between two points given time delta.
    dt should be in seconds.
    """
    if dt == 0:
        return 0.0
    distance = get_distance(prev_point, curr_point)
    return distance / dt

def is_finger_extended(base_joint, mid_joint, tip):
    """
    Returns True if finger is likely extended.
    Compares angles between finger joints.
    """
    angle = get_angle(base_joint, mid_joint, tip)
    return angle > 150

def is_thumb_near_index(thumb_tip, index_tip, threshold=0.05):
    """
    Returns True if thumb and index finger are close to each other.
    """
    return get_distance(thumb_tip, index_tip) < threshold
