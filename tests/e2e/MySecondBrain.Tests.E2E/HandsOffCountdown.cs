namespace MySecondBrain.Tests.E2E;

/// <summary>
/// Shows a floating borderless WPF countdown window ("⚠️ HANDS OFF — 3... 2... 1...")
/// before E2E tests launch, giving the user time to release mouse and keyboard.
/// WPF windows require STA; this helper runs on a dedicated STA thread.
///
/// NOTE: All WPF types are fully qualified (global::System.Windows.*) because
/// the test project has UseWindowsForms=true which creates ambiguous type references
/// (System.Drawing.Color vs System.Windows.Media.Color, FlaUI Grid vs WPF Grid, etc.).
/// </summary>
internal static class HandsOffCountdown
{
    public static void Show()
    {
        System.Exception? staError = null;
        var staThread = new System.Threading.Thread(() =>
        {
            try
            {
                var window = new global::System.Windows.Window
                {
                    WindowStyle = global::System.Windows.WindowStyle.None,
                    AllowsTransparency = true,
                    Background = new global::System.Windows.Media.SolidColorBrush(
                        global::System.Windows.Media.Color.FromArgb(0xCC, 0x1A, 0x1A, 0x2E)),
                    WindowStartupLocation = global::System.Windows.WindowStartupLocation.CenterScreen,
                    Topmost = true,
                    ShowInTaskbar = false,
                    Width = 520,
                    Height = 200,
                    ResizeMode = global::System.Windows.ResizeMode.NoResize,
                    FontFamily = new global::System.Windows.Media.FontFamily("Segoe UI"),
                };

                var grid = new global::System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new global::System.Windows.Controls.RowDefinition
                    { Height = new global::System.Windows.GridLength(1, global::System.Windows.GridUnitType.Star) });
                grid.RowDefinitions.Add(new global::System.Windows.Controls.RowDefinition
                    { Height = global::System.Windows.GridLength.Auto });
                grid.RowDefinitions.Add(new global::System.Windows.Controls.RowDefinition
                    { Height = new global::System.Windows.GridLength(1, global::System.Windows.GridUnitType.Star) });

                var warningText = new global::System.Windows.Controls.TextBlock
                {
                    Text = "\u26a0\ufe0f  HANDS OFF",
                    FontSize = 36,
                    FontWeight = global::System.Windows.FontWeights.Bold,
                    Foreground = new global::System.Windows.Media.SolidColorBrush(
                        global::System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)),
                    HorizontalAlignment = global::System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = global::System.Windows.VerticalAlignment.Bottom,
                    Margin = new global::System.Windows.Thickness(0, 0, 0, 4),
                };
                global::System.Windows.Controls.Grid.SetRow(warningText, 0);

                var countdownText = new global::System.Windows.Controls.TextBlock
                {
                    Text = "3",
                    FontSize = 64,
                    FontWeight = global::System.Windows.FontWeights.Bold,
                    Foreground = global::System.Windows.Media.Brushes.White,
                    HorizontalAlignment = global::System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = global::System.Windows.VerticalAlignment.Center,
                };
                global::System.Windows.Controls.Grid.SetRow(countdownText, 1);

                var subText = new global::System.Windows.Controls.TextBlock
                {
                    Text = "Release mouse & keyboard \u2014 E2E tests starting",
                    FontSize = 14,
                    Foreground = new global::System.Windows.Media.SolidColorBrush(
                        global::System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    HorizontalAlignment = global::System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = global::System.Windows.VerticalAlignment.Top,
                    Margin = new global::System.Windows.Thickness(0, 4, 0, 0),
                };
                global::System.Windows.Controls.Grid.SetRow(subText, 2);

                grid.Children.Add(warningText);
                grid.Children.Add(countdownText);
                grid.Children.Add(subText);
                window.Content = grid;

                window.Show();

                // Countdown: 3 → 2 → 1
                for (int i = 3; i >= 1; i--)
                {
                    countdownText.Text = i.ToString();
                    countdownText.Dispatcher.Invoke(
                        () => { }, global::System.Windows.Threading.DispatcherPriority.Render);
                    System.Threading.Thread.Sleep(1000);
                }

                // Brief "GO" flash
                countdownText.Text = "GO!";
                countdownText.Foreground = new global::System.Windows.Media.SolidColorBrush(
                    global::System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
                countdownText.Dispatcher.Invoke(
                    () => { }, global::System.Windows.Threading.DispatcherPriority.Render);
                System.Threading.Thread.Sleep(400);

                window.Close();
            }
            catch (System.Exception ex)
            {
                staError = ex;
            }
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (staError != null)
            System.Console.WriteLine($"[FIXTURE] Countdown window error (non-fatal): {staError.Message}");
    }
}
