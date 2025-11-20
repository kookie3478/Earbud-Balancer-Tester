using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EarbudBalancerTester
{
    public partial class Form1 : Form
    {
        // UI controls
        private Button btnPlayLeft;
        private Button btnPlayRight;
        private Button btnPlayBoth;
        private Button btnRecordLeft;
        private Button btnRecordRight;
        private Button btnAnalyze;
        private Button btnSave;
        private PictureBox picWave;
        private Label lblLeftRms;
        private Label lblRightRms;
        private Label lblStatus;

        // Audio objects
        private IWavePlayer waveOut;
        private readonly int sampleRate = 44100;
        private readonly int durationMs = 1000;
        private float[] toneBufferMono;

        // Recording
        private WaveInEvent waveIn;
        private List<float> recordedSamples = new List<float>();
        private List<(DateTime time, double leftRms, double rightRms)> results =
            new List<(DateTime, double, double)>();

        public Form1()
        {
            InitializeComponent();
            GenerateTone();
        }

        private void InitializeComponent()
        {
            this.Text = "Earbud Balance Tester (Single File Version)";
            this.Width = 900;
            this.Height = 520;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            btnPlayLeft = new Button { Text = "Play Left", Left = 10, Top = 10, Width = 120 };
            btnPlayRight = new Button { Text = "Play Right", Left = 140, Top = 10, Width = 120 };
            btnPlayBoth = new Button { Text = "Play Both", Left = 270, Top = 10, Width = 120 };

            btnRecordLeft = new Button { Text = "Record (Left test)", Left = 10, Top = 50, Width = 160 };
            btnRecordRight = new Button { Text = "Record (Right test)", Left = 180, Top = 50, Width = 160 };
            btnAnalyze = new Button { Text = "Analyze Last", Left = 350, Top = 50, Width = 120 };
            btnSave = new Button { Text = "Export CSV", Left = 480, Top = 50, Width = 120 };

            lblLeftRms = new Label { Text = "Left RMS: -", Left = 10, Top = 100, Width = 300 };
            lblRightRms = new Label { Text = "Right RMS: -", Left = 10, Top = 125, Width = 300 };
            lblStatus = new Label { Text = "Status: idle", Left = 10, Top = 155, Width = 600 };

            picWave = new PictureBox {
                Left = 10, Top = 190, Width = 860, Height = 280,
                BorderStyle = BorderStyle.FixedSingle
            };

            Controls.AddRange(new Control[] {
                btnPlayLeft, btnPlayRight, btnPlayBoth,
                btnRecordLeft, btnRecordRight, btnAnalyze, btnSave,
                lblLeftRms, lblRightRms, lblStatus, picWave
            });

            btnPlayLeft.Click += (s,e) => PlayStereo(true, false);
            btnPlayRight.Click += (s,e) => PlayStereo(false, true);
            btnPlayBoth.Click += (s,e) => PlayStereo(true, true);

            btnRecordLeft.Click += async (s,e) => await RecordCycleAsync(true);
            btnRecordRight.Click += async (s,e) => await RecordCycleAsync(false);
            btnAnalyze.Click += (s,e) => AnalyzeLastPair();
            btnSave.Click += (s,e) => ExportCsv();
        }

        // ---------- TONE GENERATION ----------
        private void GenerateTone()
        {
            int sampleCount = (sampleRate * durationMs) / 1000;
            toneBufferMono = new float[sampleCount];
            double freq = 150.0;

            for (int i = 0; i < sampleCount; i++)
                toneBufferMono[i] = (float)(0.7 * Math.Sin(2 * Math.PI * freq * i / sampleRate));
        }

        // ---------- PLAYBACK ----------
        private void PlayStereo(bool left, bool right)
        {
            StopPlayback();

            float[] stereo = new float[toneBufferMono.Length * 2];
            for (int i = 0; i < toneBufferMono.Length; i++)
            {
                stereo[2 * i] = left ? toneBufferMono[i] : 0f;
                stereo[2 * i + 1] = right ? toneBufferMono[i] : 0f;
            }

            var provider = new BufferedWaveProvider(new WaveFormat(sampleRate, 32, 2));
            byte[] bytes = new byte[stereo.Length * 4];
            Buffer.BlockCopy(stereo, 0, bytes, 0, bytes.Length);
            provider.AddSamples(bytes, 0, bytes.Length);

            waveOut = new WaveOutEvent();
            waveOut.Init(provider);
            waveOut.Play();

            lblStatus.Text = "Status: playing tone";

            var t = new System.Windows.Forms.Timer();
            t.Interval = durationMs + 100;
            t.Tick += (s, e) => { StopPlayback(); t.Stop(); t.Dispose(); };
            t.Start();
        }

        private void StopPlayback()
        {
            if (waveOut != null)
            {
                try { waveOut.Stop(); waveOut.Dispose(); } catch {}
                waveOut = null;
            }
            lblStatus.Text = "Status: idle";
        }

        // ---------- RECORDING ----------
        private async Task RecordCycleAsync(bool isLeft)
        {
            var dlg = MessageBox.Show(
                "Place the earbud near the microphone, then press OK.",
                "Ready?",
                MessageBoxButtons.OKCancel);

            if (dlg == DialogResult.Cancel) return;

            recordedSamples.Clear();

            waveIn = new WaveInEvent() {
                WaveFormat = new WaveFormat(sampleRate, 16, 1)
            };
            waveIn.DataAvailable += WaveIn_DataAvailable;

            waveIn.StartRecording();
            PlayStereo(isLeft, !isLeft);

            await Task.Delay(durationMs + 200);

            waveIn.StopRecording();
            waveIn.Dispose();
            StopPlayback();

            double rms = ComputeRMS(recordedSamples);
            DrawWaveform(recordedSamples.ToArray());

            if (isLeft)
            {
                lblLeftRms.Text = $"Left RMS: {rms:F5}";
                results.Add((DateTime.Now, rms, -1));
            }
            else
            {
                lblRightRms.Text = $"Right RMS: {rms:F5}";

                int idx = results.FindLastIndex(x => x.rightRms < 0);
                if (idx >= 0)
                {
                    var r = results[idx];
                    results[idx] = (r.time, r.leftRms, rms);
                }
                else
                {
                    results.Add((DateTime.Now, -1, rms));
                }
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i);
                recordedSamples.Add(sample / 32768f);
            }
        }

        // ---------- RMS ----------
        private double ComputeRMS(List<float> samples)
        {
            if (samples == null || samples.Count == 0) return 0;
            double sum = 0;
            foreach (var s in samples) sum += s * s;
            return Math.Sqrt(sum / samples.Count);
        }

        // ---------- WAVEFORM DRAWING ----------
        private void DrawWaveform(float[] samples)
        {
            if (samples.Length == 0) return;

            Bitmap bmp = new Bitmap(picWave.Width, picWave.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                Pen pen = Pens.Lime;
                int midY = bmp.Height / 2;

                for (int x = 0; x < bmp.Width; x++)
                {
                    int idx = x * samples.Length / bmp.Width;
                    float v = samples[idx];
                    int y = midY - (int)(v * (bmp.Height / 2));
                    g.DrawLine(pen, x, midY, x, y);
                }
            }

            picWave.Image?.Dispose();
            picWave.Image = bmp;
        }

        // ---------- ANALYSIS ----------
        private void AnalyzeLastPair()
        {
            if (results.Count == 0)
            {
                MessageBox.Show("No recordings yet.");
                return;
            }

            var r = results.Last();
            if (r.leftRms < 0 || r.rightRms < 0)
            {
                MessageBox.Show("Need both Left and Right recordings.");
                return;
            }

            string verdict;
            double diff = Math.Abs(r.leftRms - r.rightRms) / Math.Max(r.leftRms, r.rightRms);

            if (diff < 0.05)
                verdict = "Balanced (difference < 5%)";
            else if (r.leftRms > r.rightRms)
                verdict = "Left earbud louder";
            else
                verdict = "Right earbud louder";

            MessageBox.Show(
                $"Left RMS: {r.leftRms:F5}\nRight RMS: {r.rightRms:F5}\n\nVerdict: {verdict}");
        }

        // ---------- CSV EXPORT ----------
        private void ExportCsv()
        {
            if (results.Count == 0)
            {
                MessageBox.Show("No data to export.");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "earbud_results.csv"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            using (var sw = new StreamWriter(dlg.FileName))
            {
                sw.WriteLine("timestamp,left_rms,right_rms");
                foreach (var r in results)
                    sw.WriteLine($"{r.time:O},{r.leftRms},{r.rightRms}");
            }

            MessageBox.Show("Saved!");
        }
    }
}
