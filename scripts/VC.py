import cv2
import mediapipe as mp
import numpy as np
import time
import util 
import pyautogui
import random

# Screen size
screen_width, screen_height = pyautogui.size()

# Gesture region (touchpad zone)
X_MIN, X_MAX = 0.05, 0.95
Y_MIN, Y_MAX = 0.05, 0.95

# Smoothing config
prev_x, prev_y = 0, 0
smoothing = 5

# Gesture state
is_grabbing = False

# Mediapipe setup
mpHands = mp.solutions.hands
hands = mpHands.Hands(
    static_image_mode=False,
    model_complexity=0,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5,
    max_num_hands=1
)
draw = mp.solutions.drawing_utils

def move_mouse(index_finger_tip):
    global prev_x, prev_y
    if index_finger_tip is None:
        return

    x = np.interp(index_finger_tip.x, [X_MIN, X_MAX], [0, screen_width])
    y = np.interp(index_finger_tip.y, [Y_MIN, Y_MAX], [0, screen_height])
    x = max(0, min(screen_width, x))
    y = max(0, min(screen_height, y))

    smooth_x = prev_x + (x - prev_x) / smoothing
    smooth_y = prev_y + (y - prev_y) / smoothing

    pyautogui.moveTo(smooth_x, smooth_y)
    prev_x, prev_y = smooth_x, smooth_y

def is_grab_gesture(landmarks):
    dist = util.get_distance(landmarks[4], landmarks[8])
    return dist < 0.05

def is_left_click(landmarks, dist):
    return util.get_angle(landmarks[5], landmarks[6], landmarks[8]) < 75 and \
           util.get_angle(landmarks[9], landmarks[10], landmarks[12]) > 60 and \
           dist > 0.05

def is_right_click(landmarks, dist):
    return util.get_angle(landmarks[9], landmarks[10], landmarks[12]) < 75 and \
           util.get_angle(landmarks[5], landmarks[6], landmarks[8]) > 60 and \
           dist > 0.05

def is_double_click(landmarks, dist):
    return util.get_angle(landmarks[5], landmarks[6], landmarks[8]) < 75 and \
           util.get_angle(landmarks[9], landmarks[10], landmarks[12]) < 75 and \
           dist > 0.05

def is_screenshot(landmarks, dist):
    return util.get_angle(landmarks[5], landmarks[6], landmarks[8]) < 50 and \
           util.get_angle(landmarks[9], landmarks[10], landmarks[12]) < 50 and \
           dist < 0.05

def detect_gestures(frame, landmarks, processed):
    global is_grabbing

    if len(landmarks) < 21:
        return

    index_finger_tip = processed.multi_hand_landmarks[0].landmark[mpHands.HandLandmark.INDEX_FINGER_TIP]
    thumb_index_dist = util.get_distance(landmarks[4], landmarks[8])

    # Always move
    move_mouse(index_finger_tip)

    # Grab/drop
    if is_grab_gesture(landmarks) and not is_grabbing:
        is_grabbing = True
        pyautogui.mouseDown()
        cv2.putText(frame, "GRAB", (50, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0, 255, 255), 2)
    elif not is_grab_gesture(landmarks) and is_grabbing:
        is_grabbing = False
        pyautogui.mouseUp()
        cv2.putText(frame, "DROP", (50, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0, 0, 255), 2)

    # Click gestures (only if not grabbing)
    if not is_grabbing:
        if is_left_click(landmarks, thumb_index_dist):
            pyautogui.click()
            cv2.putText(frame, "Left Click", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        elif is_right_click(landmarks, thumb_index_dist):
            pyautogui.rightClick()
            cv2.putText(frame, "Right Click", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 0, 255), 2)
        elif is_double_click(landmarks, thumb_index_dist):
            pyautogui.doubleClick()
            cv2.putText(frame, "Double Click", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 0, 0), 2)
        elif is_screenshot(landmarks, thumb_index_dist):
            label = random.randint(1, 1000)
            pyautogui.screenshot(f"screenshot_{label}.png")
            cv2.putText(frame, "Screenshot", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 0), 2)

def main():
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("❌ Could not open webcam.")
        return

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        frame = cv2.flip(frame, 1)
        h, w, _ = frame.shape
        cv2.rectangle(frame, (int(X_MIN * w), int(Y_MIN * h)), (int(X_MAX * w), int(Y_MAX * h)), (255, 0, 255), 2)

        frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = hands.process(frame_rgb)

        landmarks_list = []
        if results.multi_hand_landmarks:
            for hand_landmarks in results.multi_hand_landmarks:
                draw.draw_landmarks(frame, hand_landmarks, mpHands.HAND_CONNECTIONS)
                for lm in hand_landmarks.landmark:
                    landmarks_list.append((lm.x, lm.y))

        detect_gestures(frame, landmarks_list, results)

        cv2.imshow("Virtual Mouse", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    main()
