using MxlToUstConverter.Models.UTAU;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using MxlToUstConverter.Models.MusicXML;
using MxlToUstConverter.Models.MusicXML.NoteElements;
using Microsoft.CodeAnalysis;

namespace MxlToUstConverter.Utils;

internal class UstUtils
{
    /// <summary>テンポ未指定時に使用するテンポ(BPM)</summary>
    const double DefaultTempo = 100;

    /// <summary>
    /// MusicXMLの各パート情報を解析する。
    /// </summary>
    /// <param name="part">パート情報</param>
    /// <param name="elements">解析後情報</param>
    /// <returns></returns>
    public static IEnumerable<IUtauElement> BuildUstElements(Part part)
    {
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

        foreach (var measure in part.Measures ?? [])
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

        return notes;
    }

    static int GetCode(Pitch pitch)
    {
        int timble = KeyCodeForStep[pitch.Step!];

        return (int)((pitch.Octave * 12) + (pitch.Alter ?? 0) + timble + 12);
    }

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
