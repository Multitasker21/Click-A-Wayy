using Godot;
using System;
using System.Net.Sockets;
using System.IO;
using MessagePack;
using System.Collections.Generic;

public partial class WebcamReceiver : Node
{
    [Export] public TextureRect DisplayRect;
    [Export] public Label GestureLabel; 
    [Export] public LandmarkRenderer LandmarkDrawer;


    private TcpClient _client;
    private NetworkStream _stream;

    public override void _Ready()
    {
        string pythonRelative = "hand_env/Scripts/python.exe";
        string scriptRelative = "scripts/webcam.py";
        string pythonPath = ProjectSettings.GlobalizePath($"res://{pythonRelative}");
        string scriptPath = ProjectSettings.GlobalizePath($"res://{scriptRelative}");
        var startInfo = new System.Diagnostics.ProcessStartInfo
{
    FileName = pythonPath,
    Arguments = scriptPath,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

try
{
    var process = System.Diagnostics.Process.Start(startInfo);
    GD.Print("✅ Python process started: webcam.py");

    new System.Threading.Thread(() =>
    {
        using var reader = process.StandardOutput;
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            GD.Print("🐍 PYTHON: " + line);
        }
    }).Start();

    new System.Threading.Thread(() =>
    {
        using var errReader = process.StandardError;
        string err;
        while ((err = errReader.ReadLine()) != null)
        {
            GD.PrintErr("❗ PYTHON ERROR: " + err);
        }
    }).Start();
}
catch (Exception e)
{
    GD.PrintErr($"❌ Failed to start Python process: {e.Message}");
}
        ConnectToPython();
    }

    private async void ConnectToPython()
{
    const int maxRetries = 10;
    int retries = 0;

    GD.Print("🔄 Attempting to connect to Python server at 127.0.0.1:5050");

    while (retries < maxRetries)
    {
        try
        {
            _client = new TcpClient("127.0.0.1", 5050);
            _stream = _client.GetStream();
            GD.Print("✅ Connected to Python server.");
            break;
        }
        catch (SocketException ex)
        {
            GD.PrintErr($"❌ Connection failed (attempt {retries + 1}/{maxRetries}): {ex.Message}");
            GD.PrintErr($"📄 StackTrace: {ex.StackTrace}");
            retries++;
            await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"❗ Unexpected error during connection attempt: {ex.Message}");
            GD.PrintErr($"📄 StackTrace: {ex.StackTrace}");
            break;
        }
    }

    if (_client == null || !_client.Connected)
    {
        GD.PrintErr("⛔ Final Result: Failed to connect to Python server after max retries.");
        return;
    }

    byte[] lengthBytes = new byte[4];

    while (true)
    {
            if (_stream.Read(lengthBytes, 0, 4) != 4)
                continue;

            Array.Reverse(lengthBytes);
            int length = BitConverter.ToInt32(lengthBytes, 0);
            byte[] payload = new byte[length];
            int read = 0;

            while (read < length)
                read += _stream.Read(payload, read, length - read);

            var unpacked = MessagePackSerializer.Deserialize<Dictionary<string, object>>(payload);

            // === Image ===
            byte[] imgBytes = (byte[])unpacked["image"];
            var image = new Image();
            image.LoadJpgFromBuffer(imgBytes);
            var texture = ImageTexture.CreateFromImage(image);
            DisplayRect.Texture = texture;

            // === Gesture ===
            string gesture = unpacked["gesture"].ToString();
            if (GestureLabel != null)
                GestureLabel.Text = $"Gesture: {gesture}";

            // === Landmarks ===
            if (unpacked.TryGetValue("landmarks", out object landmarkObj) && landmarkObj is object[] handArray)
            {
                var allHands = new List<List<Vector2>>();
                int imgW = image.GetWidth();
                int imgH = image.GetHeight();

                foreach (var handObj in handArray)
                {
                    if (handObj is object[] rawPoints && rawPoints.Length == 21)
                    {
                        var handLandmarks = new List<Vector2>();

                        foreach (var point in rawPoints)
                        {
                            if (point is object[] coords && coords.Length == 2)
                            {
                                float x = Convert.ToSingle(coords[0]) / imgW;
                                float y = Convert.ToSingle(coords[1]) / imgH;

                                float scaledX = x * DisplayRect.Size.X;
                                float scaledY = y * DisplayRect.Size.Y;
                                handLandmarks.Add(new Vector2(scaledX, scaledY));
                            }
                        }

                        if (handLandmarks.Count == 21)
                            allHands.Add(handLandmarks);
                    }
                }

                LandmarkDrawer?.UpdateMultipleHands(allHands);
            }

            await ToSignal(GetTree(), "process_frame");
        }
        

}

}
