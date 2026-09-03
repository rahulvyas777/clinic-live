using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ClinicLive.Pocket;

/// <summary>
/// The only XAML-free native page in the app: a camera preview with ZXing decoding
/// frames, a viewfinder hint and a Cancel button. Resolves once with the first QR it
/// reads. Built in C# because it's forty lines and a post can show all of them.
/// </summary>
public sealed class ScanPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _result = new();
    private readonly CameraBarcodeReaderView _camera;

    public Task<string?> Result => _result.Task;

    public ScanPage()
    {
        BackgroundColor = Colors.Black;

        _camera = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormat.QrCode,
                AutoRotate = true,
                Multiple = false,
            },
            CameraLocation = CameraLocation.Rear,
        };
        _camera.BarcodesDetected += OnDetected;

        var hint = new Label
        {
            Text = "Point the camera at the QR on your ticket",
            TextColor = Colors.White,
            FontSize = 18,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(24, 48, 24, 0),
        };

        var cancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            TextColor = Color.FromArgb("#182A2D"),
            CornerRadius = 12,
            Margin = new Thickness(24, 0, 24, 48),
            HeightRequest = 52,
        };
        cancel.Clicked += (_, _) => Finish(null);

        Content = new Grid
        {
            Children =
            {
                _camera,
                new VerticalStackLayout { Children = { hint }, VerticalOptions = LayoutOptions.Start },
                new VerticalStackLayout { Children = { cancel }, VerticalOptions = LayoutOptions.End },
            },
        };
    }

    private void OnDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var first = e.Results.FirstOrDefault()?.Value;
        if (!string.IsNullOrWhiteSpace(first))
        {
            // Detection arrives on a camera thread; the page is UI.
            MainThread.BeginInvokeOnMainThread(() => Finish(first));
        }
    }

    private void Finish(string? value)
    {
        if (_result.TrySetResult(value))
        {
            _camera.BarcodesDetected -= OnDetected;
            _camera.IsDetecting = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        Finish(null);
        return base.OnBackButtonPressed();
    }
}
