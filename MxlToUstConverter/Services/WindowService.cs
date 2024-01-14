using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MxlToUstConverter.Services;

/// <summary>
/// 簡易的なウィンドウ操作サービス
/// </summary>
internal class WindowService
{
    private Window? _window;

    /// <summary>MainWindowを取得する</summary>
    private Window CurrentWindow
        => this._window ??= ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!;

    /// <summary>ウィンドウを閉じる</summary>
    public void Close()
        => this.CurrentWindow.Close();

    public async Task<string?> SelectFileAsync(IReadOnlyList<FilePickerFileType> filter, string? title = null)
    {
        var window = this.CurrentWindow;

        var files = await TopLevel.GetTopLevel(window)!.StorageProvider.OpenFilePickerAsync(new()
        {
            FileTypeFilter = filter,
            Title = title,
        }).ConfigureAwait(false);

        return files.Select(i => i.TryGetLocalPath()).FirstOrDefault();
    }

    public async Task<string?> SelectSaveFileAsync(IReadOnlyList<FilePickerFileType> choices, string? title = null)
    {
        var window = this.CurrentWindow;

        var files = await TopLevel.GetTopLevel(window)!.StorageProvider.SaveFilePickerAsync(new()
        {
            FileTypeChoices = choices,
            Title = title,
        }).ConfigureAwait(false);

        return files?.TryGetLocalPath();
    }
}
