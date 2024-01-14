using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MxlToUstConverter.Models.MusicXML;
using MxlToUstConverter.Models.UTAU;
using MxlToUstConverter.Utils;
using MxlToUstConverter.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace MxlToUstConverter.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly WindowService _windowService = new();

    /// <summary>出力時の文字コード</summary>
    private static readonly Encoding _outputEncoding = new UTF8Encoding(false);

    private ICommand? _closeCommand;
    private ICommand? _openFileCommand;
    private AsyncRelayCommand? _convertCommand;

    private string? _inputFilePath = null;
    private string _outputLog = string.Empty;

    private readonly static FilePickerFileType[] _inputFileTypes = [new("MusicXML") { Patterns = ["*.musicxml", "*.xml", "*.mxl"] }];
    private readonly static FilePickerFileType[] _outputFileTypes = [new("UTAU Sequence Text") { Patterns = ["*.ust"] }];

    /// <summary>入力ファイル</summary>
    public string? InputFilePath
    {
        get => this._inputFilePath;
        private set
        {
            if (this.SetProperty(ref this._inputFilePath, value))
            {
                this._convertCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>ログ</summary>
    public string OutputLog
    {
        get => this._outputLog;
        private set => this.SetProperty(ref this._outputLog, value);
    }

    /// <summary>閉じるコマンド</summary>
    public ICommand CloseCommand => this._closeCommand ??= new RelayCommand(
        () => this._windowService.Close());

    /// <summary>閉じるコマンド</summary>
    public ICommand OpenFileCommand => this._openFileCommand ??= new AsyncRelayCommand(async () =>
    {
        var file = await this._windowService.SelectFileAsync(_inputFileTypes);
        if (file is null)
            // ファイル未選択時は処理を終わる
            return;

        this.InputFilePath = file;
    });

    /// <summary>変換コマンド</summary>
    public AsyncRelayCommand ConvertCommand => this._convertCommand ??= new AsyncRelayCommand(async () =>
    {
        this.ClearLog();

        var outputFilePath = await this._windowService.SelectSaveFileAsync(_outputFileTypes);
        if (outputFilePath is null)
            // ファイル未選択時は処理を終わる
            return;

        var inputFile = this._inputFilePath!;

        byte[] data;
        try
        {
            data = File.ReadAllBytes(inputFile);
        }
        catch (Exception e)
        {
            this.AppendLog($"ファイルの読み込みに失敗しました。{Environment.NewLine}{e.Message}");
            return;
        }

        MusicXmlObject? mxlObj;
        try
        {
            using var ms = new MemoryStream(data);
            mxlObj = MusicXmlUtil.Parse(ms);
        }
        catch (Exception e)
        {
            this.AppendLog($"MusicXMLの読み込みに失敗しました。{Environment.NewLine}{e.Message}");
            return;
        }

        // パートを抽出
        var part = mxlObj?.Parts?.FirstOrDefault();
        if (part is null)
        {
            this.AppendLog("MusicXML内にパートが見つかりませんでした。");
            return;
        }

        // USTに変換
        IEnumerable<IUtauElement> elements;
        try
        {
            elements = UstUtils.BuildUstElements(part);
        }
        catch (Exception e)
        {
            this.AppendLog($"USTの生成に失敗しました。{Environment.NewLine}{e.Message}");
            return;
        }

        try
        {
            using var ofs = File.Open(outputFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            UstWriter.Write(ofs, _outputEncoding, elements);
        }
        catch (Exception e)
        {
            this.AppendLog($"ファイルの書き込みに失敗しました。{Environment.NewLine}{e.Message}");
            return;
        }

        this.AppendLog($"変換が完了しました。{Environment.NewLine}{outputFilePath}");

        // 入力情報をクリア
        this.ClearInputs();
    }
    , () => !string.IsNullOrEmpty(this._inputFilePath));

    /// <summary>
    /// ログに追記する
    /// </summary>
    /// <param name="line"></param>
    private void AppendLog(string line)
    {
        this.OutputLog += (line + Environment.NewLine);
    }

    /// <summary>
    /// ログをクリアする
    /// </summary>
    private void ClearLog()
    {
        this.OutputLog = string.Empty;
    }

    /// <summary>
    /// 入力情報をクリアする
    /// </summary>
    private void ClearInputs()
    {
        this.InputFilePath = null;
    }
}
