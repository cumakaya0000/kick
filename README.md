# Kick - Otomatik Yayın Kaydedici ve YouTube Yükleyici

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4?style=flat&logo=windows)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=flat&logo=sqlite)
![FFmpeg](https://img.shields.io/badge/Encoder-FFmpeg-007808?style=flat&logo=ffmpeg)
![YouTube API](https://img.shields.io/badge/API-YouTube_v3-FF0000?style=flat&logo=youtube)

**Kick**, [Kick.com](https://kick.com) platformundaki favori yayıncılarınızın canlı yayınlarını arka planda otomatik olarak takip eden, yayın başladığında **FFmpeg** ile yüksek kalitede kaydeden, stüdyo ekranında kesip hazırlamanıza ve **YouTube** kanalınıza tek tıkla en yüksek kalitede otomatik yüklemenize olanak sağlayan modern bir Windows masaüstü yazılımıdır.

---

## 🌟 Öne Çıkan Özellikler

- **🤖 Otomatik Kick Yayın Takibi & Kayıt**: Arka planda çalışarak takip ettiğiniz kanalları izler, yayın başladığı an kayda girer.
- **🛡️ Yarıda Kalan Kayıt Kurtarma**: Elektrik kesintisi veya uygulamanın aniden kapanması durumunda, yarım kalan kayıtları açılışta otomatik kurtarır.
- **🎬 Gelişmiş Video Stüdyosu**: 
  - Videolarınızı program içinde izleyin, istediğiniz kısımları milisaniye hassasiyetiyle kesin (Trim).
  - Kayıtlarınızı bilgisayarınızda boş yer kaplamaması için tek tıkla tamamen silin.
- **🖼️ YouTube Küçük Resim (Thumbnail) Oluşturucu**: Videonun o anki karesini tek tıkla YouTube kapak resmi olarak belirleyin.
- **🚀 Akıllı YouTube Yükleyicisi**: 
  - Render bittiğinde otomatik yükleme sırasına alır.
  - İnternet kopsa dahi yüklemeye **kaldığı byte'tan (Resumable Upload)** devam eder.
  - Videolar mümkün olan **en yüksek kalitede (~10MB parçalar halinde)** yüklenir.
- **🌙 Koyu ve Açık Tema Desteği**: Göz yormayan, tam ekran açılabilen modern arayüz.

---

## 🖥️ Ekranlar ve Kullanım

1. **Dashboard (Genel Bakış)**: Canlı yayın yapanlar, aktif kayıtlar ve anlık disk durumunuzu tek ekranda görün.
2. **Yayıncılar**: Takip etmek istediğiniz yayıncıları ekleyin, otomatik kaydı açın veya kapatın.
3. **Kayıt Geçmişi**: Biten kayıtları listeleyin, oynatın veya eski kayıtları silerek diskte yer açın.
4. **Video Studio**: Kesme, başlık/açıklama girme ve **Render + YouTube'a Yükle** işlemlerini tek ekrandan yönetin.
5. **Ayarlar**: Kayıt klasörünü seçin, YouTube hesabınızı bağlayın ve temanızı değiştirin.

---

## 🛠️ Kurulum Adımları

Uygulamayı kendi bilgisayarınıza kurmak ve çalıştırmak çok basittir. 

### Gereksinimler
1. **Windows 10 veya 11** (64-bit)
2. **[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)** yüklü olmalıdır.
3. **[FFmpeg](https://ffmpeg.org/download.html)** yüklü olmalıdır. `ffmpeg.exe` ve `ffprobe.exe` dosyalarını ya sistem PATH'ine ekleyin ya da uygulamanın kurulduğu klasörün içine (veya `kayitlar` klasörüne) atın.

### Adım 1: Kaynak Kodunu İndirin
Git kullanarak projeyi bilgisayarınıza kopyalayın:
```bash
git clone https://github.com/cumakaya0000/kick.git
cd kick
```

### Adım 2: Tek Tıkla Kurulum (Tavsiye Edilen)
Projenin içinde hazırlanmış bir otomatik kurulum betiğimiz bulunmaktadır. Bu betik, uygulamayı tek bir `.exe` haline getirir ve masaüstünüze **Kick** adıyla yeni bir kısayol oluşturur.

1. `src` klasörüne gidin.
2. Terminalinizde (PowerShell) projeyi derleyin:
   ```powershell
   dotnet publish ".\KickAutoRecorder.App\KickAutoRecorder.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:UseAppHost=true
   ```
3. Ardından otomatik yükleyiciyi çalıştırın:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File .\Install.ps1
   ```
Bu adımlardan sonra Masaüstünüzde **Kick** adında, profesyonel logosuyla birlikte bir kısayol belirecektir. Çift tıklayıp kullanmaya başlayabilirsiniz. Denetim Masası'ndan normal bir program gibi kaldırabilirsiniz.

---

## 🔑 YouTube Hesabını Bağlama (YouTube Data API v3)

Programın kendi kanalınıza video yükleyebilmesi için bir kereliğine API yetkilendirmesi yapmanız gerekir. Endişelenmeyin, bu anahtarlar sadece **sizin kendi bilgisayarınızda** şifreli olarak saklanır.

1. [Google Cloud Console](https://console.cloud.google.com/)'a gidin.
2. Yeni bir proje oluşturun.
3. **APIs & Services -> Library** (Kütüphane) bölümünden **YouTube Data API v3**'ü bulup **Etkinleştirin (Enable)**.
4. **OAuth consent screen (Onay Ekranı)**'na gidin, **External (Dış)** seçin. Test kullanıcısı olarak kendi Gmail/YouTube adresinizi eklemeyi unutmayın.
5. **Credentials (Kimlik Bilgileri) -> Create Credentials -> OAuth client ID** yolunu izleyin. Uygulama türü olarak **Desktop App (Masaüstü Uygulaması)** seçin.
6. Size verilen **Client ID (İstemci Kimliği)** ve **Client Secret (İstemci Gizli Anahtarı)** bilgilerini kopyalayın.
7. Kick uygulamasını açın, **Ayarlar** sekmesine gelin. Kopyaladığınız bilgileri ilgili yerlere yapıştırıp **Hesabı Bağla** butonuna basın.

Artık Stüdyo ekranından tek tıkla kendi kanalınıza otomatik video yükleyebilirsiniz! Videolarınız varsayılan olarak **Gizli (Private)** olarak yüklenir, siz hazır olduğunuzda yayınlayabilirsiniz.