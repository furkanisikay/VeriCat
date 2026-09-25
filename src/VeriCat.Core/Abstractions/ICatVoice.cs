namespace VeriCat.Core.Abstractions;

/// <summary>Kedinin çıkarabildiği sesler.</summary>
public interface ICatVoice
{
    void Meow(double pitch, bool scared = false);
    void Purr();
    void Hiss();
    void Swat();

    /// <summary>Pençeleriyle tırmalama (kenara asılırken, duvara tutunurken).</summary>
    void Scratch();
}
