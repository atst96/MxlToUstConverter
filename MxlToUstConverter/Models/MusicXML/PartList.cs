using System.Collections.Generic;
using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML;

public class PartList
{
    [XmlElement("score-part")]
    public List<ScorePartElement>? ScorePart { get; set; }

    public override string ToString()
        => $"ScorePart={this.ScorePart}";
}
