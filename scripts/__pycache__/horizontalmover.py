import ctypes
import random
import time
import datetime

# Windows API Setup
class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", ctypes.c_long),
        ("dy", ctypes.c_long),
        ("mouseData", ctypes.c_ulong),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))
    ]

class KEYBDINPUT(ctypes.Structure):
    _fields_ = [
        ("wVk", ctypes.c_ushort),
        ("wScan", ctypes.c_ushort),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))
    ]

class INPUT_UNION(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT)]

class INPUT(ctypes.Structure):
    _fields_ = [("type", ctypes.c_ulong), ("union", INPUT_UNION)]

SendInput = ctypes.windll.user32.SendInput

# Constants
INPUT_MOUSE = 0
INPUT_KEYBOARD = 1
MOUSEEVENTF_MOVE = 0x0001
KEYEVENTF_KEYUP = 0x0002
VK_UP = 0x26  # Virtual-Key Code for UP arrow

DEBUG = True
MOVE_DISTANCE = 500
DURATION = 3.0
STEP_SIZE = 15
STEP_DELAY = DURATION / (MOVE_DISTANCE / STEP_SIZE)

# Functions
def send_mouse_move(dx, dy):
    mi = MOUSEINPUT(dx, dy, 0, MOUSEEVENTF_MOVE, 0, None)
    input = INPUT(INPUT_MOUSE, INPUT_UNION(mi=mi))
    SendInput(1, ctypes.byref(input), ctypes.sizeof(input))

def press_key(vk_code):
    ki = KEYBDINPUT(vk_code, 0, 0, 0, None)
    input = INPUT(INPUT_KEYBOARD, INPUT_UNION(ki=ki))
    SendInput(1, ctypes.byref(input), ctypes.sizeof(input))

def release_key(vk_code):
    ki = KEYBDINPUT(vk_code, 0, KEYEVENTF_KEYUP, 0, None)
    input = INPUT(INPUT_KEYBOARD, INPUT_UNION(ki=ki)) 
    
    SendInput(1, ctypes.byref(input), ctypes.sizeof(input))

# Main Loop
while True:
    delay = random.choices([15, 20, 5], weights=[0.75, 0.10, 0.05], k=1)[0]
    if DEBUG:
        print(f"[{datetime.datetime.now().strftime('%H:%M:%S')}] Waiting {delay} seconds...")
    time.sleep(delay)

    direction = random.choice([-1, 1])
    if DEBUG:
        print(f"[{datetime.datetime.now().strftime('%H:%M:%S')}] Moving {'RIGHT' if direction == 1 else 'LEFT'} by {MOVE_DISTANCE}px + UP Arrow")

    # --- Start UP arrow press ---
    press_key(VK_UP)

    steps = int(MOVE_DISTANCE / STEP_SIZE)
    for _ in range(steps):
        send_mouse_move(STEP_SIZE * direction, 0)
        time.sleep(STEP_DELAY)

    # Hold UP for 3 sec while moving
    time.sleep(3.0)

    # --- Release UP arrow ---
    release_key(VK_UP)

    if DEBUG:
        print(f"[{datetime.datetime.now().strftime('%H:%M:%S')}] Released UP arrow")
