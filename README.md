# 🚀 KickVideo - Otomatik Yayın Kayıt, Video Studio & YouTube Otomatik Yükleyici

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4?style=flat&logo=windows)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=flat&logo=sqlite)
![FFmpeg](https://img.shields.io/badge/Encoder-FFmpeg-007808?style=flat&logo=ffmpeg)
![YouTube API v3](https://img.shields.io/badge/API-YouTube_Data_v3-FF0000?style=flat&logo=youtube)

**KickVideo (KickAutoRecorder)**, Kick.com platformundaki favori yayıncılarınızın canlı yayınlarını arka planda otomatik olarak takip eden, yayın başladığında **FFmpeg** ile yüksek kalitede kaydeden, stüdyo ekranında kırpıp hazırlamanıza ve **YouTube Data API v3** üzerinden doğrudan kanalınıza otomatik yüklemenize olanak sağlayan modern bir Windows masaüstü yazılımıdır.

---

## ✨ Öne Çıkan Özellikler

- 🟢 **Otomatik Kick Yayın Takibi & Kayıt**: Arka plan servisi (`StreamMonitoringWorker`) takip edilen Kick kanallarını kontrol eder; yayın başladığı anda otomatik FFmpeg kaydını başlatır.
- 🛠️ **Yarıda Kalan Kayıt Kurtarma (Startup Recovery Scan)**: Uygulama veya bilgisayar aniden kapansa dahi açılışta kesintiye uğrayan yarım kayıtları tespit eder ve kurtarır.
- 🎬 **Video Studio (Dahili Video İşleme & Kırpma)**:
  - Dahili **MediaElement Video Player** ile canlı önizleme.
  - **✂️ Kırpma (Trim) Modları**: Hızlı kayıpsız kesim (**FAST MODE**) veya milisaniye hassasiyetli kesim (**ACCURATE MODE**).
- 🖼️ **1280x720 YouTube Thumbnail Generator**: Videonun o anki karesinden tek tıkla YouTube kapak resmi yakalama (`📸 Kareyi Yakala`) veya harici görsel seçme.
- 📝 **YouTube Metadata & Taslak Yönetimi**: Başlık (100 char), Açıklama (5000 char), Etiketler, Oyun Adı, Kategori (Gaming vb.), Dil ve Gizlilik ayarlarını SQLite veritabanına taslak olarak kaydetme.
- ⚡ **FFmpeg Render Motoru & Kuyruk Sistemi (`VideoJobQueueWorker`)**: FastCopy, 1080p60 veya 4K dışa aktarım profilleri ile videoları arka planda sırayla işleme.
- 🔴 **YouTube Data API v3 Otomatik Yükleme Servisi (`YouTubeUploadWorker`)**:
  - Parçalı (Resumable Chunked Upload) yükleme altyapısı.
  - **Render + YouTube'a Yükle** pipeline akışı: Render bittiğinde video otomatik olarak YouTube yükleme kuyruğuna aktarılır.
  - **Sunucu Byte Offset Sorgulama & Recovery**: Yükleme sırasında internet veya uygulama kapansa dahi YouTube sunucusundan kalınan son byte sorgulanarak yükleme kaldığı yerden devam ettirilir.
  - **Şifreli DPAPI OAuth Token Saklama**: Windows Data Protection API (`ProtectedData`) ile şifrelenmiş güvenli OAuth 2.0 token yönetimi.
  - **Bozulmaz İzolasyon & Güvenlik**: YouTube yüklemesindeki olası network veya kota hataları yerel Kick kayıt motorunu asla etkilemez. Gizlilik varsayılan olarak **Private** saklanır.

---

## 📺 Uygulama Ekranları

1. **Dashboard (Genel Bakış)**: Takip edilen yayıncılar, canlı yayın yapanlar, aktif kayıtlar, boş disk alanı ve anlık kayıt telemetrisi (bitrate, dosya boyutu, segment sayısı, FFmpeg sağlık durumu).
2. **Yayıncılar**: Kick kanal URL'si veya kullanıcı adı ile yayıncı ekleme, yayın bazlı çözünürlük kalitesi belirleme ve otomatik kaydı açma/kapatma.
3. **Kayıt Geçmişi**: Geçmiş tüm kayıtların veritabanı listesi, anlık arama, medya oynatıcısında açma (`Oynat`), klasörde gösterme ve diskten silme.
4. **Ayarlar**: Kayıt çıktısının kaydedileceği klasör, disk güvenlik limiti (GB), FFmpeg otomatik tespiti, YouTube OAuth hesap bağlantısı, bağlantı testi ve Koyu/Açık tema geçişi.
5. **🎬 Video Studio**: Video kırpma, YouTube metadata hazırlama, thumbnail oluşturma, **Render Al**, **Render + YouTube'a Yükle** ve canlı yükleme ilerleme çubuğu.

---

## 🏗️ Proje Mimarisi

Proje temiz mimari (Clean Architecture) ve MVVM prensiplerine uygun olarak 5 ana katmandan oluşur:

```
KickAutoRecorder/
├── src/
│   ├── KickAutoRecorder.Core           # Veritabanı Entity'leri, Interface'ler, Enum'lar ve Modeller
│   ├── KickAutoRecorder.Infrastructure # SQLite EF Core DbContext, Repositories, YouTube Services, DPAPI
│   ├── KickAutoRecorder.Recording      # FFmpeg Recording Engine & Telemetry Monitoring
│   ├── KickAutoRecorder.Worker         # Background Workers (StreamMonitoring, VideoJobQueue, YouTubeUpload)
│   └── KickAutoRecorder.App            # WPF UI Views, ViewModels (MVVM) & Theme Manager
```

---

## 📋 Gereksinimler

- **İşletim Sistemi**: Windows 10 / 11 (x64)
- **Çalışma Zamanı**: [.NET 8.0 SDK / Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Video Motoru**: [FFmpeg & FFprobe](https://ffmpeg.org/download.html) (`ffmpeg.exe` ve `ffprobe.exe` uygulama klasöründe veya sistem PATH üzerinde bulunmalıdır).

---

## 🚀 Kurulum ve Çalıştırma

### 1. Repoyu Klonlayın
```bash
git clone https://github.com/cumakaya0000/kick.git
cd kick
```

### 2. Projeyi Derleyin
```bash
dotnet build src/KickAutoRecorder.App/KickAutoRecorder.App.csproj
```

### 3. Uygulamayı Çalıştırın
```bash
dotnet run --project src/KickAutoRecorder.App/KickAutoRecorder.App.csproj
```

---

## ⚙️ YouTube Data API v3 OAuth Kurulumu

Uygulamanın videoları otomatik olarak YouTube kanalınıza yükleyebilmesi için bir Google Cloud projesi yapılandırmanız gerekmektedir:

1. **[Google Cloud Console](https://console.cloud.google.com/)**'a gidin ve yeni bir proje oluşturun.
2. **APIs & Services ➔ Library** bölümünden **YouTube Data API v3** servisini etkinleştirin.
3. **OAuth consent screen** sekmesinde User Type olarak **External** seçin; Scopes olarak `.../auth/youtube.upload` ve `.../auth/youtube.readonly` ekleyin. Test kullanıcısı olarak kendi YouTube e-postanızı tanımlayın.
4. **Credentials ➔ Create Credentials ➔ OAuth client ID** adımlarını izleyin. Application Type olarak **Desktop App** seçin.
5. Elde ettiğiniz Client ID ve Client Secret bilgilerini `appsettings.json` dosyasına ekleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=kickvideodata.db"
  },
  "RecordingSettings": {
    "OutputFolder": "kayitlar",
    "MinDiskSpaceGB": 5
  },
  "YouTube": {
    "ClientId": "YOUR_CLIENT_ID.apps.googleusercontent.com",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  }
}
```

6. KickVideo uygulamasını açın ➔ **Ayarlar** sekmesinden **`[ 🔗 Hesabı Bağla ]`** butonuna basarak yetkilendirmeyi tamamlayın.

---

## 📄 Lisans & Teşekkürler

Bu proje MIT Lisansı altında sunulmaktadır. FFmpeg, CommunityToolkit.Mvvm ve Google APIs kütüphanelerinden faydalanılmıştır.
