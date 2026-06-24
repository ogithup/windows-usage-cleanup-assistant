# Windows Usage Cleanup Assistant

## TR

### Proje Nedir?
`Windows Usage Cleanup Assistant`, Windows üzerinde yüklü programları analiz eden, kullanım sinyallerini toplayan, güvenli temizlik alanlarını tarayan ve kullanıcıya açıklanabilir öneriler sunan bir `WPF` masaüstü uygulamasıdır.

Uygulama doğrudan tehlikeli silme yapmaz. Mantık şu sırayı izler:

1. Programları ve kullanım verilerini analiz eder
2. Riskleri gösterir
3. Kullanıcıya açıklama sunar
4. Güvenli alanlar için onaylı cleanup akışı çalıştırır
5. Uninstall tarafında yalnızca Windows’un kendi uninstall komutunu açar

### Özellikler

- Registry üzerinden yüklü program envanteri çıkarma
- Process takibi ile kullanım geçmişi toplama
- `SQLite` ile kullanım verisi kalıcılığı
- Program ile process eşleştirme
- Kural tabanlı cleanup öneri motoru
- Türkçe kullanıcı dostu açıklama üretimi
- Güvenli disk cleanup taraması
- `HTML`, `CSV`, `JSON` rapor export
- Modern `MaterialDesignInXaml` tabanlı arayüz
- Güvenli uninstall akışı

### Teknolojiler

- `.NET 8`
- `C#`
- `WPF`
- `MaterialDesignThemes`
- `Microsoft.Data.Sqlite`

### Proje Yapısı

- `Models/`: veri modelleri ve DTO'lar
- `Services/`: envanter, kullanım takibi, cleanup, rapor ve açıklama servisleri
- `ViewModels/`: `MVVM` görünüm modelleri
- `Views/`: ekran bileşenleri
- `Commands/`: `RelayCommand`

### Ekranlar

- `Dashboard`
- `Installed Apps`
- `Usage Tracking`
- `Cleanup`
- `Reports`
- `Settings`

### Nasıl Çalıştırılır?

#### Gereksinimler

- Windows 10 / Windows 11
- `.NET 8 SDK`

#### 1. Repoyu klonla

```powershell
git clone https://github.com/ogithup/windows-usage-cleanup-assistant.git
cd windows-usage-cleanup-assistant
```

#### 2. Bağımlılıkları geri yükle

```powershell
dotnet restore
```

#### 3. Uygulamayı derle

```powershell
dotnet build
```

#### 4. Uygulamayı çalıştır

```powershell
dotnet run
```

Alternatif olarak Visual Studio ile:

1. Klasörü aç
2. `WindowsUsageCleanupAssistant.csproj` dosyasını yükle
3. `Start` ile çalıştır

### Uygulama Nasıl Çalışır?

- Yüklü programlar şu registry yollarından okunur:
  - `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall`
  - `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall`
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall`
- Çalışan process’ler periyodik olarak okunur
- Kullanım verileri yerel `SQLite` veritabanında tutulur
- Programlar risk, boyut, kullanım ve bağımlılık sinyallerine göre etiketlenir
- Disk cleanup yalnızca güvenli kategoriler için çalışır:
  - `User Temp`
  - `Windows Temp`
  - `Recycle Bin`
  - `Windows thumbnail cache`

### Güvenlik Notları

- `Downloads`, `Documents`, `Desktop`, `Program Files`, Windows sistem dosyaları ve proje klasörleri cleanup kapsamına dahil değildir.
- Uygulama sessiz uninstall yapmaz.
- LLM katmanı yalnızca açıklama ve raporlama için kullanılır.
- Cleanup ve uninstall denemeleri loglanır.

### Çıktılar

- Yerel `SQLite` kullanım veritabanı
- Cleanup log dosyası
- `HTML`, `CSV`, `JSON` raporları

---

## EN

### What Is This Project?
`Windows Usage Cleanup Assistant` is a `WPF` desktop application for Windows that analyzes installed programs, collects usage signals, scans safe cleanup areas, and presents explainable recommendations to the user.

The application does not perform dangerous direct deletion. Its flow is:

1. Analyze installed applications and usage data
2. Show risk indicators
3. Provide user-friendly explanations
4. Run confirmed cleanup only for safe categories
5. For uninstall, only launch Windows' own uninstall command

### Features

- Installed program inventory from Windows Registry
- Process-based usage tracking
- Persistent usage storage with `SQLite`
- Program-to-process matching
- Rule-based cleanup recommendation engine
- User-friendly Turkish explanation generation
- Safe disk cleanup scanning
- `HTML`, `CSV`, `JSON` report export
- Modern `MaterialDesignInXaml` interface
- Safe uninstall flow

### Technologies

- `.NET 8`
- `C#`
- `WPF`
- `MaterialDesignThemes`
- `Microsoft.Data.Sqlite`

### Project Structure

- `Models/`: data models and DTOs
- `Services/`: inventory, usage tracking, cleanup, reporting, and explanation services
- `ViewModels/`: `MVVM` view models
- `Views/`: UI screens
- `Commands/`: `RelayCommand`

### Screens

- `Dashboard`
- `Installed Apps`
- `Usage Tracking`
- `Cleanup`
- `Reports`
- `Settings`

### How To Run

#### Requirements

- Windows 10 / Windows 11
- `.NET 8 SDK`

#### 1. Clone the repository

```powershell
git clone https://github.com/ogithup/windows-usage-cleanup-assistant.git
cd windows-usage-cleanup-assistant
```

#### 2. Restore dependencies

```powershell
dotnet restore
```

#### 3. Build the application

```powershell
dotnet build
```

#### 4. Run the application

```powershell
dotnet run
```

Alternatively, with Visual Studio:

1. Open the folder
2. Load `WindowsUsageCleanupAssistant.csproj`
3. Run with `Start`

### How It Works

- Installed applications are read from these registry paths:
  - `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall`
  - `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall`
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall`
- Running processes are sampled periodically
- Usage data is stored in a local `SQLite` database
- Programs are labeled using risk, size, usage, and dependency signals
- Disk cleanup only targets safe categories:
  - `User Temp`
  - `Windows Temp`
  - `Recycle Bin`
  - `Windows thumbnail cache`

### Safety Notes

- `Downloads`, `Documents`, `Desktop`, `Program Files`, Windows system files, and user project folders are excluded from cleanup.
- The app does not perform silent uninstall.
- The LLM layer is used only for explanation and reporting.
- Cleanup and uninstall attempts are logged.

### Outputs

- Local `SQLite` usage database
- Cleanup log file
- `HTML`, `CSV`, `JSON` reports
