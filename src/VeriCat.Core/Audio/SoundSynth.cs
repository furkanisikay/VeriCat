namespace VeriCat.Core.Audio;

/// <summary>Kedi seslerini kodla sentezler ve 16-bit mono PCM WAV olarak döndürür.</summary>
public static class SoundSynth
{
    public const int Rate = 44100;

    /// <summary>"mi-yav": perde yükselip düşer, formant i → a → u yönünde kayar.</summary>
    public static byte[] Meow(double baseF, double length)
    {
        int n = (int)(Rate * length);
        var phases = new double[14];
        var s = new double[n];
        for (int i = 0; i < n; i++)
        {
            double u = (double)i / n, t = (double)i / Rate;
            double f0 = baseF * (0.78 + 0.5 * Math.Sin(Math.PI * Math.Pow(u, 0.75))) * (1 + 0.012 * Math.Sin(2 * Math.PI * 6 * t));
            double formant = 700 + 950 * Math.Sin(Math.PI * Math.Min(1, u * 1.4)) - 300 * u;
            double env = Math.Min(1, u / 0.07) * Math.Pow(Math.Max(0, 1 - u), 0.8);
            double v = 0;
            for (int k = 1; k <= phases.Length; k++)
            {
                double fk = f0 * k;
                if (fk > 9000) break;
                phases[k - 1] += 2 * Math.PI * fk / Rate;
                v += (Math.Exp(-Math.Pow((fk - formant) / 450, 2)) + 0.3 / k) * Math.Sin(phases[k - 1]);
            }
            s[i] = v * env;
        }
        return Wav(s, 0.32);
    }

    /// <summary>Alçak geçiren gürültü, ~26 Hz titreşim, nefes alıp verme.</summary>
    public static byte[] Purr(Random random)
    {
        const double length = 1.8;
        int n = (int)(Rate * length);
        var s = new double[n];
        double lp = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            lp += (random.NextDouble() * 2 - 1 - lp) * 0.05;
            double am = Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 26 * t), 2);
            double breath = 0.55 + 0.45 * Math.Sin(2 * Math.PI * 0.55 * t);
            double env = Math.Min(1, t / 0.15) * Math.Min(1, (length - t) / 0.2);
            s[i] = lp * am * breath * env;
        }
        return Wav(s, 0.32);
    }

    /// <summary>Tıslama: yüksek geçiren gürültü, hızlı atak, yavaş sönüm.</summary>
    public static byte[] Hiss(Random random, double length = 0.6)
    {
        int n = (int)(Rate * length);
        var s = new double[n];
        double prev = 0, lp = 0;
        for (int i = 0; i < n; i++)
        {
            double u = (double)i / n;
            double white = random.NextDouble() * 2 - 1;
            double hp = white - prev;              // birinci dereceden yüksek geçiren
            prev = white;
            lp += (hp - lp) * 0.55;                // çok tiz kısmı biraz yumuşat
            double env = Math.Min(1, u / 0.04) * Math.Pow(1 - u, 1.6);
            s[i] = lp * env;
        }
        return Wav(s, 0.22);
    }

    /// <summary>Pati darbesi: kısa, boğuk bir "pat".</summary>
    public static byte[] Swat(Random random)
    {
        const double length = 0.09;
        int n = (int)(Rate * length);
        var s = new double[n];
        double phase = 0, lp = 0;
        for (int i = 0; i < n; i++)
        {
            double u = (double)i / n, f = 180 - 110 * u;
            phase += 2 * Math.PI * f / Rate;
            lp += (random.NextDouble() * 2 - 1 - lp) * 0.25;
            s[i] = (Math.Sin(phase) + 0.6 * lp) * Math.Pow(1 - u, 3);
        }
        return Wav(s, 0.35);
    }

    /// <summary>16-bit mono PCM WAV, tepe değer <paramref name="peakLevel"/>'e normalize.</summary>
    public static byte[] Wav(double[] samples, double peakLevel)
    {
        double peak = 0.0001;
        foreach (var v in samples) peak = Math.Max(peak, Math.Abs(v));
        double gain = peakLevel / peak;
        using var ms = new MemoryStream(44 + samples.Length * 2);
        using var w = new BinaryWriter(ms);
        int data = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + data); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(data);
        foreach (var v in samples) w.Write((short)Math.Clamp(v * gain * short.MaxValue, short.MinValue, short.MaxValue));
        w.Flush();
        return ms.ToArray();
    }
}
