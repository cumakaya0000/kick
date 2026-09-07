# Kick - Otomatik Yayýn Kaydedici ve YouTube Yükleyici

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4?style=flat&logo=windows)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=flat&logo=sqlite)
![FFmpeg](https://img.shields.io/badge/Encoder-FFmpeg-007808?style=flat&logo=ffmpeg)
![YouTube API](https://img.shields.io/badge/API-YouTube_v3-FF0000?style=flat&logo=youtube)

**Kick**, Kick.com platformundaki favori yayýncýlarýnýzýn canlý yayýnlarýný arka planda otomatik olarak takip eden, yayýn baþladýðýnda **FFmpeg** ile yüksek kalitede kaydeden, stüdyo ekranýnda kesip hazýrlamanýza ve **YouTube** kanalýnýza tek týkla en yüksek kalitede otomatik yüklemenize olanak saðlayan modern bir Windows masaüstü yazýlýmýdýr.

---

## 🌟 Öne Çýkan Özellikler

- **🤖 Otomatik Kick Yayýn Takibi & Kayýt**: Arka planda çalýþarak takip ettiðiniz kanallarý izler, yayýn baþladýðý an kayda girer.
- **🛡️ Yarýda Kalan Kayýt Kurtarma**: Elektrik kesintisi veya uygulamanýn aniden kapanmasý durumunda, yarým kalan kayýtlarý açýlýþta otomatik kurtarýr.
- **🎬 Geliþmiþ Video Stüdyosu**: 
  - Videolarýnýzý program içinde izleyin, istediðiniz kısımları milisaniye hassasiyetiyle kessin (Trim).
  - Kayýtlarýnýzý bilgisayarýnýzda boþ yer kaplamamasý için tek týkla tamamen silin.
- **🖼️ YouTube Küçük Resim (Thumbnail) Oluþturucu**: Videonun o anki karesini tek týkla YouTube kapak resmi olarak belirleyin.
- **🚀 Akýllý YouTube Yükleyicisi**: 
  - Render bittiðinde otomatik yükleme sýrasýna alýr.
  - Ýnternet kopsa dahi yüklemeye **kaldýðý byte'tan (Resumable Upload)** devam eder.
  - Videolar mümkün olan **en yüksek kalitede (10MB parçalar halinde)** yüklenir.
- **🌙 Koyu ve Açýk Tema Desteði**: Göz yormayan, tam ekran açýlabilen modern arayüz.

---

## 🖥️ Ekranlar ve Kullaným

1. **Dashboard (Genel Bakýþ)**: Canlý yayýn yapanlar, aktif kayýtlar ve anlýk disk durumunuzu tek ekranda görün.
2. **Yayýncýlar**: Takip etmek istediðiniz yayýncýlarý ekleyin, otomatik kaydý açýn veya kapatýn.
3. **Kayıt Geçmişi**: Biten kayýtlarý listeleyin, oynatýn veya eski kayýtlarý silerek diskte yer açýn.
4. **Video Studio**: Kesme, baþlýk/açýklama girme ve **Render + YouTube'a Yükle** iþlemlerini tek ekrandan yönetin.
5. **Ayarlar**: Kayýt klasörünü seçin, YouTube hesabýnýzý baðlayýn ve temanýzý deðiþtirin.

---

## 🛠️ Kurulum Adýmlarý

Uygulamayý kendi bilgisayarýnýza kurmak ve çalýþtýrmak çok basittir. 

### Gereksinimler
1. **Windows 10 veya 11** (64-bit)
2. **[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)** yüklü olmalýdýr.
3. **[FFmpeg](https://ffmpeg.org/download.html)** yüklü olmalýdýr. fmpeg.exe ve fprobe.exe dosyalarýný ya sistem PATH'ine ekleyin ya da uygulamanýn kurulduðu klasörün içine (veya \kayitlar\ klasörüne) atýn.

### Adým 1: Kaynak Kodunu Ýndirin
Git kullanarak projeyi bilgisayarýnýza kopyalayýn:
`ash
git clone https://github.com/cumakaya0000/kick.git
cd kick
`

### Adým 2: Tek Týkla Kurulum (Tavsiye Edilen)
Projenin içinde hazýrlanmýþ bir otomatik kurulum betiðimiz bulunmaktadýr. Bu betik, uygulamayý tek bir \.exe\ haline getirir ve masaüstünüze **Kick** adýyla yeni bir kýsayol oluþturur.

1. \src\ klasörüne gidin.
2. Terminalinizde (PowerShell) projeyi derleyin:
   `powershell
   dotnet publish ".\KickAutoRecorder.App\KickAutoRecorder.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:UseAppHost=true
   `
3. Ardýndan otomatik yükleyiciyi çalýþtýrýn:
   `powershell
   powershell.exe -ExecutionPolicy Bypass -File .\Install.ps1
   `
Bu adýmlardan sonra Masaüstünüzde **Kick** adýnda, profesyonel logosuyla birlikte bir kýsayol belirecektir. Çift týklayýp kullanmaya baþlayabilirsiniz. Denetim Masasý'ndan normal bir program gibi kaldýrabilirsiniz.

---

## 🔑 YouTube Hesabýný Baðlama (YouTube Data API v3)

Programýn kendi kanalýnýza video yükleyebilmesi için bir kereliðine API yetkilendirmesi yapmanýz gerekir. Endiþelenmeyin, bu anahtarlar sadece **sizin kendi bilgisayarýnýzda** þifreli olarak saklanýr.

1. [Google Cloud Console](https://console.cloud.google.com/)'a gidin.
2. Yeni bir proje oluþturun.
3. **APIs & Services -> Library** (Kütüphane) bölümünden **YouTube Data API v3**'ü bulup **Etkinleþtirin (Enable)**.
4. **OAuth consent screen (Onay Ekraný)**'na gidin, **External (Dýþ)** seçin. Test kullanýcýsý olarak kendi Gmail/YouTube adresinizi eklemeyi unutmayýn.
5. **Credentials (Kimlik Bilgileri) -> Create Credentials -> OAuth client ID** yolunu izleyin. Uygulama türü olarak **Desktop App (Masaüstü Uygulamasý)** seçin.
6. Size verilen **Client ID (Ýstemci Kimliði)** ve **Client Secret (Ýstemci Gizli Anahtarý)** bilgilerini kopyalayýn.
7. Kick uygulamasýný açýn, **Ayarlar** sekmesine gelin. Kopyaladýðýnýz bilgileri ilgili yerlere yapýþtýrýp **Hesabý Baðla** butonuna basýn.

Artýk Stüdyo ekranýndan tek týkla kendi kanalýnýza otomatik video yükleyebilirsiniz! Videolarınız varsayılan olarak **Gizli (Private)** olarak yüklenir, siz hazır olduğunuzda yayınlayabilirsiniz.