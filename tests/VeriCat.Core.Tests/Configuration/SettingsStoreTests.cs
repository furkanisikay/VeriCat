using VeriCat.Core.Appearance;
using VeriCat.Core.Audio;
using VeriCat.Core.Configuration;

namespace VeriCat.Core.Tests.Configuration;

public sealed class SettingsStoreTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "vericat-tests-" + Guid.NewGuid().ToString("N"));

    string PathOf(string name) => Path.Combine(dir, name);

    [Fact]
    public void Round_trips_settings_and_cats()
    {
        var store = new SettingsStore(PathOf("settings.json"));
        var s = new AppSettings { Chase = false, ShowNames = false };
        s.Cats.Add(new CatConfig { Name = "Boncuk", Collar = 0x123456, Spec = CoatSpec.Presets[2].Spec, Scale = 1.5 });

        store.Save(s);
        var loaded = store.Load();

        Assert.False(loaded.Chase);
        Assert.False(loaded.ShowNames);
        var cat = Assert.Single(loaded.Cats);
        Assert.Equal("Boncuk", cat.Name);
        Assert.Equal(0x123456u, cat.Collar);
        Assert.Equal(CoatSpec.Presets[2].Spec, cat.Spec);
        Assert.Equal(1.5, cat.Scale);
    }

    [Fact]
    public void Reads_the_legacy_file_when_there_is_no_new_one()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(PathOf("old.json"), """{"Scale":1.5,"Windows":false,"Cats":[{"Name":"Pamuk","Scale":1}]}""");

        var loaded = new SettingsStore(PathOf("settings.json"), PathOf("old.json")).Load();

        Assert.False(loaded.Windows);
        Assert.True(loaded.InnerWindows);   // eski dosyada yok: varsayılan
        Assert.Equal("Pamuk", Assert.Single(loaded.Cats).Name);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(PathOf("settings.json"), "{ bu json değil");

        var loaded = new SettingsStore(PathOf("settings.json")).Load();

        Assert.Empty(loaded.Cats);
        Assert.True(loaded.Chase);
    }

    [Theory]
    [InlineData("  Tekir  ", "Tekir")]
    [InlineData("", null)]
    [InlineData("\t\n", null)]
    [InlineData("Çokuzunbirkedismi123", "Çokuzunbirkedism")]
    public void Names_are_trimmed_to_fit_the_collar(string input, string? expected)
    {
        Assert.Equal(expected, CatConfig.SanitizeName(input));
    }

    [Fact]
    public void Presets_cycle_coats_and_collars()
    {
        var a = CatConfig.Preset(0, 1, new Random(1));
        var b = CatConfig.Preset(1, 1, new Random(1));
        var wrapped = CatConfig.Preset(CoatSpec.Presets.Count, 1, new Random(1));

        Assert.NotEqual(a.Collar, b.Collar);
        Assert.Equal(a.Name, wrapped.Name);
    }

    [Fact]
    public void Synthesized_sounds_are_valid_wav()
    {
        foreach (var wav in new[] { SoundSynth.Meow(600, 0.4), SoundSynth.Hiss(new Random(1)), SoundSynth.Swat(new Random(1)), SoundSynth.Purr(new Random(1)) })
        {
            Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
            Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
            Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
