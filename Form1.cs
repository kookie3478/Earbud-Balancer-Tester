using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace EarbudBalancerTester
{
    public partial class Form1 : Form
    {
        // --- UI controls ---
        private Button btnPlayLeft;
        private Button btnPlayRight;
        private Button btnPlayBoth;
        private Button btnRecordLeft;
        private Button btnRecordRight;
        private Button btnAnalyze;
        private Button btnSaveCsv;
        private Label lblLeft;
        private Label lblRight;
        private Label lblStatus;
        private PictureBox picWave;
        private NumericUpDown nudRepeats;
        private NumericUpDown nudMeasureMs;

        // --- Audio / signal objects ---
        private IWavePlayer waveOut;
        private readonly int sampleRate = 44100;
        private readonly int defaultMeasureMs = 1200;
        private float[] toneBufferMono;
        private readonly double toneFreq = 1000.0; // test frequency (Hz)

        // Recording
        private WaveInEvent waveIn;
        private List<float> recordedSamples = new List<float>();

        // Results storage (time, leftVal, rightVal)
        private List<(DateTime time, double leftVal, double rightVal)> results =
            new List<(DateTime, double, double)>();

        // ctor
        public Form1()
        {
            InitializeComponent();
            GenerateTone(); // generate default 1kHz tone
        }

        // ------------------- UI Initialization (programmatic) -------------------
        private void InitializeComponent()
        {
            this.Text = "Earbud Balance Tester — Improved (Goertzel + Averaging)";
            this.ClientSize = new Size(920, 520);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Buttons
            btnPlayLeft = new Button { Text = "Play Left", Left = 10, Top = 10, Width = 120 };
            btnPlayRight = new Button { Text = "Play Right", Left = 140, Top = 10, Width = 120 };
            btnPlayBoth = new Button { Text = "Play Both", Left = 270, Top = 10, Width = 120 };

            btnRecordLeft = new Button { Text = "Record (Left)", Left = 10, Top = 50, Width = 160 };
            btnRecordRight = new Button { Text = "Record (Right)", Left = 180, Top = 50, Width = 160 };
            btnAnalyze = new Button { Text = "Analyze Last", Left = 350, Top = 50, Width = 120 };
            btnSaveCsv = new Button { Text = "Export CSV", Left = 480, Top = 50, Width = 120 };

            // Labels
            lblLeft = new Label { Text = "Left: -", Left = 10, Top = 100, Width = 350 };
            lblRight = new Label { Text = "Right: -", Left = 10, Top = 125, Width = 350 };
            lblStatus = new Label { Text = "Status: idle", Left = 10, Top = 155, Width = 700 };

            // NumericUpDowns for repeats and measure duration
            var lblRepeats = new Label { Text = "Repeats:", Left = 620, Top = 12, Width = 60 };
            nudRepeats = new NumericUpDown { Left = 680, Top = 10, Width = 60, Minimum = 1, Maximum = 20, Value = 5 };
            var lblMs = new Label { Text = "Measure ms:", Left = 760, Top = 12, Width = 80 };
            nudMeasureMs = new NumericUpDown { Left = 840, Top = 10, Width = 60, Minimum = 200, Maximum = 5000, Value = defaultMeasureMs };

            // Waveform box
            picWave = new PictureBox
            {
                Left = 10,
                Top = 190,
                Width = 900,
                Height = 320,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Add controls
            Controls.AddRange(new Control[] {
                btnPlayLeft, btnPlayRight, btnPlayBoth,
                btnRecordLeft, btnRecordRight, btnAnalyze, btnSaveCsv,
                lblLeft, lblRight, lblStatus,
                lblRepeats, nudRepeats, lblMs, nudMeasureMs,
                picWave
            });

            // Event handlers
            btnPlayLeft.Click += (s, e) => PlayStereo(true, false);
            btnPlayRight.Click += (s, e) => PlayStereo(false, true);
            btnPlayBoth.Click += (s, e) => PlayStereo(true, true);

            btnRecordLeft.Click += async (s, e) => await BtnRecordLeft_Click();
            btnRecordRight.Click += async (s, e) => await BtnRecordRight_Click();

            btnAnalyze.Click += (s, e) => AnalyzeLastPair();
            btnSaveCsv.Click += (s, e) => ExportCsv();
        }

        // ------------------- Tone generation -------------------
        private void GenerateTone(int durationMs = 1000, double freq = 1000.0)
        {
            int sampleCount = (sampleRate * durationMs) / 1000;
            toneBufferMono = new float[sampleCount];
            double amplitude = 0.7;
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / sampleRate;
                toneBufferMono[i] = (float)(amplitude * Math.Sin(2 * Math.PI * freq * t));
            }
        }

        // ------------------- Playback (stereo) -------------------
        private void PlayStereo(bool left, bool right, float[] sourceMono = null, int sourceSampleRate = 44100)
        {
            StopPlayback();

            // choose source
            float[] source = sourceMono ?? toneBufferMono;
            if (source == null || source.Length == 0) return;

            // if source sample rate differs from app sampleRate, simple resample by nearest (basic)
            if (sourceSampleRate != sampleRate)
            {
                source = ResampleNearest(source, sourceSampleRate, sampleRate);
            }

            // create stereo interleaved buffer (float -> bytes as 32-bit IEEE)
            float[] stereo = new float[source.Length * 2];
            for (int i = 0; i < source.Length; i++)
            {
                stereo[2 * i] = left ? source[i] : 0f;
                stereo[2 * i + 1] = right ? source[i] : 0f;
            }

            var provider = new BufferedWaveProvider(new WaveFormat(sampleRate, 32, 2))
            {
                BufferLength = stereo.Length * 4
            };

            byte[] bytes = new byte[stereo.Length * 4];
            Buffer.BlockCopy(stereo, 0, bytes, 0, bytes.Length);
            provider.AddSamples(bytes, 0, bytes.Length);

            waveOut = new WaveOutEvent();
            waveOut.Init(provider);
            waveOut.Play();

            lblStatus.Text = "Status: playing tone";

            // stop after length of source
            var stopTimer = new System.Windows.Forms.Timer();
            stopTimer.Interval = (int)Math.Ceiling((double)source.Length / sampleRate * 1000) + 100;
            stopTimer.Tick += (s, e) =>
            {
                stopTimer.Stop();
                stopTimer.Dispose();
                StopPlayback();
            };
            stopTimer.Start();
        }

        private void StopPlayback()
        {
            try
            {
                waveOut?.Stop();
                waveOut?.Dispose();
            }
            catch { }
            waveOut = null;
            lblStatus.Text = "Status: idle";
        }

        // simple nearest resample (mono)
        private float[] ResampleNearest(float[] src, int srcRate, int dstRate)
        {
            if (srcRate == dstRate) return src;
            int dstLength = (int)((long)src.Length * dstRate / srcRate);
            float[] dst = new float[dstLength];
            for (int i = 0; i < dstLength; i++)
            {
                int idx = (int)Math.Min(src.Length - 1, Math.Round((double)i * srcRate / dstRate));
                dst[i] = src[idx];
            }
            return dst;
        }

        // ------------------- Recording helpers & event -------------------
        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            // 16-bit PCM input -> convert to float
            int bytesPerSample = 2;
            int sampleCount = e.BytesRecorded / bytesPerSample;
            for (int i = 0; i < sampleCount; i++)
            {
                short s = BitConverter.ToInt16(e.Buffer, i * bytesPerSample);
                recordedSamples.Add(s / 32768f);
            }
        }

        // ------------------- Signal processing helpers -------------------

        // RMS (useful for broadband signals)
        private double ComputeRMS(float[] arr)
        {
            if (arr == null || arr.Length == 0) return 0;
            double sum = 0;
            for (int i = 0; i < arr.Length; i++) sum += arr[i] * arr[i];
            return Math.Sqrt(sum / arr.Length);
        }

        // Goertzel magnitude for target frequency (robust for single-tone)
        private double GoertzelMagnitude(float[] samples, int fs, double targetFreq)
        {
            int n = samples.Length;
            if (n == 0) return 0;
            double k = Math.Round((n * targetFreq) / fs);
            double omega = 2.0 * Math.PI * k / n;
            double coeff = 2.0 * Math.Cos(omega);
            double q0 = 0, q1 = 0, q2 = 0;
            for (int i = 0; i < n; i++)
            {
                q0 = coeff * q1 - q2 + samples[i];
                q2 = q1;
                q1 = q0;
            }
            double real = q1 - q2 * Math.Cos(omega);
            double imag = q2 * Math.Sin(omega);
            return Math.Sqrt(real * real + imag * imag);
        }

        private (double mean, double std) MeanStd(List<double> values)
        {
            if (values == null || values.Count == 0) return (0, 0);
            double mean = values.Average();
            double sumsq = values.Sum(v => (v - mean) * (v - mean));
            double std = Math.Sqrt(sumsq / values.Count);
            return (mean, std);
        }

        // ------------------- Multi-run measurement flow (Goertzel + noise floor) -------------------
        private async Task<double> MeasureToneAmplitudeAsync(bool isLeft, int repeats, int measureMs, double freq)
        {
            // prompt
            var dlg = MessageBox.Show("Place the earbud near the microphone, then press OK.", "Ready?", MessageBoxButtons.OKCancel);
            if (dlg == DialogResult.Cancel) return -1;

            List<double> measures = new List<double>();
            List<double> noiseFloors = new List<double>();

            for (int r = 0; r < repeats; r++)
            {
                // --- capture short ambient noise to estimate noise floor ---
                recordedSamples.Clear();
                waveIn = new WaveInEvent { WaveFormat = new WaveFormat(sampleRate, 16, 1) };
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.StartRecording();
                await Task.Delay(200); // 200 ms ambient capture
                waveIn.StopRecording();
                waveIn.DataAvailable -= WaveIn_DataAvailable;
                waveIn.Dispose();
                var noiseArr = recordedSamples.ToArray();
                double noiseRms = ComputeRMS(noiseArr);
                noiseFloors.Add(noiseRms);

                // --- capture while playing tone ---
                recordedSamples.Clear();
                waveIn = new WaveInEvent { WaveFormat = new WaveFormat(sampleRate, 16, 1) };
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.StartRecording();

                // play the test tone on the requested channel
                PlayStereo(isLeft, !isLeft);

                await Task.Delay(measureMs);

                waveIn.StopRecording();
                waveIn.DataAvailable -= WaveIn_DataAvailable;
                waveIn.Dispose();

                StopPlayback();

                float[] arr = recordedSamples.ToArray();
                if (arr.Length == 0)
                {
                    measures.Add(0);
                    continue;
                }

                // Use Goertzel magnitude around freq for stable detection
                double mag = GoertzelMagnitude(arr, sampleRate, freq);
                measures.Add(mag);

                // small pause between repeats
                await Task.Delay(150);
            }

            // statistics
            var (mMean, mStd) = MeanStd(measures);
            var (nMean, nStd) = MeanStd(noiseFloors);

            // subtract noise (ensure non-negative)
            double norm = Math.Max(0, mMean - nMean);

            return norm;
        }

        // ------------------- Button handlers (wrappers) -------------------
        private async Task BtnRecordLeft_Click()
        {
            try
            {
                int repeats = (int)nudRepeats.Value;
                int ms = (int)nudMeasureMs.Value;
                lblStatus.Text = $"Measuring LEFT: {repeats} runs, {ms} ms each...";
                double leftVal = await MeasureToneAmplitudeAsync(true, repeats, ms, toneFreq);
                if (leftVal < 0) { lblStatus.Text = "Cancelled."; return; }
                lblLeft.Text = $"Left (mag): {leftVal:F3}";
                results.Add((DateTime.Now, leftVal, -1));
                lblStatus.Text = "Left measurement complete.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Recording error: " + ex.Message);
                lblStatus.Text = "Error";
            }
        }

        private async Task BtnRecordRight_Click()
        {
            try
            {
                int repeats = (int)nudRepeats.Value;
                int ms = (int)nudMeasureMs.Value;
                lblStatus.Text = $"Measuring RIGHT: {repeats} runs, {ms} ms each...";
                double rightVal = await MeasureToneAmplitudeAsync(false, repeats, ms, toneFreq);
                if (rightVal < 0) { lblStatus.Text = "Cancelled."; return; }
                lblRight.Text = $"Right (mag): {rightVal:F3}";

                // store / match with last left entry
                int idx = results.FindLastIndex(x => x.rightVal < 0);
                if (idx >= 0)
                {
                    var r = results[idx];
                    results[idx] = (r.time, r.leftVal, rightVal);
                }
                else
                {
                    results.Add((DateTime.Now, -1, rightVal));
                }

                lblStatus.Text = "Right measurement complete.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Recording error: " + ex.Message);
                lblStatus.Text = "Error";
            }
        }

        // ------------------- Analysis -------------------
        private void AnalyzeLastPair()
        {
            if (results.Count == 0)
            {
                MessageBox.Show("No results to analyze.");
                return;
            }

            var last = results.Last();
            if (last.leftVal < 0 || last.rightVal < 0)
            {
                MessageBox.Show("Last entry does not contain both left and right measurements. Record both sides.");
                return;
            }

            double L = last.leftVal;
            double R = last.rightVal;

            if (L == 0 && R == 0)
            {
                MessageBox.Show("No signal detected on either side.");
                return;
            }

            // relative difference
            double diff = Math.Abs(L - R) / Math.Max(L, R);
            string verdict;
            if (diff < 0.03) verdict = $"Balanced (diff {diff * 100.0:F2}%)";
            else if (L > R) verdict = $"Left louder ({diff * 100.0:F1}% difference)";
            else verdict = $"Right louder ({diff * 100.0:F1}% difference)";

            MessageBox.Show($"Left: {L:F4}\nRight: {R:F4}\n\nVerdict: {verdict}");
        }

        // ------------------- Waveform drawing (last recordedSamples) -------------------
        // call DrawWaveform after a recording if you want to visualize last recorded data
        private void DrawWaveform(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                // clear
                picWave.Image?.Dispose();
                picWave.Image = new Bitmap(picWave.Width, picWave.Height);
                return;
            }

            Bitmap bmp = new Bitmap(picWave.Width, picWave.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                Pen pen = Pens.Lime;
                int mid = bmp.Height / 2;
                int len = samples.Length;
                for (int x = 0; x < bmp.Width; x++)
                {
                    int idx = (int)((long)x * len / bmp.Width);
                    float v = samples[idx];
                    int y = mid - (int)(v * (bmp.Height / 2));
                    g.DrawLine(pen, x, mid, x, y);
                }
            }

            picWave.Image?.Dispose();
            picWave.Image = bmp;
        }

        // ------------------- CSV export -------------------
        private void ExportCsv()
        {
            if (results.Count == 0)
            {
                MessageBox.Show("No results to export.");
                return;
            }

            using (SaveFileDialog dlg = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "earbud_results.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                using (var sw = new StreamWriter(dlg.FileName))
                {
                    sw.WriteLine("timestamp,left_mag,right_mag");
                    foreach (var r in results)
                    {
                        sw.WriteLine($"{r.time:O},{r.leftVal},{r.rightVal}");
                    }
                }
            }

            MessageBox.Show("Exported CSV.");
        }

        // ------------------- Cleanup on close -------------------
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopPlayback();
            try
            {
                waveIn?.StopRecording();
                waveIn?.Dispose();
            }
            catch { }
            base.OnFormClosing(e);
        }
    }
}
