using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoteCountRenderMod
{
    public enum Alignments
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        TopSpread,
        BottomSpread,
    }
    public enum Commas
    {
        Comma,
        Dot,
        Nothing,
    }
    public class Settings
    {
        public string text = "TIME:{cmiltime}/{tmiltime}.000  BPM:{bpm}  BEAT:{tsn}/{tsd}  BAR:{currbars}/{totalbars}  NOTES:{nc}/{tn}  POLYPHONY:{mplph} - {plph}";
        public Alignments textAlignment = Alignments.TopLeft;

        public int fontSize = 48;
        public string fontName = "MS UI Gothic";
        public System.Drawing.FontStyle fontStyle = System.Drawing.FontStyle.Regular;

        public Commas thousandSeparator = Commas.Comma;

        public bool saveCsv = false;
        public string csvOutput = "";
        public string csvFormat = "{nps},{plph},{bpm},{nc}";
    }
}
