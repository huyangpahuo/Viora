using System.Windows;
using Viora.UI.Services;

namespace Viora.App.Hosting;

/// <summary>MessageBox-based user alerts (UI project stays dialog-free/testable).</summary>
public sealed class AppAlert : IUiAlert
{
    public void Info(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "Viora", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warn(string title, string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool Confirm(string title, string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
