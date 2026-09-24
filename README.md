# VeriCat

Masaüstünde yaşayan, tamamen özelleştirilebilir kediler. Görev çubuğunun üstünde gezinir, pencerelerin üstüne ve
içlerindeki bölümlere zıplar, fareyi kovalayıp yumruklar, birbirleriyle selamlaşıp kavga eder, okşanınca mırlarlar.

Windows 10/11 · .NET 10 · kurulum gerektirmeyen tek exe · kendini GitHub'dan günceller.

**[En son sürümü indir →](https://github.com/furkanisikay/VeriCat/releases/latest)**

## Özellikler

| | |
|---|---|
| **Özelleştirme** | İsim, kürk (hazır + özel renk), göz rengi, desen (çizgili, benekli, beyaz pati, beyaz göğüs), tasma rengi, aksesuar (zil, fiyonk, taç, çiçek, gözlük), boyut, hız, ses tonu. Tek tıkla rastgele kedi. |
| **Karakter** | Her kedinin oyunculuk, huysuzluk, sevecenlik ve enerji ayarı var ve davranışı gerçekten değiştiriyor: kavgacı kedi daha çok kavga eder, sevecen kedi okşanmaya dayanır ve diğerleriyle selamlaşır, uykucu kedi daha çok uyur. |
| **Fizik** | Yerçekimi, balistik zıplama, pencere taşınınca üstündeki kedi de kayar, çoklu monitör. Kediler hiçbir zaman ekranın dışına taşmaz. **Pencereyi hızlıca sallarsan üstündeki kedi (ve yumak) savrulur.** |
| **İhtiyaçlar** | Tokluk, sevgi, oyun ve enerji zamanla azalır. Acıkan kedi mama kabına gider, kap boşsa gelip sana miyavlar; yalnız kalan yanına gelir; sıkılan yumağa saldırır; yorulan uyur. Kafasının üstündeki düşünce baloncuğu ne istediğini söyler. Kedi menüsünde çubuklarla görünür. Hiçbir şey cezalandırmaz; uygulama kapalıyken geçen zaman nazikçe işler. |
| **Eşyalar** | **Yumak**: fırlat, seker, yuvarlanır; kediler peşinden koşup pati atar, birbirinden kapar. **Mama kabı**: koy, doldur (çift tık); aç kediler gelip yer, mama azalır. Eşyalar da pencerelerin üstünde durur, fareyle fırlatılabilir. |
| **İlişkiler** | Kediler kiminle kavga ettiğini ve kiminle selamlaştığını hatırlar. Arkadaşlar daha az kavga eder, yan yana kıvrılıp uyur; rakipler karşılaşınca tıslar. |
| **Gün döngüsü** | Gece (23:00–07:00) kediler çok daha fazla uyur. |
| **Pencere içleri** | Ara ara pencerelerin içindeki bölümlerin (araç çubuğu, panel, liste…) üst kenarlarına da zıplar. |
| **Kediler arası** | Aynı zemindeki kediler birbirinin içinden geçmez. Karşılaşınca geri döner, selamlaşır ya da kavga eder (kabarık kuyruk, tıslama, toz bulutu). Kaybeden kaçar. |
| **Okşama** | Fareyi tuşa basmadan kedinin üstünde gezdir: mırlar, kalpler çıkar. Bazen keyfi yoktur ve kaçar; çok okşanırsa pati atar. |
| **Fare avı** | Pusu, üstüne atlama, arka ayaklar üstünde yumruk kombosu. İsabet eden yumruk imleci biraz iter (kapatılabilir). |
| **Tasma** | Renkli tasma ve üstünde okunaklı isim etiketi. |
| **Performans** | Oturan/uyuyan kediler düşük kare hızında çizilir, pencere içi tarama önbelleklenir, tam ekran uygulama (oyun, video, sunum) öndeyken kediler saklanır ve simülasyon tamamen durur. |
| **Güncelleme** | GitHub'dan yeni sürümü denetler. İstersen arka planda sessizce güncellenir, istersen önce yenilikleri gösterip onay ister. İndirilen dosya SHA-256 ile doğrulanır. |

## Kullanım

- **Tepsi simgesi**: kedi ekle (hazır / rastgele), özelleştir, kedileri çağır, davranış ayarları, güncellemeler.
- **Kediye sağ tık** (ya da Ctrl + sol tık): kediye özel menü.
- **Sürükle bırak**: kediyi tutup fırlat. **Tıkla**: miyavlar. **Üstünde fareyi gezdir**: okşa.

Menüler Windows'un açık/koyu temasını izler. Ayarlar `%APPDATA%\VeriCat\settings.json`, günlük
`%LOCALAPPDATA%\VeriCat\logs\vericat.log` dosyasındadır.

## Sorun bildirme

Tepsi menüsü → **Sorun bildir**: tarayıcıda sürüm, Windows ve ekran bilgisiyle (ve günlüğün son satırlarıyla)
doldurulmuş bir GitHub issue açılır. Kullanıcı adın ve kişisel klasör yolların metinden çıkarılır; hiçbir şey sen
"Submit" demeden gönderilmez. Beklenmeyen bir hata olursa uygulama da bunu teklif eder. **Öneri gönder** fikirler içindir.

## Lisans

[MIT](LICENSE) © 2026 Furkan IŞIKAY

## Güncellemeler

Tepsi menüsü → **Güncellemeler**:

| Mod | Davranış |
|---|---|
| Otomatik yükle | Açılıştan kısa süre sonra ve 6 saatte bir denetler; yeni sürümü indirir, doğrular, yerine koyar ve yeniden başlar. Açılışta "yenilikler" bildirimi gösterir. |
| Sorarak yükle (varsayılan) | Yeni sürüm çıkınca bildirim gösterir; tıklayınca sürüm notlarını gösteren pencere açılır: *Güncelle*, *Sonra*, *Bu sürümü atla*. |
| Kapalı | Kendiliğinden denetlemez. *Şimdi denetle* yine çalışır. |

Exe'nin bulunduğu klasöre yazılamıyorsa (ör. `Program Files`) güncelleme yapılamaz; uygulama bunu söyler.

## Geliştirme

Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build VeriCat.slnx
dotnet test VeriCat.slnx
./build/publish.sh          # macOS/Linux → artifacts/release/VeriCat.exe (+ .sha256)
.\build\publish.ps1         # Windows
```

Yerel derlemeler `0.0.0` sürümündedir ve kendini güncellemez.

## Sürüm yayınlama

Elle bir şey yapmaya gerek yok. `main`/`master`'a her merge'de [release](.github/workflows/release.yml) iş akışı
commit mesajlarına bakarak sürüm numarasını belirler, `CHANGELOG.md`'yi günceller, etiketi atar ve exe'yi
sürüm notlarıyla birlikte GitHub Release olarak yayınlar. Commit kuralları: [CONTRIBUTING.md](CONTRIBUTING.md).

## Proje yapısı

```
src/
  VeriCat.Core/        Platformdan bağımsız: davranış, fizik, çarpışma, platformlar, ayarlar, ses sentezi, güncelleme mantığı
  VeriCat.Desktop/     Windows: tepsi, katmanlı pencereler, Win32 tarama, çizim, tema/menüler, özelleştirme, güncelleyici
tests/
  VeriCat.Core.Tests/  xUnit (deterministik simülasyon, sahte HTTP)
build/                 Yayın betikleri
docs/                  Mimari notları
```

Ayrıntılar: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
