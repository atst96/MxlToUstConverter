namespace MxlToUstConverter.Models.UTAU;

public class LyricElement : IUtauElement
{
    /// <summary>詞</summary>
    public string Lyric { get; set; }

    /// <summary>
    /// ノートナンバー。
    /// C1の値を24とし、半音ずつ+1した値とする。
    /// </summary>
    public int? NoteNum { get; set; }

    /// <summary>音符の長さ。四分音符=480</summary>
    public long Length { get; set; }

    /// <summary>テンポ</summary>
    public decimal? Tempo { get; set; }

    private LyricElement(string lyric, int? noteNum, long length, decimal? tempo = null)
    {
        this.Lyric = lyric;
        this.NoteNum = noteNum;
        this.Length = length;
        this.Tempo = tempo;
    }

    public static LyricElement CreateNote(string lyric, int noteNum, long length, decimal? tempo = null)
        => new(lyric, noteNum, length);

    public static LyricElement CreateRest(long length, decimal? tempo = null)
        => new("R", null, length);
}
