namespace GBOG.Audio;

public interface IAudioSink
{
    void WriteSamples(ReadOnlySpan<short> interleavedStereoPcm16);
}
