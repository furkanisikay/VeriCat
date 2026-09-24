namespace VeriCat.Core.Behavior;

// Kullanıcıyla doğrudan etkileşim: tutup sürükleme, tıklama, okşama.
public sealed partial class Cat
{
    /// <summary>Okşanmaya başladığında kaçma olasılığı ("bugün keyfim yok").</summary>
    internal const double MoodyChance = 0.15;

    double grabOffX, grabOffY, grabStartX, grabStartY, lastDragX, lastDragY, lastDragTime, dragVX, dragVY;
    bool dragging, grabbed;

    double petting, sleepPetting, petIdle, petPatience;

    public void Grab()
    {
        var m = Pointer.Position;
        grabStartX = lastDragX = m.X; grabStartY = lastDragY = m.Y;
        grabOffX = m.X - px; grabOffY = m.Y - py;
        lastDragTime = Now;
        dragVX = dragVY = 0; dragging = false; grabbed = true;
    }

    public void Drag()
    {
        if (!grabbed) return;
        var m = Pointer.Position;
        if (!dragging)
        {
            if (Math.Sqrt(Math.Pow(m.X - grabStartX, 2) + Math.Pow(m.Y - grabStartY, 2)) <= 4 * D) return;
            dragging = true;
            opponent = null;
            state = CatState.Dragged; stateTime = 0; platform = null; riding = null; pouncing = false;
            Voice.Meow(Config.Pitch, scared: true);
        }
        double now = Now, dt = Math.Max(0.001, now - lastDragTime);
        dragVX = dragVX * 0.5 + (m.X - lastDragX) / dt * 0.5;
        dragVY = dragVY * 0.5 + (m.Y - lastDragY) / dt * 0.5;
        lastDragX = m.X; lastDragY = m.Y; lastDragTime = now;
        px = m.X - grabOffX; py = m.Y - grabOffY;
        Present();
    }

    public void Release()
    {
        if (!grabbed) return;
        grabbed = false;
        if (!dragging)
        {   // sadece tıklandı
            Voice.Meow(Config.Pitch);
            if (state is not (CatState.Air or CatState.Crouch)) Set(CatState.Happy, 1.6, 2.4);
            return;
        }
        dragging = false;
        if (Now - lastDragTime > 0.08) dragVX = dragVY = 0;
        double v = Math.Sqrt(dragVX * dragVX + dragVY * dragVY), max = 1800 * D, k = v > max ? max / v : 1;
        vx = dragVX * k; vy = dragVY * k;
        state = CatState.Air; stateTime = 0;
    }

    /// <summary>Menü açılınca yarım kalan sürüklemeyi bırakır.</summary>
    public void CancelGrab()
    {
        grabbed = false;
        if (dragging) { dragging = false; state = CatState.Air; vx = vy = 0; }
    }

    /// <summary>
    /// Fare, tuşa basılmadan kedinin üstünde gezdirildi (<paramref name="amount"/> piksel).
    /// Yeterince okşanınca mırlar; bazen keyfi yoktur ve kaçar, uzun sürerse bıkıp pati atar ya da kaçar.
    /// </summary>
    public void Pet(double amount)
    {
        if (Paused || amount <= 0) return;
        switch (state)
        {
            case CatState.Sleep:
                // Uyuyan kedi hemen uyanmaz; biraz okşanınca gözünü açar.
                sleepPetting += amount;
                if (sleepPetting > 150 * D) { sleepPetting = 0; Set(CatState.Sit, 1.5, 3); }
                return;
            case CatState.Sit or CatState.Happy or CatState.Walk or CatState.Petted:
                break;
            default:
                return;
        }

        petting = Math.Min(petting + amount, 220 * D);
        petIdle = 0;
        if (state == CatState.Petted || petting < 90 * D) return;

        if (Rng.NextDouble() < MoodyChance) { RunFrom(Pointer.Position.X, scared: false); return; }
        Set(CatState.Petted, 1, 1);
        petPatience = R(4, 12);
        Voice.Purr();
    }

    void PettedStep(double dt)
    {
        if (petting > 0) { petIdle = 0; Voice.Purr(); }
        else petIdle += dt;

        if (petIdle > 0.8) { Set(CatState.Happy, 1.2, 2); return; }
        if (stateTime < petPatience) return;

        // Fazla okşandı: yarısında imlece bir pati atıp kaçar, yarısında doğrudan kaçar.
        if (Rng.NextDouble() < 0.5) StartSwat(punches: 1, fleeAfter: true);
        else RunFrom(Pointer.Position.X, scared: false);
    }

    /// <summary><paramref name="fromX"/>'ten uzağa koşar.</summary>
    void RunFrom(double fromX, bool scared)
    {
        facingRight = px >= fromX;
        petting = 0;
        Set(CatState.Flee, 1.0, 1.8);
        if (scared || Rng.Next(2) == 0) Voice.Meow(Config.Pitch, scared: true);
    }

    public void Meow()
    {
        Voice.Meow(Config.Pitch);
        if (state == CatState.Sleep) Set(CatState.Sit, 1, 2);
    }

    public void Sleep()
    {
        if (state is not (CatState.Air or CatState.Dragged or CatState.Crouch)) Set(CatState.Sleep, 15, 30);
    }

    public void Wake()
    {
        if (state == CatState.Sleep) Set(CatState.Sit, 1, 2);
    }
}
