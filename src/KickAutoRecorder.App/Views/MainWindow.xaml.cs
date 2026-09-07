using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using KickAutoRecorder.App.ViewModels;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.App.Views;

public partial class MainWindow : Window
{
    private readonly IRecordingOrchestrator _orchestrator;
    private bool _isShuttingDown;

    public MainWindow(MainViewModel viewModel, IRecordingOrchestrator orchestrator)
    {
        InitializeComponent();
        DataContext = viewModel;
        _orchestrator = orchestrator;
        Closing += OnMainWindowClosing;
    }

    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isShuttingDown) return;

        // Cancel immediate WPF close to allow graceful FFmpeg process termination
        e.Cancel = true;
        _isShuttingDown = true;
        IsEnabled = false; // Prevent further user clicks

        try
        {
            await Task.Run(async () =>
            {
                await _orchestrator.StopAllActiveRecordingsAsync();
            });
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }
}
