namespace VeriCat.Core.Audio;

/// <summary>
/// Kedi seslerini kodla sentezler ve 16-bit mono PCM WAV olarak döndürür.
/// Kaynak-süzgeç modeli: ses telleri (gırtlak darbesi + nefes gürültüsü) ağız boşluğunun rezonanslarından (formant)
/// geçer. Doğallık küçük kusurlardan gelir: perde titremesi (jitter), genlik dalgalanması (shimmer), nefes.
/// </summary>
public static class SoundSynth
{
    public const int Rate = 44100;

    /// <summary>
    /// "mi-yav": ağız kapalı bir "m" ile başlar, "i → a → u" diye açılıp kapanır. Perde yükselip düşer, sonunda hafifçe
    /// sarkar; her miyav biraz farklıdır. <paramref name="scared"/>: daha tiz, kısa, hırıltılı ve nefesli ("MRAAV!").
    /// </summary>
    public static byte[] Meow(double baseF, double length, Random? random = null, bool scared = false)
    {
        var rng = random ?? new Random((int)(baseF * 1000 + length * 100));
        int n = (int)(Rate * length);
        var s = new double[n];

        double peakAt = 0.3 + rng.NextDouble() * 0.2, rise = 0.32 + rng.NextDouble() * 0.12, tract = 0.93 + rng.NextDouble() * 0.14;
        double breathiness = scared ? 0.45 : 0.16, rough = scared ? 0.02 : 0.006;
        // Formant yolları: (zaman, F1, F2, F3). Kedinin küçük ağız boşluğu insandan yüksek rezonanslar verir.
        double[][] track = scared
            ? new[] { new[] { 0, 500, 1900, 3400.0 }, new[] { 0.08, 1200, 2300, 3700.0 }, new[] { 0.5, 1350, 2100, 3600.0 }, new[] { 1, 800, 1400, 3100.0 } }
            : new[]
            {
                new[] { 0, 320, 1700, 3200.0 }, new[] { 0.12, 450, 2400, 3500.0 }, new[] { 0.38, 1050, 2200, 3600.0 },
                new[] { 0.6, 1150, 1700, 3400.0 }, new[] { 0.85, 750, 1200, 3100.0 }, new[] { 1, 550, 1000, 3000.0 },
            };

        var f1 = new Resonator(); var f2 = new Resonator(); var f3 = new Resonator(); var nasal = new Resonator();
        var wobble = new SmoothNoise(rng, 6); var shimmer = new SmoothNoise(rng, 18);
        double phase = 0, prevPulse = 0, lp = 0;
        for (int i = 0; i < n; i++)
        {
            double u = (double)i / n;
            // Perde: tepeye çıkıp iner, sonunda sarkar; yavaş rastgele titreme + döngüden döngüye küçük sapma.
            double contour = 1 - rise + rise * Math.Sin(Math.PI * 0.5 * Math.Min(1, u / peakAt)) - (u > peakAt ? 0.45 * rise * Math.Pow((u - peakAt) / (1 - peakAt), 1.6) : 0);
            double f0 = baseF * contour * (1 + 0.018 * wobble.Next()) * (1 + rough * (rng.NextDouble() * 2 - 1));

            // Gırtlak darbesi (Rosenberg): açılma, hızlı kapanma, kapalı bekleme; türevi = dudaktan yayılan ses.
            phase += f0 / Rate;
            if (phase >= 1) phase -= 1;
            double open = scared ? 0.5 : 0.62, close = 0.2;
            double pulse = phase < open ? 0.5 * (1 - Math.Cos(Math.PI * phase / open))
                : phase < open + close ? Math.Cos(Math.PI * 0.5 * (phase - open) / close) : 0;
            double source = (pulse - prevPulse) * Rate / Math.Max(f0, 1) * 0.5;
            prevPulse = pulse;
            double air = (rng.NextDouble() * 2 - 1) * (0.25 + pulse) * breathiness;   // nefes, tel açıkken artar
            double exc = source * (1 + 0.12 * shimmer.Next()) + air;

            var (a1, a2, a3) = Formants(track, u);
            double mouth = Math.Clamp((u - 0.03) / 0.12, 0, 1);                      // "m": ağız kapalı başlar
            double v = f1.Band(exc, a1 * tract, 5) + 0.7 * f2.Band(exc, a2 * tract, 8) + 0.35 * f3.Band(exc, a3 * tract, 10);
            double hum = nasal.Band(exc, 280, 3);
            double y = v * (0.15 + 0.85 * mouth) + hum * 0.6 * (1 - mouth);

            double env = Math.Pow(Math.Sin(Math.PI * 0.5 * Math.Min(1, u / 0.06)), 2)
                         * Math.Pow(Math.Max(0, 1 - Math.Max(0, u - 0.7) / 0.3), 1.4) * (0.75 + 0.25 * contour);
            lp += (y * env - lp) * 0.55;                                             // çok tiz uçları yumuşat
            s[i] = lp;
        }
        return Wav(RemoveDc(s), 0.34);
    }

    /// <summary>
    /// Mırlama: gırtlak kasları saniyede ~25 kez seğirir; her seğirme göğüste yankılanan kısa, boğuk bir darbe.
    /// Nefes verirken güçlü, alırken daha kısık ve biraz daha pes; arada kısa bir duraklama olur.
    /// </summary>
    public static byte[] Purr(Random random)
    {
        const double length = 2.0, exhaleEnd = 1.05, inhaleStart = 1.12;
        int n = (int)(Rate * length);
        var s = new double[n];
        var chest = new Resonator(); var throat = new Resonator(); var nose = new Resonator();
        var drift = new SmoothNoise(random, 3);
        double phase = 1, kick = 0, kickGain = 1, lp = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            bool exhale = t < exhaleEnd;
            double level = exhale ? Ramp(t, 0, exhaleEnd, 0.12) : t >= inhaleStart ? 0.62 * Ramp(t, inhaleStart, length, 0.12) : 0;
            double rate = (exhale ? 26.5 : 23.5) * (1 + 0.04 * drift.Next());

            phase += rate / Rate;
            if (phase >= 1) { phase -= 1; kick = 1; kickGain = 0.75 + random.NextDouble() * 0.5; }   // her darbe biraz farklı
            kick *= Math.Exp(-1 / (Rate * 0.011));

            double noise = random.NextDouble() * 2 - 1;
            double exc = noise * kick * kickGain;
            double y = chest.Band(exc, exhale ? 130 : 115, 1.6) * 1.4 + throat.Band(exc, 380, 2.5) * 0.35 + nose.Band(exc, 1100, 3) * 0.08;
            lp += (y - lp) * 0.12;
            s[i] = lp * level;
        }
        return Wav(RemoveDc(s), 0.34);
    }

    /// <summary>
    /// Tıslama: açık ağız ve dişlerden zorla üflenen hava. Kısa bir "tükürük" patlamasıyla başlar, dişlerin verdiği
    /// 3–9 kHz tepeleri olan geniş bantlı gürültü olarak sürer, nefes titreyerek azalır.
    /// </summary>
    public static byte[] Hiss(Random random, double length = 0.6)
    {
        int n = (int)(Rate * length);
        var s = new double[n];
        var teeth = new Resonator(); var sib = new Resonator(); var air = new Resonator(); var throat = new Resonator();
        var tremor = new SmoothNoise(random, 12); var wander = new SmoothNoise(random, 2);
        double spitAt = 0.012 + random.NextDouble() * 0.01;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate, u = (double)i / n;
            double white = random.NextDouble() * 2 - 1;
            double w = 1 + 0.06 * wander.Next();
            double y = teeth.Band(white, 3600 * w, 1.4) + 0.85 * sib.Band(white, 6300 * w, 1.8)
                       + 0.4 * air.Band(white, 9200, 2.2) + 0.3 * throat.Band(white, 1300 * w, 0.9);

            double spit = 1.4 * Math.Exp(-Math.Max(0, t - spitAt) / 0.016);                 // ağız açılırken patlama
            double body = Math.Min(1, t / 0.07) * Math.Pow(Math.Max(0, 1 - Math.Max(0, u - 0.45) / 0.55), 1.5);
            s[i] = y * (spit + body * (1 + 0.18 * tremor.Next())) + (t < spitAt ? white * 0.4 : 0);
        }
        return Wav(RemoveDc(s), 0.24);
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

    // MARK: Yardımcılar

    /// <summary>Formant yolunda <paramref name="u"/> anındaki (F1, F2, F3), ara değerler yumuşak geçişli.</summary>
    static (double, double, double) Formants(double[][] track, double u)
    {
        int k = 1;
        while (k < track.Length - 1 && track[k][0] < u) k++;
        var a = track[k - 1]; var b = track[k];
        double x = Math.Clamp((u - a[0]) / Math.Max(1e-6, b[0] - a[0]), 0, 1);
        x = x * x * (3 - 2 * x);
        return (a[1] + (b[1] - a[1]) * x, a[2] + (b[2] - a[2]) * x, a[3] + (b[3] - a[3]) * x);
    }

    /// <summary>[from, to] aralığında kenarları <paramref name="fade"/> saniyede yumuşakça açılıp kapanan pencere.</summary>
    static double Ramp(double t, double from, double to, double fade) =>
        Math.Clamp(Math.Min(t - from, to - t) / fade, 0, 1);

    /// <summary>DC kaymasını siler (hoparlörde "tık" olmasın).</summary>
    static double[] RemoveDc(double[] s)
    {
        double prevX = 0, prevY = 0;
        for (int i = 0; i < s.Length; i++)
        {
            double y = s[i] - prevX + 0.995 * prevY;
            prevX = s[i]; prevY = y; s[i] = y;
        }
        return s;
    }

    /// <summary>Zamanla değişebilen iki kutuplu bant geçiren süzgeç (RBJ biquad, tepe kazancı 1).</summary>
    sealed class Resonator
    {
        double x1, x2, y1, y2;

        public double Band(double x, double freq, double q)
        {
            double w0 = 2 * Math.PI * Math.Min(freq, Rate * 0.45) / Rate, alpha = Math.Sin(w0) / (2 * q), a0 = 1 + alpha;
            double y = (alpha * x - alpha * x2 + 2 * Math.Cos(w0) * y1 - (1 - alpha) * y2) / a0;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    /// <summary>Yaklaşık -1…1 arasında, saniyede <c>rate</c> kez yeni hedefe kayan yumuşak rastgele sinyal.</summary>
    sealed class SmoothNoise(Random random, double rate)
    {
        double value, target, step;
        int left;

        public double Next()
        {
            if (left-- <= 0)
            {
                left = (int)(Rate / rate);
                target = random.NextDouble() * 2 - 1;
                step = (target - value) / left;
            }
            value += step;
            return value;
        }
    }
}
