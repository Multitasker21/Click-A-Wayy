# webcam.py
import cv2
import socket
import msgpack
import msgpack_numpy as m 
import struct 
import mediapipe as mp
import util  # mostly using your get_angle() & get_distance()
import numpy as np
import pyautogui
import threading
import time

m.patch()

# === Default CamFeed Size === #
TARGET_WIDTH = 640
TARGET_HEIGHT = 480

# === CONFIG Flag for switching to local preview mode ===
use_tkinter_preview = False  # <<< SET TO TRUE (preview only)

# === Setup CLient Connection === #
def listen_for_commands(sock):
    global TARGET_WIDTH, TARGET_HEIGHT
    try:
        while True:
            # Read 4-byte header
            header = sock.recv(4)
            if not header:
                break
            length = struct.unpack(">I", header)[0]
            data = sock.recv(length)
            if not data:
                break

            message = msgpack.unpackb(data, raw=False)
            if message.get("type") == "resize":
                TARGET_WIDTH = int(message["width"])
                TARGET_HEIGHT = int(message["height"])
                print(f"Resize request: {TARGET_WIDTH}x{TARGET_HEIGHT}", flush=True)
    except Exception as e:
        print("Command listener error:", e, flush=True)

# === Setuping a server ===
if not use_tkinter_preview:
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(('127.0.0.1', 5052))
    server.listen(1) 
    print("Waiting for connection...", flush=True)
    conn, _ = server.accept()
    print("Client connected!", flush=True)
    # launch background thread
    threading.Thread(target=listen_for_commands, args=(conn,), daemon=True).start()

# === Camera Setp ===
cap = cv2.VideoCapture(0)
if not cap.isOpened():
    print("Camera failed to open", flush=True)
    exit()

# === MediaPipe Hand Declarations ===
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(
    static_image_mode=False,
    model_complexity=0,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5,
    max_num_hands=2
)
draw = mp.solutions.drawing_utils

# === Cursor Smoothing ===
cursor_history = []
history_length = 5  # Number of frames to average TF in!!

# === Gesture Detectioner ===
def detect_gesture(landmarks) -> tuple[str, tuple[float, float]]:
    if len(landmarks) < 21:
        return "None", None

    thumb_tip = landmarks[4]
    index_tip = landmarks[8]
    dist = util.get_distance(thumb_tip, index_tip)
    gesture = "None"

    # Angles for our fingery fingers
    angle_index = util.get_angle(landmarks[5], landmarks[6], landmarks[8])
    angle_middle = util.get_angle(landmarks[9], landmarks[10], landmarks[12])
    angle_ring = util.get_angle(landmarks[13], landmarks[14], landmarks[16])
    angle_pinky = util.get_angle(landmarks[17], landmarks[18], landmarks[20])

    bent_threshold = 70
    bent_fingers = sum(angle < bent_threshold for angle in [
        angle_index, angle_middle, angle_ring, angle_pinky
    ])

    # === Prioritize Grab FIRST!!!! ✊ ===
    if bent_fingers >= 3:
        gesture = "Grab"

    # Left Click: index bent, middle straight 🖕
    elif angle_index < 100 and angle_middle > 60 and dist > 0.05:
        gesture = "Left Click"

    # Right Click: middle bent, index straight☝️
    elif angle_middle < 90 and angle_index > 60 and dist > 0.05:
        gesture = "Right Click"

    # Double Click: both index and middle bent✌️ 
    elif angle_index < 75 and angle_middle < 75 and dist > 0.05:
        gesture = "Double Click"

    return gesture, index_tip

# === Gesture Cooldowns & Debounce ===
last_click_time = 0
click_delay = 0.5  #secs

is_grabbing = False
last_grab_state = False
grab_debounce_time = 0.2  #secs
last_grab_change_time = 0

# === Main Loop ===
while True:
    ret, frame = cap.read()
    if not ret:
        continue

    # Flip and convert color space 
    frame = cv2.flip(frame, 1)
    frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(frame_rgb)

    all_hand_landmarks = []  # A List to hold all detected hands 
    landmark_points = []     # Scaled points for each hand 
    gesture = "None"

    # ====== RESIZING before sending to Godot ======
    frame = cv2.resize(frame, (TARGET_WIDTH, TARGET_HEIGHT))

    if results.multi_hand_landmarks:
        for hand_landmarks in results.multi_hand_landmarks:
            if use_tkinter_preview:
                draw.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)
            # Raw normalized landmarks needed for less cpu usage
            hand = [(lm.x, lm.y) for lm in hand_landmarks.landmark]
            all_hand_landmarks.append(hand)

        # Gesture detection only for the first detected hand
        gesture, _ = detect_gesture(all_hand_landmarks[0])

        # Scale all hands to image resolution
        h, w, _ = frame.shape
        for hand in all_hand_landmarks:
            scaled_hand = [(int(x * w), int(y * h)) for (x, y) in hand]
            landmark_points.append(scaled_hand)

        # Cursor control using first hand's wrist (index 0)
        wrist = all_hand_landmarks[0][0]  # (x, y)
        screen_w, screen_h = pyautogui.size()
        x = int(wrist[0] * screen_w)
        y = int(wrist[1] * screen_h)

        cursor_history.append((x, y))
        if len(cursor_history) > history_length:
            cursor_history.pop(0)

        avg_x = int(sum(p[0] for p in cursor_history) / len(cursor_history))
        avg_y = int(sum(p[1] for p in cursor_history) / len(cursor_history))
        pyautogui.moveTo(avg_x, avg_y, duration=0.004)

        # === Gesture Actions ===
        now = time.time()

        # Grabbing 👐
        if gesture == "Grab":
            if not is_grabbing and now - last_grab_change_time > grab_debounce_time:
                pyautogui.mouseDown()
                is_grabbing = True
                last_grab_change_time = now
        else:
            if is_grabbing and now - last_grab_change_time > grab_debounce_time:
                pyautogui.mouseUp()
                is_grabbing = False
                last_grab_change_time = now

        # Clicking/Poking
        if not is_grabbing:
            if gesture == "Left Click" and now - last_click_time > click_delay:
                pyautogui.click()
                last_click_time = now
            elif gesture == "Right Click" and now - last_click_time > click_delay:
                pyautogui.click(button='right')
                last_click_time = now
            elif gesture == "Double Click" and now - last_click_time > click_delay:
                pyautogui.doubleClick()
                last_click_time = now

    # === Mode: Local Preview Only ===
    if use_tkinter_preview:
        cv2.putText(frame, f"Gesture: {gesture}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)
        cv2.imshow("Hand Gesture Preview", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break
        continue

    # Encode frame to JPEG (Lighter and easier)
    _, buffer = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 40])

    # Sends frame + landmarks to Godot
    data = {
        "image": buffer.tobytes(),
        "gesture": gesture,
        "landmarks": landmark_points  
    }

    packed = msgpack.packb(data)
    try:
        conn.sendall(struct.pack('>I', len(packed)) + packed)
    except (BrokenPipeError, ConnectionResetError):
        print("Client disconnected")
        break
