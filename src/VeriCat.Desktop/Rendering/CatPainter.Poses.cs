namespace VeriCat.Desktop.Rendering;

// Pozlar: hepsi sağa bakar şekilde çizilir, sola bakış için Draw() aynalar.
internal sealed partial class CatPainter
{
    void Walk()
    {
        float ph = f.Phase, cr = f.Crouch;
        float bob = cr > 0 ? -5 * cr : MathF.Abs(MathF.Sin(ph)) * 1.8f;
        float sw = MathF.Sin(ph) * 5 * (1 - cr);
        float up1 = Math.Max(0, MathF.Cos(ph)) * 3 * (1 - cr), up2 = Math.Max(0, -MathF.Cos(ph)) * 3 * (1 - cr);
        float wig = f.Wiggle * 2.5f;                   // pusuda kıç sallama
        float sway = MathF.Sin(f.Clock * 2.2f) * 5 + wig * 2, foot = Ground + 5;

        Stroke(Bezier(P(40 + wig, 34 + bob), P(18 + wig, 36 + bob), P(14 + sway * 0.5f, 54), P(22 + sway, 68 + bob * 0.5f)), 11, c.Fur, true);
        Leg(P(50 + wig, 24 + bob), P(50 - sw + wig, foot + up1), 11, c.Shade);
        Leg(P(84, 24 + bob), P(84 + sw, foot + up2), 11, c.Shade);
        Leg(P(42 + wig, 24 + bob), P(42 + sw + wig, foot + up2), 11.5f, c.Fur);
        Leg(P(76, 24 + bob), P(76 - sw, foot + up1), 11.5f, c.Fur);
        Torso(P(62 + wig * 0.3f, 30 + bob));
        Head(P(98, 58 + bob * 1.3f), 4);
    }

    void Air()
    {
        Rotated(P(66, 40), f.Tilt, () =>
        {
            Stroke(Bezier(P(40, 34), P(26, 38), P(14, 44), P(8, 46)), 11, c.Fur, true);
            Leg(P(50, 24), P(38, 12), 11, c.Shade);
            Leg(P(84, 24), P(98, 16), 11, c.Shade);
            Leg(P(42, 24), P(28, 14), 11.5f, c.Fur);
            Leg(P(76, 24), P(92, 12), 11.5f, c.Fur);
            Torso(P(62, 30));
            Head(P(98, 58), 4);
        });
    }

    /// <summary>İmlece atlayış: arka ayaklar geride, ön patiler ileri uzanmış.</summary>
    void Pounce()
    {
        Rotated(P(66, 40), f.Tilt, () =>
        {
            Stroke(Bezier(P(40, 34), P(26, 36), P(14, 38), P(4, 42)), 11, c.Fur, true);
            Leg(P(50, 24), P(30, 14), 11, c.Shade);
            Leg(P(42, 24), P(22, 20), 11.5f, c.Fur);
            Torso(P(62, 30));
            Head(P(94, 56), 5);
            var tip = P(118, 44);
            Paw(P(80, 30), P(112, 50), c.Shade);
            Paw(P(76, 28), tip, c.Fur);
            if (f.Impact is float k) Burst(tip, k);
        });
    }

    void Sit()
    {
        float flick = MathF.Sin(f.Clock * 1.6f) * 3;
        Fill(Oval(56, 22, 36, 30), c.Fur);
        Stripe(P(48, 33), P(50, 24)); Stripe(P(58, 36), P(60, 27));
        Spots(P(52, 24), 14, 0.8f);
        Leg(P(84, 26), P(84, 12), 11, c.Shade);
        Leg(P(70, 26), P(70, 12), 11.5f, c.Fur);
        Fill(Oval(72, 34, 46, 42), c.Fur);
        Fill(Oval(76, 32, 24, 26), c.Belly, false);
        Stroke(Bezier(P(46, 14), P(30, 9), P(78, 8), P(100 + flick, 15 + MathF.Abs(flick) * 0.5f)), 11, c.Fur, true);
        if (f.Lean != 0) Rotated(P(76, 60), f.Lean, () => Head(P(76, 76), 3));
        else Head(P(76, 76), 3);
    }

    void Sleep()
    {
        float br = MathF.Sin(f.Clock * 1.8f) * 1.5f;
        Fill(Oval(66, 24 + br / 2, 88, 36 + br), c.Fur);
        Stripe(P(48, 39 + br), P(51, 30)); Stripe(P(62, 41 + br), P(65, 31)); Stripe(P(76, 40 + br), P(79, 31));
        Spots(P(62, 28 + br / 2), 26, 1);
        Stroke(Bezier(P(26, 20), P(16, 7), P(64, 7), P(98, 12)), 11, c.Fur, true);
        Fill(Oval(90, 11, 15, 9), c.Fur);
        Rotated(P(102, 32), -0.12f, () => Head(P(102, 32), 2));
    }

    void Dangle()
    {
        float sw = MathF.Sin(f.Clock * 3) * 6;
        Stroke(Bezier(P(75, 24), P(77, 16), P(79 + sw * 0.6f, 11), P(73 + sw, 9)), 11, c.Fur, true);
        Leg(P(64, 28), P(62, 14), 11.5f, c.Fur);
        Leg(P(86, 28), P(88, 14), 11.5f, c.Fur);
        Fill(Oval(75, 42, 42, 50), c.Fur);
        Fill(Oval(75, 38, 24, 32), c.Belly, false);
        Leg(P(60, 58), P(56, 44), 11, c.Fur);
        Leg(P(90, 58), P(94, 44), 11, c.Fur);
        Head(P(75, 88), 0);
    }

    /// <summary>Kavga: sırt kamburlaşmış, kuyruk kabarık ve dik, bir pati havada hızla savruluyor.</summary>
    void Fight()
    {
        float sw = MathF.Sin(f.Phase * MathF.PI * 2), foot = Ground + 5;
        float tailW = f.Puffed ? 16 : 11;
        Stroke(Bezier(P(40, 40), P(22, 48), P(16, 72), P(26 + sw * 2, 94)), tailW, c.Fur, true);
        Leg(P(46, 30), P(44, foot), 11, c.Shade);
        Leg(P(82, 34), P(88, foot), 11, c.Shade);
        Leg(P(54, 30), P(56, foot), 11.5f, c.Fur);
        Rotated(P(64, 40), 0.18f, () => Torso(P(64, 40)));
        Head(P(100, 64), 5);
        // Savrulan pati: -0.3 (aşağı) ile 1.0 (yukarı) arasında gidip gelir.
        var shoulder = P(86, 44);
        Paw(shoulder, Polar(shoulder, -0.3f + 1.3f * (0.5f + 0.5f * sw), 26), c.Fur);
        if (f.Dust is float t) FightDust(t);
    }

    /// <summary>Mama kabından yeme: ön taraf eğik, kafa aşağıda ve çiğnerken hafifçe inip kalkıyor, kuyruk mutlu yukarıda.</summary>
    void Eat()
    {
        float chew = MathF.Sin(f.Phase * MathF.PI * 2) * 1.6f, foot = Ground + 5, sway = MathF.Sin(f.Clock * 1.6f) * 4;
        Stroke(Bezier(P(40, 36), P(20, 42), P(14 + sway * 0.5f, 62), P(22 + sway, 76)), 11, c.Fur, true);
        Leg(P(50, 26), P(50, foot), 11, c.Shade);
        Leg(P(84, 20), P(88, foot), 11, c.Shade);
        Leg(P(42, 26), P(42, foot), 11.5f, c.Fur);
        Leg(P(76, 20), P(80, foot), 11.5f, c.Fur);
        Rotated(P(62, 30), -0.14f, () => Torso(P(62, 30)));
        Rotated(P(104, 32), -0.35f, () => Head(P(104, 30 + chew), 5));
    }

    /// <summary>
    /// Ekran kenarına tırmanma: duvar sağda (x≈119, gövdenin yarı genişliği kadar ötede), gövde dikey ve karnı duvara
    /// dönük. Çapraz ayak çiftleri sırayla uzanıp çekilir; itiş anında gövde yükselir, kuyruk aşağıda dengede sallanır.
    /// </summary>
    void Climb()
    {
        const float wall = 117;
        float ph = f.Phase * MathF.PI * 2, reach = MathF.Sin(ph) * 8, bob = MathF.Max(0, MathF.Sin(ph)) * 4;
        float sway = MathF.Sin(f.Clock * 2.4f) * 5;
        Stroke(Bezier(P(92, 32 + bob), P(86, 16), P(74 + sway, 12), P(68 + sway, 3)), 11, c.Fur, true);
        Leg(P(98, 36 + bob), P(wall, 24 - reach), 11, c.Shade);
        Leg(P(102, 82 + bob), P(wall, 96 + reach), 11, c.Shade);
        Rotated(P(96, 58 + bob), MathF.PI / 2, () => Torso(P(96, 58 + bob)));
        Leg(P(100, 40 + bob), P(wall, 30 + reach), 11.5f, c.Fur);
        Leg(P(104, 78 + bob), P(wall, 90 - reach), 11.5f, c.Fur);
        Head(P(90, 94 + bob), 7);
    }

    /// <summary>
    /// Kenara asılma: ön patiler kafanın iki yanından yukarı uzanıp kenarı (y≈128) kavramış, gövde aşağı sarkıyor,
    /// arka ayaklar sırayla tırmalıyor, kuyruk dengede sallanıyor.
    /// </summary>
    void Hang()
    {
        float kick = MathF.Sin(f.Phase * MathF.PI * 2), sw = MathF.Sin(f.Clock * 1.7f) * 3;
        Stroke(Bezier(P(75 + sw, 30), P(80 + sw, 18), P(68 + sw * 2, 12), P(74 + sw * 2.5f, 2)), 11, c.Fur, true);
        Leg(P(64 + sw, 30), P(60 + sw + kick * 3, 14 + Math.Max(0, kick) * 7), 11.5f, c.Fur);
        Leg(P(86 + sw, 30), P(90 + sw + kick * 3, 14 + Math.Max(0, -kick) * 7), 11.5f, c.Fur);
        Fill(Oval(75 + sw, 48, 40, 50), c.Fur);
        Fill(Oval(75 + sw, 44, 22, 30), c.Belly, false);
        Stripe(P(64 + sw, 60), P(66 + sw, 50)); Stripe(P(84 + sw, 60), P(86 + sw, 50));
        Paw(P(62 + sw * 0.6f, 64), P(56, 126), c.Fur);
        Paw(P(88 + sw * 0.6f, 64), P(94, 126), c.Fur);
        Head(P(75 + sw * 0.5f, 86), 0);
    }

    /// <summary>Arka ayaklar üstünde dikilip imlece yumruk: bir pati uzanır, diğeri geride bekler.</summary>
    void Swat()
    {
        float foot = Ground + 5;
        Stroke(Bezier(P(54, 14), P(34, 8), P(20, 12), P(14, 24 + MathF.Sin(f.Clock * 3) * 3)), 11, c.Fur, true);
        Leg(P(60, 30), P(56, foot), 11, c.Shade);
        Leg(P(74, 30), P(76, foot), 11.5f, c.Fur);
        Rotated(P(68, 48), -0.2f, () =>
        {
            Fill(Oval(68, 48, 40, 54), c.Fur);
            Fill(Oval(72, 46, 22, 34), c.Belly, false);
            Stripe(P(56, 62), P(58, 52)); Stripe(P(58, 48), P(60, 38));
        });
        Head(P(80, 88), 4);

        var shoulder = P(86, 64);
        float ext = MathF.Sin(MathF.PI * Math.Clamp(f.Phase, 0, 1));
        var punch = Polar(shoulder, f.Aim, 12 + 30 * ext);
        var guard = P(92, 56);
        var (punchCol, guardCol) = f.Arm == 0 ? (c.Fur, c.Shade) : (c.Shade, c.Fur);
        Paw(P(shoulder.X - 4, shoulder.Y - 2), guard, guardCol);
        Paw(shoulder, punch, punchCol);
        if (f.Impact is float k) Burst(punch, k);
    }
}
