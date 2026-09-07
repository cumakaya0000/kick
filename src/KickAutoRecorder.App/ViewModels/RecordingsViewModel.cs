using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.App.ViewModels;

public partial class RecordingsViewModel : ObservableObject
{
    private readonly IRecordingLogRepository _recordingLogRepository;
    private readonly List<RecordingLog> _allLogs = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<RecordingLog> RecordingLogs { get; } = new();

    public RecordingsViewModel(IRecordingLogRepository recordingLogRepository)
    {
        _recordingLogRepository = recordingLogRepository;
        _ = LoadLogsAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public async Task LoadLogsAsync()
    {
        IsLoading = true;
        try
        {
            _allLogs.Clear();
            var list = await _recordingLogRepository.GetAllAsync();
            _allLogs.AddRange(list);
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        RecordingLogs.Clear();
        var query = _allLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLowerInvariant();
            query = query.Where(r => 
                (r.StreamerUsername != null && r.StreamerUsername.ToLowerInvariant().Contains(term)) ||
                (r.StreamTitle != null && r.StreamTitle.ToLowerInvariant().Contains(term)) ||
                (r.FilePath != null && r.FilePath.ToLowerInvariant().Contains(term)) ||
                r.Status.ToString().ToLowerInvariant().Contains(term));
        }

        foreach (var log in query)
        {
            RecordingLogs.Add(log);
        }
    }

    [RelayCommand]
    private async Task DeleteLogAsync(RecordingLog? log)
    {
        if (log == null) return;

        var result = MessageBox.Show(
            $"'{log.StreamerUsername}' yayın kaydı geçmişini silmek istiyor musunuz?\n\n" +
            "[Evet] : Hem veritabanından hem de diskteki video dosyasından siler.\n" +
            "[Hayır] : Yalnızca kayıt geçmişinden siler, video dosyasını saklar.\n" +
            "[İptal] : İşlemi iptal eder.",
            "Kayıt Sil",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return;

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(log.FilePath) && File.Exists(log.FilePath))
                {
                    if (IsFileLocked(log.FilePath))
                    {
                        MessageBox.Show(
                            "⚠️ Bu video dosyası şu anda aktif kayıt sürecinde olduğu için kilitlidir ve silinemez. Lütfen önce kaydı durdurun.",
                            "Dosya Kilitli",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    File.Delete(log.FilePath);
                    
                    // Also check if directory is empty after deletion
                    var dir = Path.GetDirectoryName(log.FilePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya silinirken hata oluştu: {ex.Message}", "Silme Uyarısı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        try
        {
            await _recordingLogRepository.DeleteAsync(log.Id);
            _allLogs.Remove(log);
            RecordingLogs.Remove(log);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt silinirken veritabanı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenInExplorer(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            if (File.Exists(filePath))
            {
                var argument = $"/select,\"{filePath}\"";
                Process.Start("explorer.exe", argument);
            }
            else
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", $"\"{dir}\"");
                }
                else
                {
                    MessageBox.Show("Klasör veya dosya bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch
        {
            // Ignore explorer launch exceptions
        }
    }

    [RelayCommand]
    private void PlayVideo(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show("Video dosyası bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsFileLocked(filePath))
        {
            MessageBox.Show(
                "⚠️ Bu video dosyası şu anda aktif olarak kaydedildiği (FFmpeg tarafından yazıldığı) için kilitlidir.\n\n" +
                "Kayıt tamamlandıktan sonra veya kaydı durdurduğunuzda videoyu rahatça oynatabilirsiniz.",
                "Kayıt Devam Ediyor (Erişim Kilitli)",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Video oynatılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
