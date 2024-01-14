using MxlToUstConverter.Models.UTAU;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace MxlToUstConverter.Utils;

internal class UstWriter : IDisposable
{
    private readonly Encoding _encoding;
    private readonly Stream _baseStream;
    private readonly StreamWriter _writer;
    private bool _disposed;
    private int _lyricCount = -1;

    public UstWriter(Stream stream, Encoding encoding)
    {
        this._encoding = encoding;
        this._baseStream = stream;
        this._writer = new StreamWriter(stream, encoding, leaveOpen: true);
    }

    public UstWriter(Stream stream) : this(stream, Encoding.UTF8)
    {
    }

    public static void Write(Stream stream, Encoding encoding, IEnumerable<IUtauElement> elements)
    {
        using (var writer = new UstWriter(stream, encoding))
        {
            writer.WriteVersion();

            foreach (var item in elements)
            {
                if (item is SettingElement setting)
                    writer.WriteSetting(setting);
                else if (item is LyricElement note)
                    writer.WriteLyric(note);
            }
        }
    }

    public void WriteVersion()
    {
        var writer = this._writer;
        var encoding = this._encoding;

        writer.WriteLine(
            $"""
            [#VERSION]
            UST Version1.2
            Charset={encoding.WebName}
            """
        );
    }

    public void WriteSetting(SettingElement setting)
    {
        var writer = this._writer;

        writer.WriteLine(
            $"""
            [#SETTING]
            Tempo={setting.Tempo}
            Tracks={setting.Tracks}
            VoiceDir=
            """
        );
    }

    public void WriteLyric(LyricElement lyric)
    {
        var writer = this._writer;

        int num = Interlocked.Increment(ref this._lyricCount);

        writer.WriteLine(
            $"""
            [#{num}]
            Lyric={lyric.Lyric ?? string.Empty}
            Length={lyric.Length}
            """
        );

        if (lyric.NoteNum is { } noteNum)
            writer.WriteLine($"NoteNum={noteNum}");

        if (lyric.Tempo is { } tempo)
            writer.WriteLine($"Tempo={tempo}");
    }

    public void Dispose()
    {
        if (this._disposed)
            return;

        this._disposed = true;

        this._writer.Dispose();
    }
}
