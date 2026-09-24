# Mimari

## Katmanlar

```
VeriCat.Desktop  ──►  VeriCat.Core
(net10.0-windows)     (net10.0, platformdan bağımsız)
      ▲
      │ (yok)
VeriCat.Core.Tests ──► VeriCat.Core
```

**Core**, Windows'a özgü hiçbir API kullanmaz. Dış dünyaya dört arayüzle bağlanır:

| Arayüz | Görev | Windows uygulaması |
|---|---|---|
| `IWorld` | Ekranlar, pencere çerçeveleri, platformlar | `WorldModel` (Core) + `Win32WorldScanner` |
| `IPointer` | İmleç konumu, tuş durumu, imleci itme | `CursorPointer` |
| `ICatVoice` | Miyav, mırlama, tıslama, pati sesi | `SoundPlayerVoice` |
| `ICatView` | Bir kareyi ekrana koyma | `CatWindow` (katmanlı pencere) |

Bu sayede tüm davranış Linux üzerinde, sahte saat ve sabit rastgele sayılarla birim testine tabi tutulabilir.

## Koordinatlar

Fizik y ekseni **yukarı** bakan bir sistemde çalışır (`y = -ekranY`). Dönüşüm yalnızca sınırda yapılır:
`RectU.FromScreen`, `CursorPointer` ve `CatWindow.Present`.

## Ana döngü

`TrayApplication` ~66 Hz'lik bir zamanlayıcıyla:

1. Saniyede 15 kez dünyayı tarar: `Win32WorldScanner` → anlık görüntüler → `WorldModel.Update` → `PlatformBuilder`.
2. `Colony.Step(dt)`:
   - her kedinin durum makinesini ilerletir (`Cat.Step`),
   - aynı zemindeki kediler arasındaki temasları çözer (itme, geri dönme, kavga başlatma),
   - her kedinin karesini görünümüne gönderir.

## Kedi durum makinesi

`Cat` kısmi sınıftır; her dosya bir konuyu taşır:

| Dosya | İçerik |
|---|---|
| `Cat.cs` | Durum, döngü, karar verme, `Sprite` üretimi |
| `Cat.Movement.cs` | Yürüme, pencereyle kayma, zıplama planı, düşme, iniş |
| `Cat.Interaction.cs` | Sürükleme, tıklama, okşama |
| `Cat.Hunting.cs` | İmleç kovalama, pusu, atlayış, yumruk |
| `Cat.Social.cs` | Çarpışma tepkisi, kavga, kazanan seçimi |

```
          ┌──────── Decide ────────┐
          ▼                        │
 Walk ─ Sit ─ Sleep ─ Chase ──► Stalk ──► Crouch ──► Air (Pounce)
  │                     │                    ▲
  │                     └──► Swat ───────────┘ (tekrar kovala / otur)
  │
  ├── temas ──► Fight ──► Flee (kaybeden) / Sit (kazanan)
  └── okşama ─► Petted ──► Happy | Swat → Flee | Flee
```

## Platformlar

`Platform(Y, MinX, MaxX, Owner, Part)`:

- `Owner < 0`: ekran zemini (görev çubuğunun üstü).
- `Part == 0`: pencerenin üst kenarı.
- `Part != 0`: pencerenin içindeki bir alt pencerenin (child window) üst kenarı.

`PlatformBuilder` öndeki pencerelerin örttüğü parçaları keser, başlık çubuğuna/pencere dibine yapışık iç kenarları ve
aynı yükseklikte iç içe gelen panelleri eler. İç bölümler yalnızca en öndeki 10 pencere için taranır.

Not: Chromium/Electron/UWP gibi kendi içini çizen uygulamalar alt pencere yayınlamaz; onlarda kediler yalnızca üst kenarı kullanır.

## Çizim

`CatPainter` her kareyi 150×135 temel birimlik bir kutuda, sağa bakar şekilde çizer; sola bakış aynalamayla yapılır.
Yazılar (zzz, isim etiketi) aynalanmadan ayrı bir geçişte çizilir. İsim etiketi tasmadaki madalyonun cihaz pikseli
konumuna asılır, yazı boyu okunaklılık için alt sınırın altına inmez ve kutunun dışına taşmaz.
