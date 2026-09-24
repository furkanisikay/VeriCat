# Katkı rehberi

## Commit mesajları → sürüm numarası

Sürüm numarası ve sürüm notları commit mesajlarından **otomatik** üretilir
([Conventional Commits](https://www.conventionalcommits.org/tr/v1.0.0/) + semantic-release).

```
<tür>(<kapsam, isteğe bağlı>): <kısa açıklama>
```

| Tür | Ne zaman | Sürüm etkisi | Notlarda |
|---|---|---|---|
| `feat` | Yeni özellik | **minor** (1.2.0 → 1.3.0) | Yenilikler |
| `fix` | Hata düzeltme | patch (1.2.0 → 1.2.1) | Düzeltmeler |
| `perf` | Performans | patch | Performans |
| `refactor` | Davranışı değiştirmeyen iyileştirme | patch | İyileştirmeler |
| `docs`, `test`, `ci`, `build`, `chore`, `style` | Diğer | yok | gizli |
| `feat!:` ya da gövdede `BREAKING CHANGE:` | Uyumsuz değişiklik | **major** (1.x → 2.0.0) | |

Örnekler:

```
feat(kedi): uykucu kediler güneşli pencere kenarlarını seçer
fix(hareket): pencere küçültülünce kedi havada asılı kalmıyor
perf: oturan kedilerin kare hızı düşürüldü
```

## Pull request

- PR başlığı da aynı biçimde olmalı (CI denetler). Squash merge'de başlık commit mesajı olur.
- `dotnet test VeriCat.slnx` geçmeli; davranış değişikliği içeren her şeye test ekleyin
  (`tests/VeriCat.Core.Tests/Support/Sim.cs` deterministik bir masaüstü simülasyonu sağlar).
- Windows'a özgü kod yalnızca `VeriCat.Desktop` içine girer; mantık `VeriCat.Core`'da kalır.
