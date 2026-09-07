using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KickAutoRecorder.App.ViewModels;

namespace KickAutoRecorder.App.Views;

public partial class VideoStudioView : UserControl
{
    private readonly DispatcherTimer _positionTimer;

    public VideoStudioView()
    {
        InitializeComponent();

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _positionTimer.Tick += PositionTimer_Tick;

        Loaded += async (s, e) =>
        {
            if (DataContext is VideoStudioViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            if (DataContext is VideoStudioViewModel vm)
            {
                vm.CurrentPositionSeconds = MediaPlayer.Position.TotalSeconds;
                vm.CurrentPositionText = MediaPlayer.Position.ToString(@"hh\:mm\:ss");
                vm.TotalDurationSeconds = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                vm.TotalDurationText = MediaPlayer.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
            }
        }
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        MediaPlayer.Play();
        _positionTimer.Start();
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        MediaPlayer.Pause();
        _positionTimer.Stop();
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            if (Math.Abs(MediaPlayer.Position.TotalSeconds - e.NewValue) > 1)
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(e.NewValue);
            }
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MediaPlayer != null)
        {
            MediaPlayer.Volume = e.NewValue;
        }
    }
}
