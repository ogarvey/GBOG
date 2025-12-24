using System;
using System.Runtime.InteropServices;

namespace GBOG.Audio;

// Thin wrapper: create a buffered PCM output and allow streaming interleaved stereo samples.
// Uses NAudio on Windows.
public sealed class AudioOutput : IAudioSink, IDisposable
{
    private readonly NAudio.Wave.WaveOutEvent _player;
    private readonly NAudio.Wave.BufferedWaveProvider _buffer;
    private byte[] _scratch = Array.Empty<byte>();
    private float _volume = 1.0f;

    public int SampleRate { get; }

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

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
        // Apply volume in software for reliability across audio drivers.
        int bytesLen = interleavedStereoPcm16.Length * sizeof(short);
        if (_scratch.Length < bytesLen)
        {
            _scratch = new byte[bytesLen];
        }

        float vol = _volume;
        if (vol <= 0f)
        {
            Array.Clear(_scratch, 0, bytesLen);
            _buffer.AddSamples(_scratch, 0, bytesLen);
            return;
        }

        if (Math.Abs(vol - 1.0f) < 0.0001f)
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(interleavedStereoPcm16);
            bytes.CopyTo(_scratch);
            _buffer.AddSamples(_scratch, 0, bytesLen);
            return;
        }

        Span<short> dst = MemoryMarshal.Cast<byte, short>(_scratch.AsSpan(0, bytesLen));
        for (int i = 0; i < interleavedStereoPcm16.Length; i++)
        {
            int scaled = (int)MathF.Round(interleavedStereoPcm16[i] * vol);
            if (scaled > short.MaxValue) scaled = short.MaxValue;
            else if (scaled < short.MinValue) scaled = short.MinValue;
            dst[i] = (short)scaled;
        }

        _buffer.AddSamples(_scratch, 0, bytesLen);
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
