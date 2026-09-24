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

## Ekranda kalma

İki katmanda sağlanır:

1. **Platformlar** (`PlatformBuilder.ClipToScreens`): pencere kenarları yalnızca bir ekranın içinde kalan kısımlarıyla
   platform olur; ekran dışına taşan pencere kediye yol açmaz. Yan yana ekranlar birleştirilir.
2. **Kedi** (`Cat.Bounds.cs`, her adımın sonunda): gövde (±44 temel birim) bulunduğu ya da en yakın ekranın
   kenarından taşmaz; yanında başka ekran olan kenarlar serbesttir. Havadaki kedi o ekranın tepesinde durur,
   kafası sığmayan platforma zıplamaz, üstünde durduğu pencere tepeye dayanırsa düşer. Hiçbir ekranda olmayan
   (farklı yükseklikteki monitörlerin arasındaki boşluk) kedi en yakın ekrana çekilir.

## Karakter

`Personality` (0–1 arası dört değer, 0.5 ortalama) olasılıkları ölçekler; 0.5'te davranış eski sabit değerlerle aynıdır:

| Özellik | Etkilediği |
|---|---|
| Oyunculuk | Kovalamaya karar verme, hızlı imleci fark etme, pusu kurma sıklığı |
| Huysuzluk | Karşılaşmada kavga olasılığı (iki kedinin toplamı), kavgayı kazanma, fazla okşanınca pati atma |
| Sevecenlik | Okşanınca kaçma olasılığı, okşanma sabrı, selamlaşma olasılığı |
| Enerji | Yürüme/zıplama ağırlığı ↑, uyku ağırlığı ↓ |

## Performans

- Oturan/uyuyan kediler 20 fps'de çizilir (`Cat.Present`), hareket edenler tam hızda.
- Pencere içi tarama, pencere yerinden oynamadıysa 0.5 sn önbelleklenir (`Win32WorldScanner`).
- Tam ekran uygulama öndeyken kedi pencereleri gizlenir, tarama ve simülasyon durur (`FullscreenDetector`).

## Güncelleme

```
GitHub Releases ──(api.github.com)──► GitHubReleaseClient ──► UpdatePolicy ──► UpdateService
                                           │ indir + SHA-256 doğrula                │
                                           ▼                                        ▼
                                  %LOCALAPPDATA%\VeriCat\updates          UpdateDialog (yenilikler)
                                           │
                                           ▼
                           UpdateInstaller: VeriCat.exe → VeriCat.exe.old, yeni exe yerine,
                           "--after-update" ile yeniden başlat (eski süreç kilidi bırakana dek bekler)
```

- Mantık (`SemVersion`, `ReleaseInfo`, `UpdatePolicy`, `ReleaseNotes`, indirme + doğrulama) Core'dadır ve testlidir.
- Yalnızca `github.com/furkanisikay/VeriCat/releases/download/...` adreslerinden indirilir; özet tutmazsa dosya silinir.
- Özet dosyası exe ile aynı sürümde yayınlanır: bozulmaya karşı korur, ama depo ele geçirilirse korumaz.
  Bunun için kod imzalama gerekir (bkz. yol haritası).
- `LastSeenVersion` ile güncellemeden sonraki ilk açılış algılanır ve yenilikler bildirilir.

## Sürüm hattı

`release.yml` → testler → `semantic-release`: commit analizi → `build/publish.sh <sürüm>` (exe + `.sha256`) →
`CHANGELOG.md` commit'i (`[skip ci]`) → `vX.Y.Z` etiketi → GitHub Release (notlar + dosyalar).

## Tema ve menüler

`Theming/`: `Theme` Windows'un açık/koyu uygulama temasını okur ve değişimini izler. `MenuRenderer` tüm menüleri
çizer (yuvarlak vurgu, ince ayraçlar), `ToggleMenuItem` sağda anahtar gösterir ve tıklanınca menüyü kapatmaz,
`MenuHeader` kedi menüsünün avatarlı başlığıdır, `Icons` DPI'a göre çizilen vektör ikonlardır. Windows 11'de
menü ve pencere köşeleri DWM ile yuvarlatılır. Özelleştirme ve güncelleme pencereleri aynı paleti kullanır.

## Çizim

`CatPainter` her kareyi 150×135 temel birimlik bir kutuda, sağa bakar şekilde çizer; sola bakış aynalamayla yapılır.
Yazılar (zzz, isim etiketi) aynalanmadan ayrı bir geçişte çizilir. İsim etiketi tasmadaki madalyonun cihaz pikseli
konumuna asılır, yazı boyu okunaklılık için alt sınırın altına inmez ve kutunun dışına taşmaz.
