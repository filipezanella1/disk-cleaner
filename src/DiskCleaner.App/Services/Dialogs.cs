using System.Windows;

namespace DiskCleaner.App.Services;

public interface IDialogs
{
    bool Confirm(string title, string message);
    void Info(string title, string message);
}

/// <summary>MessageBox-based dialogs; safe to call from any thread.</summary>
public sealed class WpfDialogs : IDialogs
{
    public bool Confirm(string title, string message) =>
        OnUi(() => MessageBox.Show(Owner(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);

    public void Info(string title, string message) =>
        OnUi(() => MessageBox.Show(Owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information));

    private static Window Owner() => Application.Current.MainWindow;

    private static T OnUi<T>(Func<T> f) =>
        Application.Current.Dispatcher.CheckAccess() ? f() : Application.Current.Dispatcher.Invoke(f);
}
