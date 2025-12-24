using System;
using System.Runtime.InteropServices;

namespace GBOG.Audio;

// Thin wrapper: create a buffered PCM output and allow streaming interleaved stereo samples.
// Uses NAudio on Windows.
public sealed class AudioOutput : IAudioSink, IDisposable
{
    private readonly NAudio.Wave.IWavePlayer _player;
    private readonly NAudio.Wave.BufferedWaveProvider _buffer;
    private byte[] _scratch = Array.Empty<byte>();

    public int SampleRate { get; }

    public AudioOutput(int sampleRate = 44100, int latencyMs = 80)
    {
        SampleRate = sampleRate;

        var format = new NAudio.Wave.WaveFormat(sampleRate, 16, 2);
        _buffer = new NAudio.Wave.BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(Math.Clamp(latencyMs * 6, 200, 2000)),
        };

        _player = new NAudio.Wave.WaveOutEvent
        {
            DesiredLatency = Math.Clamp(latencyMs, 40, 200),
            NumberOfBuffers = 3,
        };

        _player.Init(_buffer);
        _player.Play();
    }

    public void WriteSamples(ReadOnlySpan<short> interleavedStereoPcm16)
    {
        // NAudio takes byte[] + offset/count.
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(interleavedStereoPcm16);
        if (_scratch.Length < bytes.Length)
        {
            _scratch = new byte[bytes.Length];
        }
        bytes.CopyTo(_scratch);
        _buffer.AddSamples(_scratch, 0, bytes.Length);
    }

    public void Dispose()
    {
        try
        {
            _player.Stop();
        }
        catch
        {
            // ignore
        }

        _player.Dispose();
    }
}
