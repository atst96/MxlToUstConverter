﻿using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML.NoteElements;

public class Lyric
{
    [XmlElement("syllabic")]
    public Syllabic Syllabic { get; set; }

    [XmlElement("text")]
    public string? Text { get; set; }

    public override string ToString()
        => $"Syllabic={this.Syllabic}, Text={this.Text}";
}
