# VeriCat

Masaüstünde yaşayan kediler. Görev çubuğunun üstünde gezinir, pencerelerin üstüne ve içlerindeki bölümlere zıplar,
fareyi kovalayıp yumruklar, birbirleriyle itişip kavga eder, okşanınca mırlarlar.

Windows 10/11 · .NET 10 · WinForms · tek dosyalık, kurulum gerektirmeyen exe.

## Özellikler

| | |
|---|---|
| **Fizik** | Yerçekimi, balistik zıplama, pencere taşınınca üstündeki kedi de kayar, çoklu monitör. |
| **Pencere içleri** | Ara ara pencerelerin içindeki bölümlerin (araç çubuğu, panel, liste…) üst kenarlarına da zıplar. |
| **Çarpışma ve kavga** | Aynı zemindeki kediler birbirinin içinden geçmez. Karşılaşınca ya geri dönerler ya da kavga çıkar: kulaklar geride, kuyruk kabarık, tıslama, toz bulutu. Kaybeden kaçar; iri kedinin kazanma şansı fazladır. |
| **Okşama** | Fareyi tuşa basmadan kedinin üstünde gezdir: mırlar, gözlerini kısar, kalpler çıkar. Bazen keyfi yoktur ve kaçar; çok uzun okşanırsa bıkıp eline pati atar ya da gider. |
| **Fare avı** | İmleci kovalar, pusuya yatıp kıçını sallar, üstüne atlar; yakınındaysa arka ayakları üstünde kalkıp yumruk atar. İsabet eden yumruk imleci biraz iter (kapatılabilir; bir tuş basılıyken asla itmez). |
| **Tasma** | Her kedinin renkli bir tasması ve üstünde okunaklı isim etiketi var. Tasma rengi özelleştirilebilir. |
| **Özelleştirme** | İsim, kürk/göz/tasma rengi, çizgili desen, boyut, hız, ses tonu. |

## Kullanım

- Tepsi simgesine tıkla: kedi ekle, boyut, davranış ayarları, çıkış.
- Kediye **sağ tık** (ya da Ctrl + sol tık): kediye özel menü.
- **Sürükle bırak**: kediyi tutup fırlat.
- **Tıkla**: miyavlar ve sevinir.
- **Üstünde fareyi gezdir**: okşa.

Ayarlar `%APPDATA%\VeriCat\settings.json` dosyasında saklanır. Eski "Kedi" sürümünün ayarları ilk açılışta otomatik okunur.

## Derleme

Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build VeriCat.slnx          # tüm çözüm (Linux/macOS'ta da derlenir)
dotnet test VeriCat.slnx           # birim testleri
```

Tek dosyalık exe (`artifacts/publish/win-x64/VeriCat.exe`):

```powershell
.\build\publish.ps1     # Windows
```

```bash
./build/publish.sh      # macOS / Linux (çapraz derleme)
```

CI her push'ta testleri çalıştırır ve Windows exe'sini `VeriCat-win-x64` artefaktı olarak yükler.

## Proje yapısı

```
src/
  VeriCat.Core/        Platformdan bağımsız çekirdek: davranış, fizik, çarpışma, platform üretimi, ayarlar, ses sentezi
  VeriCat.Desktop/     Windows kabuğu: tepsi uygulaması, katmanlı pencereler, Win32 tarama, GDI+ çizim, ses çalma
tests/
  VeriCat.Core.Tests/  xUnit testleri (deterministik simülasyon)
build/                 Yayın betikleri
docs/                  Mimari notları
```

Ayrıntılar: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
