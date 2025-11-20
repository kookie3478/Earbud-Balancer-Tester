🎧 Earbud Balance Tester (C# | WinForms | .NET)

A simple but powerful tool to test and compare the loudness of left and right earbuds using your laptop/PC microphone.
It helps you detect whether one earbud is quieter than the other using a 1 kHz test tone, multi-run averaging, noise reduction, and a Goertzel frequency detector for highly stable measurements.

This project is built using C#, .NET WinForms, and NAudio.

🚀 Features
🎵 Audio Playback

Plays a clean 1 kHz tone into:
Left channel only
Right channel only
Both channels
Ensures reliable earbud channel separation during tests.


🎙️ Accurate Measurement (Improved Algorithm)

Records audio from your PC microphone.
Performs multi-run averaging (default 5 measurements).
Uses noise floor detection before each run.
Uses the Goertzel algorithm to detect exact 1 kHz amplitude.
Results are far more stable than simple RMS methods.

📊 Analysis

Compares left vs right loudness.
Computes percentage difference.
Tells you if:
Left is louder
Right is louder
Both are balanced (within tolerance)

📈 Waveform Display

Displays waveform of the last recorded audio sample.

🧪 CSV Export

Saves all measurements to CSV: timestamp, left_magnitude, right_magnitude

📥 Installation (for Users)
🔧 Requirements

Windows 10 or later

.NET Desktop Runtime (same version as project target)
Example: .NET 8 Desktop Runtime or .NET 10 Desktop Runtime
A laptop / PC with a microphone
Bluetooth earbuds connected to your PC

💾 Steps to Install

Go to the Releases section of this GitHub repo.
Download the latest .zip or .exe build.
Make sure you have .NET Desktop Runtime installed.
You can check by running:

dotnet --list-runtimes


Extract the ZIP and run EarbudBalanceTester.exe.

🛠️ Building From Source (for Developers)
Requirements

Visual Studio 2022 or newer
.NET SDK (8/9/10 — whichever your project uses)
NAudio NuGet package

Steps

1. Clone the repo:

    git clone https://github.com/yourusername/EarbudBalanceTester.git


2. Open the project in Visual Studio.

Ensure the .csproj targets a version of .NET Desktop Runtime you have installed:

<TargetFramework>net10.0-windows</TargetFramework>


3. Install NAudio:

In VS: Project → Manage NuGet Packages → Search NAudio → Install

Or using CLI:

dotnet add package NAudio --version 2.1.0


4. Build & run:

dotnet build
dotnet run

🎧 How to Use the App
1. Connect Your Earbuds

Pair your earbuds with your PC through Bluetooth.
In Windows Sound Settings → Output Device:
Select Headphones (Stereo) mode (NOT hands-free).

2. Prepare for Testing

The program will ask you to:
Hold your left earbud close (1–2 cm) to the laptop microphone.
Ensure the room is relatively quiet.

3. Run Left/Right Tests

Click:
Record (Left) → program performs multiple runs
Record (Right) → same for right earbud
Each run:
Records ambient noise (200 ms)
Plays tone and records microphone input
Extracts the true tone amplitude at 1 kHz
Averages results

4. Analyze

Click:
Analyze Last
The app compares left & right, then tells you:
Balanced
Left louder
Right louder
…with percentage difference.

5. Export Results

Click:
Export CSV
Useful for debugging or sharing results.

🧠 How It Works (Technical Overview)

This app uses several audio and signal-processing techniques:

✔ Goertzel Algorithm

Extracts the amplitude of exactly 1 kHz, ignoring all other noise.
More stable than FFT or RMS for pure tones.

✔ Multi-Run Averaging

Reduces random fluctuations from:

Mic distance
Ambient noise
Human movement

✔ Noise-Floor Subtraction

Captures 200 ms of silence before each run and subtracts its magnitude.

✔ Waveform Visualization

Renders the last recorded audio using a pixel-mapped waveform.

📌 Limitations

Not suitable for measuring music (tone-based only).
Accuracy depends on your microphone and how consistently you hold the earbuds.
Bluetooth latency does not affect measurement, but room noise can.

🗺️ Roadmap / Future Ideas

Add WASAPI system-audio capture (measure YouTube/Spotify directly).
Add support for loading audio test files (WAV/MP3).
Add visual comparison charts (Recharts or ScottPlot).
Add frequency sweeps for full-band earbud testing.
Add FFT view.
