using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML.NoteElements;

public class Tie
{
    [XmlAttribute("type")]
    public StartStop Type { get; set; }

    public override string ToString()
        => $"Type={this.Type}";
}
