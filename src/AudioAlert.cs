using System;
using System.IO;
using System.Media;
using System.Threading;

namespace BatteryAlarmApp
{
    public class AudioAlert
    {
        private Thread _alarmThread;
        private volatile bool _isPlaying;
        private readonly object _lock = new object();

        public bool IsPlaying
        {
            get { return _isPlaying; }
        }

        public void StartAlarm(string soundType, string customPath, bool loop, int intervalSeconds)
        {
            lock (_lock)
            {
                StopAlarm();

                _isPlaying = true;
                _alarmThread = new Thread(new ThreadStart(delegate
                {
                    RunAlarmLoop(soundType, customPath, loop, intervalSeconds);
                }));
                _alarmThread.IsBackground = true;
                _alarmThread.Name = "BatteryAlarmAudioThread";
                _alarmThread.Start();
            }
        }

        public void StopAlarm()
        {
            lock (_lock)
            {
                _isPlaying = false;
                if (_alarmThread != null && _alarmThread.IsAlive)
                {
                    try
                    {
                        _alarmThread.Interrupt();
                    }
                    catch { }
                    _alarmThread = null;
                }
            }
        }

        public void TestSound(string soundType, string customPath)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                PlaySingleSound(soundType, customPath);
            });
        }

        private void RunAlarmLoop(string soundType, string customPath, bool loop, int intervalSeconds)
        {
            try
            {
                do
                {
                    if (!_isPlaying) break;

                    PlaySingleSound(soundType, customPath);

                    if (!loop || !_isPlaying) break;

                    int waitMs = Math.Max(1000, intervalSeconds * 1000);
                    int step = 200;
                    for (int elapsed = 0; elapsed < waitMs && _isPlaying; elapsed += step)
                    {
                        Thread.Sleep(step);
                    }
                } while (_isPlaying);
            }
            catch (ThreadInterruptedException)
            {
                // Normal exit when alarm is stopped
            }
            catch { }
            finally
            {
                _isPlaying = false;
            }
        }

        private void PlaySingleSound(string soundType, string customPath)
        {
            try
            {
                if (string.Equals(soundType, "System", StringComparison.OrdinalIgnoreCase))
                {
                    SystemSounds.Exclamation.Play();
                    return;
                }

                if (string.Equals(soundType, "Custom", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                {
                    try
                    {
                        using (SoundPlayer sp = new SoundPlayer(customPath))
                        {
                            sp.PlaySync();
                            return;
                        }
                    }
                    catch
                    {
                        // Fallback to synthesized alarm if custom file is invalid
                    }
                }

                // Synthesized sounds
                byte[] wav = null;
                if (string.Equals(soundType, "Chime", StringComparison.OrdinalIgnoreCase))
                {
                    wav = GenerateChimeWav();
                }
                else if (string.Equals(soundType, "Siren", StringComparison.OrdinalIgnoreCase))
                {
                    wav = GenerateSirenWav();
                }
                else // Default "Alarm"
                {
                    wav = GenerateAlarmWav();
                }

                if (wav != null)
                {
                    using (MemoryStream ms = new MemoryStream(wav))
                    using (SoundPlayer sp = new SoundPlayer(ms))
                    {
                        sp.PlaySync();
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
                throw;
            }
            catch { }
        }

        // Generates smooth melodic chime (C5 -> E5 -> G5)
        public static byte[] GenerateChimeWav()
        {
            int sampleRate = 44100;
            int totalMs = 1200;
            int totalSamples = (int)(sampleRate * (totalMs / 1000.0));
            short[] audio = new short[totalSamples];

            int[] freqs = new int[] { 523, 659, 784, 1046 }; // C5, E5, G5, C6
            int noteDuration = (int)(sampleRate * 0.25);

            for (int n = 0; n < freqs.Length; n++)
            {
                int start = n * (int)(sampleRate * 0.15);
                int freq = freqs[n];
                for (int i = 0; i < noteDuration && (start + i) < totalSamples; i++)
                {
                    int idx = start + i;
                    double t = (double)i / sampleRate;
                    double decay = Math.Exp(-4.0 * t);
                    double sample = Math.Sin(2 * Math.PI * freq * t) * decay * 22000;
                    // Harmonics for warmth
                    sample += Math.Sin(4 * Math.PI * freq * t) * decay * 5000;
                    
                    int mixed = audio[idx] + (int)sample;
                    if (mixed > 32767) mixed = 32767;
                    if (mixed < -32768) mixed = -32768;
                    audio[idx] = (short)mixed;
                }
            }

            return PackWav(audio, sampleRate);
        }

        // Generates classic digital alarm (beep-beep-beep)
        public static byte[] GenerateAlarmWav()
        {
            int sampleRate = 44100;
            int totalMs = 1100;
            int totalSamples = (int)(sampleRate * (totalMs / 1000.0));
            short[] audio = new short[totalSamples];

            // 3 quick sharp pulses: 900Hz, 900Hz, 1200Hz
            int[] pulseStarts = new int[] { 0, (int)(sampleRate * 0.22), (int)(sampleRate * 0.44), (int)(sampleRate * 0.66) };
            int[] pulseFreqs = new int[] { 950, 950, 950, 1250 };
            int pulseLen = (int)(sampleRate * 0.16);

            for (int p = 0; p < pulseStarts.Length; p++)
            {
                int start = pulseStarts[p];
                int freq = pulseFreqs[p];
                for (int i = 0; i < pulseLen && (start + i) < totalSamples; i++)
                {
                    int idx = start + i;
                    double t = (double)i / sampleRate;
                    // Envelope attack/release
                    double env = 1.0;
                    if (i < 200) env = (double)i / 200.0;
                    else if (i > pulseLen - 300) env = (double)(pulseLen - i) / 300.0;

                    // Square-ish wave for distinct alarm buzzer tone
                    double sine = Math.Sin(2 * Math.PI * freq * t);
                    double harmonic = Math.Sin(6 * Math.PI * freq * t) * 0.3;
                    double sample = (sine + harmonic) * 24000 * env;

                    int mixed = audio[idx] + (int)sample;
                    if (mixed > 32767) mixed = 32767;
                    if (mixed < -32768) mixed = -32768;
                    audio[idx] = (short)mixed;
                }
            }

            return PackWav(audio, sampleRate);
        }

        // Generates urgent alternating siren alert
        public static byte[] GenerateSirenWav()
        {
            int sampleRate = 44100;
            int totalMs = 1400;
            int totalSamples = (int)(sampleRate * (totalMs / 1000.0));
            short[] audio = new short[totalSamples];

            double phase = 0.0;
            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                // Frequency alternates between 700Hz and 1100Hz every 0.2 seconds
                double freq = (Math.Sin(2 * Math.PI * 4.0 * t) > 0) ? 750.0 : 1050.0;
                phase += 2 * Math.PI * freq / sampleRate;
                if (phase > 2 * Math.PI) phase -= 2 * Math.PI;

                double sample = Math.Sin(phase) * 23000;
                audio[i] = (short)sample;
            }

            return PackWav(audio, sampleRate);
        }

        private static byte[] PackWav(short[] samples, int sampleRate)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int dataSize = samples.Length * 2;
                int byteRate = sampleRate * 2;

                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataSize);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16); // Subchunk1Size (16 for PCM)
                bw.Write((short)1); // AudioFormat (1 = PCM)
                bw.Write((short)1); // NumChannels (1 = Mono)
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)2); // BlockAlign
                bw.Write((short)16); // BitsPerSample
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    bw.Write(samples[i]);
                }

                return ms.ToArray();
            }
        }
    }
}
