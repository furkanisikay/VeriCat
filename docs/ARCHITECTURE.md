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
   - aynı zemindeki kediler arasındaki temasları çözer (üstünden atlama, yol verme, selam, kovalamaca, kavga),
   - yoldaki duran eşyanın üstünden atlatır (`ResolveObstacles`),
   - her kedinin karesini görünümüne gönderir.

## Kedi durum makinesi

`Cat` kısmi sınıftır; her dosya bir konuyu taşır:

| Dosya | İçerik |
|---|---|
| `Cat.cs` | Durum, döngü, karar verme, `Sprite` üretimi |
| `Cat.Movement.cs` | Yürüme, pencereyle kayma, zıplama planı, düşme, iniş |
| `Cat.Interaction.cs` | Sürükleme, tıklama, okşama |
| `Cat.Hunting.cs` | İmleç kovalama, pusu, atlayış, yumruk |
| `Cat.Hanging.cs` | Tepeye yakın kenara ön patilerle asılma, tam ekran pencereye perde gibi tırmanma |
| `Cat.Climbing.cs` | Yüksekteki imleç: pencereden basamak, ekran kenarına sıçrama, tırmanma, duvardan sekme |
| `Cat.Social.cs` | Üstünden atlama, yol verme, kovalamaca, kavga, kazanan seçimi |
| `Cat.Needs.cs` | İhtiyaca göre hedef seçme, hedefe gitme, yeme, yumakla oynama, sokulma, düşünce baloncuğu |
| `Cat.Bounds.cs` | Ekranda kalma |

```
          ┌──────── Decide ────────┐
          ▼                        │
 Walk ─ Sit ─ Sleep ─ Chase ──► Stalk ──► Crouch ──► Air (Pounce)
  │                     │                    ▲
  │                     └──► Swat ───────────┘ (tekrar kovala / otur)
  │
  │                     └──► (imleç çok yüksek) ──► pencereye zıpla | duvara sıçra ──► Climb ──► Air (Pounce)
  ├── temas ──► Fight ──► Flee (kaybeden) / Sit (kazanan)
  └── okşama ─► Petted ──► Happy | Swat → Flee | Flee
```

## Yüksekteki imleç

İmleç normal zıplama yüksekliğinin (`MaxJumpHeight`) üstündeyse kedi önce **ister mi** diye bakar (oyunculuk, sıkıntı,
enerji; karar birkaç saniye geçerli). İsterse sırayla:

1. İmlece yaklaştıran, zıplanabilecek en yüksek pencere kenarına zıplar; inince kovalamaya devam eder (basamak basamak).
2. Yoksa imlece en yakın **kapalı** ekran kenarına (yanında başka ekran olmayan) koşar ve duvara sıçrar.
3. Duvara çarpan av zıplayışı: imleç duvardan uzaktaysa bir kez **duvardan seker** (daha yükseğe, imlece doğru),
   değilse duvara **tutunur** (`Climb`). Tırmanış ritmiktir (itiş–duraklama), gücü karaktere ve enerjiye bağlıdır;
   imleç menzile girince duvardan tepip atlar, gücü biterse kayıp bırakır.

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

## İhtiyaçlar, hedefler ve ilişkiler

- `Vitals` (Core): tokluk, sevgi, oyun, enerji (0–1). Saniyede azalır; uyku enerjiyi, yeme tokluğu, okşama sevgiyi,
  oyun (kovalama, yumruk, yumak) eğlenceyi doldurur. `CatConfig` ile saklanır; kapalıyken geçen süre üçte bir hızla
  ve bir tabanın altına indirmeden uygulanır.
- `Cat.Needs.cs`: `Decide()` önce acil ihtiyaca bakar ve bir **hedef** (`Goal`) seçer: mama kabı, yumak, uyuyan
  arkadaşın yanı ya da imleç (mama/sevgi istemek). `Seek` durumu hedefe yürür; hedef aşağıdaysa kenardan atlar,
  yukarıdaysa `TryJumpTo` ile balistik zıplar. Varınca `Eat`, pati, uyku ya da miyavlama.
- Düşünce baloncuğu (`Emote`) ihtiyacı görünür kılar; çizimi `CatPainter.Effects`.
- `BondBook`: kedi çiftleri için -1…1 ilişki. Kavga −, selam +. Arkadaşlarda kavga ¼, selam ×2; uyuyan arkadaşın
  yanına sokulma ve temas olunca uyandırmama. Rakiplerde kavga ×2 (üst sınır %75), selam yok, tıslama.
- Gece (yerel saat 23–07) uyku ağırlığı ×4. Saat `CatEnvironment.LocalTime` ile verilir (testte sabit).

## Eşyalar ve pencere eylemsizliği

`Prop` (Core) kediden bağımsız bir fizik gövdesidir: yerçekimi, yumakta sekme ve yuvarlanma sürtünmesi, kabın hemen
durması, pencereyle birlikte kayma, ekran sınırları, fareyle tutup fırlatma. Hareket etmeyen eşya hiç çizilmez.
`Colony.ResolveToys` yuvarlanan yumağın kedilere çarpmasını ve refleks patileri çözer.

Hem kedi hem eşya, üstünde durduğu pencerenin **hızını** ölçer (iki tarama arası yer değişimi / geçen süre).
2200 px/sn'yi aşan sallamada eylemsizlikle savrulur. Uzun süre duran bir pencerenin ilk hareketi yavaş sayılır;
böylece pencereyi yakalamak ya da yapıştırmak (snap) kediyi fırlatmaz, sadece gerçekten sallamak fırlatır.

## Tanılama

`Log` (Core): dönen, 512 KB sınırlı dosya günlüğü; pencere başlığı gibi kişisel veri yazılmaz. `IssueReport`
önceden doldurulmuş GitHub issue adresi üretir: ortam tablosu, hata, günlük kuyruğu; kullanıcı adı/ev klasörü
çıkarılır, adres 7500 karakteri aşmayacak şekilde kısaltılır. Desktop tarafı yakalanmayan hataları günlüğe yazar ve
bildirmeyi teklif eder.

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
