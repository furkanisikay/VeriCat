using VeriCat.Core.Abstractions;
using VeriCat.Core.Configuration;
using VeriCat.Core.World;

namespace VeriCat.Core.Behavior;

/// <summary>Kedinin dış dünyayla bağları. Testlerde sahteleri verilir.</summary>
public sealed class CatEnvironment
{
    public required IWorld World { get; init; }
    public required ICatVoice Voice { get; init; }
    public required IPointer Pointer { get; init; }
    public required AppSettings Settings { get; init; }

    /// <summary>Saniye cinsinden monoton saat.</summary>
    public required Func<double> Clock { get; init; }

    /// <summary>1 mantıksal nokta kaç piksel.</summary>
    public double Dpi { get; init; } = 1;

    public Random Random { get; init; } = Random.Shared;
}
