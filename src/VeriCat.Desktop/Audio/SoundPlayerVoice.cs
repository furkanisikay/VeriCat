using System.Media;
using VeriCat.Core.Abstractions;
using VeriCat.Core.Audio;
using VeriCat.Core.Configuration;

namespace VeriCat.Desktop.Audio;

/// <summary>Sentezlenen sesleri bellekteki WAV olarak çalar. Aynı anda tek ses çalar; yenisi eskisini keser.</summary>
internal sealed class SoundPlayerVoice : ICatVoice, IDisposable
{
    readonly AppSettings settings;
    readonly Func<double> clock;
    readonly Random random = new();
    SoundPlayer? player;
    MemoryStream? current;
    byte[]? purr, swat;
    byte[][]? scratches;
    byte[][]? hisses;
    double purrUntil, hissUntil, scratchUntil;

    public SoundPlayerVoice(AppSettings settings, Func<double> clock)
    {
        this.settings = settings;
        this.clock = clock;
    }

    public void Meow(double pitch, bool scared = false)
    {
        if (!settings.Sound) return;
        double f0 = pitch * (scared ? 1.35 : 0.92 + random.NextDouble() * 0.16);
        Play(SoundSynth.Meow(f0, scared ? 0.38 : 0.45 + random.NextDouble() * 0.3, random, scared));
        purrUntil = 0;
    }

    public void Purr()
    {
        if (!settings.Sound || clock() < purrUntil) return;
        purrUntil = clock() + 1.75;
        Play(purr ??= SoundSynth.Purr(random));
    }

    public void Hiss()
    {
        if (!settings.Sound || clock() < hissUntil) return;
        hissUntil = clock() + 0.5;
        hisses ??= Enumerable.Range(0, 3).Select(i => SoundSynth.Hiss(random, 0.5 + i * 0.1)).ToArray();
        Play(hisses[random.Next(hisses.Length)]);
        purrUntil = 0;
    }

    public void Swat()
    {
        if (!settings.Sound) return;
        Play(swat ??= SoundSynth.Swat(random));
    }

    public void Scratch()
    {
        if (!settings.Sound || clock() < scratchUntil) return;
        scratchUntil = clock() + 0.4;
        scratches ??= Enumerable.Range(0, 3).Select(_ => SoundSynth.Scratch(random)).ToArray();
        Play(scratches[random.Next(scratches.Length)]);
    }

    void Play(byte[] wav)
    {
        try
        {
            player?.Stop();
            player?.Dispose();
            current?.Dispose();
            current = new MemoryStream(wav);
            player = new SoundPlayer(current);
            player.Play();
        }
        catch (InvalidOperationException) { }   // ses aygıtı yoksa sessiz devam
    }

    public void Dispose()
    {
        player?.Dispose();
        current?.Dispose();
    }
}
