using VeriCat.Core.Abstractions;
using VeriCat.Core.Rendering;

namespace VeriCat.Core.Tests.Support;

/// <summary>Sabit değerler döndüren, testte ayarlanabilen rastgele sayı üreteci.</summary>
internal sealed class ScriptedRandom : Random
{
    public double DoubleValue { get; set; } = 0.5;
    public int IntValue { get; set; }

    public override double NextDouble() => DoubleValue;
    public override int Next() => IntValue;
    public override int Next(int maxValue) => Math.Min(IntValue, Math.Max(0, maxValue - 1));
    public override int Next(int minValue, int maxValue) => Math.Clamp(minValue + IntValue, minValue, Math.Max(minValue, maxValue - 1));
}

internal sealed class FakeVoice : ICatVoice
{
    public int Meows, Purrs, Hisses, Swats;

    public void Meow(double pitch, bool scared = false) => Meows++;
    public void Purr() => Purrs++;
    public void Hiss() => Hisses++;
    public void Swat() => Swats++;
}

internal sealed class FakePointer : IPointer
{
    public (double X, double Y) Position { get; set; }
    public bool AnyButtonDown { get; set; }
    public List<(double Dx, double Dy)> Nudges { get; } = new();

    public void Nudge(double dx, double dy)
    {
        Nudges.Add((dx, dy));
        Position = (Position.X + dx, Position.Y + dy);
    }
}

internal sealed class NullView : ICatView
{
    public Sprite? Last { get; private set; }
    public bool Closed { get; private set; }

    public void Present(double feetX, double feetY, double scale, Sprite sprite) => Last = sprite;
    public void Close() => Closed = true;
}
