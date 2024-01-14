﻿using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml;
using MxlToUstConverter.Models.MusicXML;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using MxlToUstConverter.Models.MusicXML.NoteElements;
using MxlToUstConverter.Models.UTAU;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MxlToUstConverter.Utils;

/// <summary>
/// MusicXMLファイルの処理に関するUtil
/// </summary>
public static class MusicXmlUtil
{
    /// <summary>テンポ未指定時に使用するテンポ(BPM)</summary>
    const double DefaultTempo = 100;

    /// <summary>XMLデータのエンコーディング(BOM無しUTF-8)</summary>
    private static readonly UTF8Encoding Utf8NonBom = new(false);

    /// <summary>XMLデシリアライズ設定</summary>
    private static readonly XmlReaderSettings _xmlReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreWhitespace = true,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    /// <summary>XMLシリアライズ設定</summary>
    private static readonly XmlWriterSettings _xmlWriterSetting = new()
    {
        NamespaceHandling = NamespaceHandling.OmitDuplicates,
        Encoding = Utf8NonBom,
    };

    /// <summary>取得対象とするMusicXMLのメディアタイプ</summary>
    private static readonly HashSet<string> TargetMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.recordare.musicxml",
        "application/vnd.recordare.musicxml+xml",
    };

    /// <summary>XMLシリアライズ時の名前空間(明示的に未設定)</summary>
    private static readonly XmlSerializerNamespaces _xmlWriterNamespaces = new([XmlQualifiedName.Empty]);

    /// <summary>
    /// MusicXMLからパート情報を列挙する。
    /// </summary>
    /// <param name="xmlStream"></param>
    /// <returns></returns>
    public static IEnumerable<(ScorePartElement Info, Part Part)> EnumerateParts(Stream xmlStream)
    {
        var score = Parse(xmlStream);
        if (score == null || score.Parts is not { Count: > 0 } scoreParts)
            // 解析できるパート情報がなければ空で返す
            return Enumerable.Empty<(ScorePartElement, Part)>();

        // パートIDをキーにしてDictionary化
        var partInfoByPartId = score.PartList?.ScorePart
            ?.Where(i => i != null && i.Id != null)
            ?.ToDictionary(i => i.Id!)
            ?? [];

        return scoreParts.Where(i => i != null)
            .Select(scorePart =>
            {
                ScorePartElement? info;

                var partId = scorePart.Id;
                if (partId == null || !partInfoByPartId.TryGetValue(partId, out info))
                {
                    info = new();
                }

                return (info, scorePart);
            })
            .ToArray();
    }

    /// <summary>
    /// MusicXMLの各パート情報を解析する。
    /// </summary>
    /// <param name="part">パート情報</param>
    /// <param name="elements">解析後情報</param>
    /// <returns></returns>
    public static bool TryParse(Part part, [NotNullWhen(true)] out IEnumerable<IUtauElement>? elements)
    {
        if (part.Measures is not { Count: > 0 } measures)
        {
            elements = null;
            return false;
        }

        var notes = new LinkedList<IUtauElement>();

        decimal? tempo = null;
        decimal division = 1m;
        //decimal unit = 1;

        //static decimal GetQuarterDuration(decimal unit, decimal tempo)
        //    => unit * TempoToTick(tempo) * 1000;

        // 4部音符あたりの時間
        // decimal timePerQuarter = GetQuarterDuration(unit, (decimal)DefaultTempo);
        Dictionary<int, int>? keys = null;
        LyricElement? tiedNote = null;

        foreach (var measure in measures)
        {
            if (measure.Attributes is { } attributes)
            {
                if (attributes.Divisions is { } _division)
                {
                    division = _division;
                    //unit = 1 / (decimal)_division;
                    //timePerQuarter = GetQuarterDuration(unit, (decimal)tempo);
                }

                var fifth = attributes.Key?.Fifths;
                if (fifth is null || fifth == 0)
                {
                    keys = null;
                }
                else
                {
                    if (!FifthCodeRelations.TryGetValue(fifth.Value, out keys))
                    {
                        Debug.WriteLine(attributes.Key!.Fifths);
                        Debugger.Break();
                    }
                }
            }

            decimal? currentTempo = null;

            foreach (var item in measure.Items ?? [])
            {
                if (item is Direction direction)
                {
                    if (direction.Sound is { } sound && direction.DirectionType?.Metronome is { } metronome)
                    {
                        tempo ??= (decimal)sound.Tempo;
                        currentTempo = (decimal)sound.Tempo;
                        //timePerQuarter = GetQuarterDuration(unit, (decimal)tempo);

                        // tempos.AddLast(new TempoInfo(true, currentTime, tempo, metronome.BeatUnit ?? "4", metronome.BeatUnitDot != null, metronome.PerMinute));
                    }

                    continue;
                }

                if (item is Note note)
                {
                    // var duration = note.Duration * timePerQuarter;
                    int noteLength = (int)(note.Duration * 480 / division);

                    try
                    {
                        if (note.Rest is not null)
                        {
                            // 休符
                            notes.AddLast(LyricElement.CreateRest(noteLength, tempo: currentTempo));
                            continue;
                        }
                        else if (note.Pitch is { } pitch)
                        {
                            var lyricText = note.Lyric?.Text ?? string.Empty;

                            var ties = note.Tie;
                            if (ties is null || ties.Count == 0)
                            {
                                // タイ以外の音符
                                notes.AddLast(LyricElement.CreateNote(lyricText, GetCode(pitch), noteLength, currentTempo));
                            }
                            else
                            {
                                if (ties.All(t => t.Type == StartStop.Start))
                                {
                                    // タイ記号の始め
                                    tiedNote = LyricElement.CreateNote(lyricText, GetCode(pitch), noteLength);
                                }
                                else if (ties.Any(t => t.Type == StartStop.Stop) && tiedNote is not null)
                                {
                                    tiedNote.Length += noteLength;

                                    if (ties.Any(t => t.Type == StartStop.Start))
                                    {
                                        // タイの途中
                                        // pass
                                    }
                                    else
                                    {
                                        // タイ記号の終わり
                                        // tiedNote.SetBreath(GetIsBreath(note));
                                        notes.AddLast(tiedNote);
                                        tiedNote = null;
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine(note);
                                    Debugger.Break();
                                }
                            }
                        }
                        else
                        {
                            // 音符じゃない場合？
                            Debug.WriteLine(note);
                            Debugger.Break();
                        }
                    }
                    finally
                    {
                        // currentTime += duration;
                    }
                }

                currentTempo = null;
            }
        }

        notes.AddFirst(new SettingElement
        {
            Tempo = tempo ?? (decimal)DefaultTempo,
            Tracks = 1,
        });

        elements = notes;
        return true;
    }

    /// <summary>
    /// MusicXMLデータをパースする。
    /// </summary>
    /// <param name="stream">対象データ</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static MusicXmlObject? Parse(Stream stream)
    {
        // 読み取り対象のストリーム
        bool isCopiedStream = false;

        // 現在位置
        long currentPosition = stream.Position;
        Span<byte> fileHeader = stackalloc byte[4];

        // ファイルヘッダーを読み込む
        if (stream.Read(fileHeader) < fileHeader.Length)
        {
            // 読み取れたデータが4バイト以下の場合
            // MusicXML関連のデータでない可能性が極めて高いので読み取りを諦める
            throw new NotSupportedException();
        }

        try
        {
            // ファイルヘッダの読み取り時に移動したファイルハンドルを元にに戻す
            // FileStream、MemoryStream等のシークできるストリームの場合はシーク位置をも度に戻す
            // NetworkStreamなどのシークできないストリームの場合は一旦MemoryStreamに移す
            if (stream.CanSeek)
            {
                // FileStream、MemoryStream等のシークできるストリームの場合
                stream.Position = currentPosition;
            }
            else
            {
                // ネットワークストリームなどのシークできないストリームの場合
                // 読み取り済みのファイルヘッダとデータをMemoryStreamにコピーする
                var baseStream = stream;
                isCopiedStream = true;
                stream = new MemoryStream();
                stream.Write(fileHeader);
                stream.CopyTo(baseStream);
                stream.Position = 0;
            }

            if (ZipUtil.IsZipHeader(fileHeader))
            {
                // ファイルヘッダがZIPの場合
                return ParseCompressedMusicXml(stream);
            }
            else
            {
                // その他の場合はテキストのXMLデータをみなす
                return ParseTextBaseMusicXml(stream);
            }
        }
        finally
        {
            // コピー済みストリームの場合は解放する
            if (isCopiedStream)
                stream.Dispose();
        }
    }

    /// <summary>
    /// 非圧縮のMusicXMLファイルをパースする。
    /// </summary>
    /// <param name="stream">対象データ</param>
    /// <returns></returns>
    private static MusicXmlObject? ParseTextBaseMusicXml(Stream stream)
        => ParseXml<MusicXmlObject>(stream);

    /// <summary>
    /// 圧縮済みMusicXMLをパースする。
    /// </summary>
    /// <param name="stream">対象データ</param>
    /// <returns></returns>
    private static MusicXmlObject? ParseCompressedMusicXml(Stream stream)
    {
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, true);

        // メタ情報(ファイル一覧)を取得
        if (TryGetArchiveFile(zipArchive, "META-INF/container.xml", out var filePath))
        {
            // MusicXMLを読み込む
            var scoreFile = zipArchive.GetEntry(filePath);
            if (scoreFile != null)
                using (var entryStream = scoreFile.Open())
                    return ParseTextBaseMusicXml(entryStream);
        }

        return null;
    }

    /// <summary>
    /// ZIPアーカイブからMusicXMLファイルを探す。
    /// </summary>
    /// <param name="zipArchive"></param>
    /// <param name="containerPath"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool TryGetArchiveFile(ZipArchive zipArchive, string containerPath, [NotNullWhen(true)] out string? path)
    {
        if (zipArchive.GetEntry(containerPath) is { } containerEntry)
        {
            // ZIPファイル内のMETA-INF/container.xmlを読み込む
            ContainerXmlObject? containerXml;
            using (var entryStream = containerEntry.Open())
                containerXml = ParseXml<ContainerXmlObject>(entryStream);

            // パスが設定されている情報に絞り込む
            var files = containerXml?.RootFiles?.RootFile?.Where(f => !string.IsNullOrEmpty(f.FullPath));
            if (files != null)
            {
                // MusicXMLファイルを探す
                // メディアタイプが設定されている場合はMusicXMLのメディアタイプに合致するものを優先する
                var file = files.FirstOrDefault(f => f.MediaType != null && TargetMediaTypes.Contains(f.MediaType))
                    ?? files.FirstOrDefault(f => f.FullPath!.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                if (file != null)
                {
                    path = file.FullPath!;
                    return true;
                }
            }
        }

        path = null;
        return false;
    }

    /// <summary>
    /// XMLデータをパースする。
    /// </summary>
    /// <typeparam name="T">デシリアライズ後の型</typeparam>
    /// <param name="stream">対象データ</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static T? ParseXml<T>(Stream stream) where T : class
    {
        using (var streamReader = new StreamReader(stream, Encoding.UTF8))
        using (var reader = XmlReader.Create(streamReader, _xmlReaderSettings))
            return XmlUtil.GetXmlSerializer<T>().Deserialize(reader) as T ?? throw new NotSupportedException();
    }

    /// <summary>
    /// パートごとのMusicXMLを作成する。
    /// </summary>
    /// <param name="part">パート情報</param>
    /// <param name="partName">パート名</param>
    /// <returns>XML文字列</returns>
    public static string ToXmlString(Part part, string partName)
        => Utf8NonBom.GetString(ToXmlData(part, partName));

    /// <summary>
    /// パートごとのMusicXMLを作成する。
    /// </summary>
    /// <param name="part">パート情報</param>
    /// <param name="partName">パート名</param>
    /// <returns>XMLデータ(バイナリ)</returns>
    public static byte[] ToXmlData(Part part, string partName)
    {
        var musicXml = new MusicXmlObject()
        {
            Version = "4.0",
            Identification = new() { Encoding = new() { Software = "Quark", EncodingDate = DateTime.Now } },
            PartList = new() { ScorePart = new() { new() { Id = "1", PartName = partName } } },
            Parts = new() { new() { Id = "1", Measures = part.Measures } }
        };

        return ToXmlData(musicXml);
    }

    /// <summary>
    /// オブジェクトをMusicXMLに変換する。
    /// </summary>
    /// <typeparam name="T">シリアライズ前の型情報</typeparam>
    /// <param name="obj">対象オブジェクト</param>
    /// <returns>シリアライズ後データ</returns>
    private static byte[] ToXmlData<T>(T obj) where T : class
    {
        const string PubId = "-//Recordare//DTD MusicXML 4.0 Partwise//EN";
        const string SysId = "http://www.musicxml.org/dtds/partwise.dtd";

        using (var ms = new MemoryStream())
        using (var writer = XmlWriter.Create(ms, _xmlWriterSetting))
        {
            writer.WriteDocType("score-partwise", PubId, SysId, null);

            XmlUtil.GetXmlSerializer<T>().Serialize(writer, obj, _xmlWriterNamespaces);
            return ms.ToArray();
        }
    }

    static int GetCode(Pitch pitch)
    {
        int timble = KeyCodeForStep[pitch.Step];

        return (int)((pitch.Octave * 12) + (pitch.Alter ?? 0) + timble + 12);
    }

    private static bool GetIsBreath(Note note)
        => note.Notations?.Articulations?.BreathMark is not null;

    private const int KeyCodeC = 0;
    private const int KeyCodeCSharp = 1;
    private const int KeyCodeD = 2;
    private const int KeyCodeDSharp = 3;
    private const int KeyCodeE = 4;
    private const int KeyCodeF = 5;
    private const int KeyCodeFSharp = 6;
    private const int KeyCodeG = 7;
    private const int KeyCodeGSharp = 8;
    private const int KeyCodeA = 9;
    private const int KeyCodeASharp = 10;
    private const int KeyCodeB = 11;

    private static Dictionary<string, int> KeyCodeForStep = new()
    {
        ["C"] = KeyCodeC,
        ["D"] = KeyCodeD,
        ["E"] = KeyCodeE,
        ["F"] = KeyCodeF,
        ["G"] = KeyCodeG,
        ["A"] = KeyCodeA,
        ["B"] = KeyCodeB,
    };

    private static Dictionary<int, Dictionary<int, int>> FifthCodeRelations = new()
    {
        [-7] = new() // ♭7つ
        {
            [KeyCodeF] = KeyCodeF - 1,
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeC] = KeyCodeC - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
            [KeyCodeG] = KeyCodeG - 1,
        },
        [-6] = new() // ♭6つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeC] = KeyCodeC - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
            [KeyCodeG] = KeyCodeG - 1,
        },
        [-5] = new() // ♭5つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
            [KeyCodeG] = KeyCodeG - 1,
        },
        [-4] = new() // ♭4つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeD] = KeyCodeD - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
        },
        [-3] = new() // ♭3つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeB] = KeyCodeB - 1,
            [KeyCodeA] = KeyCodeA - 1,
        },
        [-2] = new() // ♭2つ
        {
            [KeyCodeE] = KeyCodeE - 1,
            [KeyCodeB] = KeyCodeB - 1,
        },
        [-1] = new() // ♭1つ
        {
            [KeyCodeB] = KeyCodeB - 1,
        },
        [1] = new() // ♯1つ
        {
            [KeyCodeF] = KeyCodeF + 1,
        },
        [2] = new() // ♯2つ
        {
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeC] = KeyCodeC + 1,
        },
        [3] = new()// ♯3つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeC] = KeyCodeC + 1,
        },
        [4] = new()// ♯4つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeD] = KeyCodeD + 1,
            [KeyCodeC] = KeyCodeC + 1,
        },
        [5] = new()// ♯5つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeD] = KeyCodeD + 1,
            [KeyCodeC] = KeyCodeC + 1,
            [KeyCodeA] = KeyCodeA + 1,
        },
        [6] = new()// ♯6つ
        {
            [KeyCodeG] = KeyCodeG + 1,
            [KeyCodeF] = KeyCodeF + 1,
            [KeyCodeD] = KeyCodeD + 1,
            [KeyCodeC] = KeyCodeC + 1,
            [KeyCodeB] = KeyCodeB + 1,
            [KeyCodeA] = KeyCodeA + 1,
        },
    };
}
