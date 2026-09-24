# Değişiklik günlüğü

## 1.0.0

### Eklenenler
- Kediler arası çarpışma: aynı zemindeki kediler birbirinin içinden geçmez, karşılaşınca geri döner ya da kavga eder.
- Kavga animasyonu (kabarık kuyruk, geri yatık kulaklar, tıslama, toz bulutu); kaybeden kaçar.
- Okşama: fareyle üstünde gezdirince mırlama, kalpler, başını yaslama; bazen kaçma, fazla okşanınca pati atma.
- Pencerelerin içindeki bölümlere (alt pencereler) ara ara zıplama. Tepsi menüsünden kapatılabilir.
- Tasma ve okunaklı isim etiketi; tasma rengi özelleştirilebilir, etiket tepsi menüsünden gizlenebilir.
- Gelişmiş fare avı: pusu + kıç sallama, imlece atlayış, arka ayaklar üstünde yumruk kombosu, isabet efekti ve sesi.
  Yumruk imleci biraz iter (kapatılabilir; tuş basılıyken hiç itmez).

### Değişenler
- Kod tabanı katmanlara ayrıldı: `VeriCat.Core` (platformdan bağımsız) + `VeriCat.Desktop` (Windows) + testler.
- .NET 7'den (desteği bitti) .NET 10 LTS'e geçildi.
- Ayarlar `%APPDATA%\VeriCat` altına taşındı; eski `%APPDATA%\Kedi` ayarları otomatik okunur. Ayar dosyası atomik yazılır.
- Tek dosyalık yayın ayarları publish profiline taşındı; günlük derleme hafifledi.
