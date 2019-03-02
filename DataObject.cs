using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TranslateOverlay
{
    public class Rootobject
    {
        public Parsedresult[] ParsedResults { get; set; }
        public int OCRExitCode { get; set; }
        public bool IsErroredOnProcessing { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDetails { get; set; }
    }

    public class Parsedresult
    {
        public object FileParseExitCode { get; set; }
        public string ParsedText { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDetails { get; set; }
        public TextOverlay TextOverlay { get; set; }
    }

    public class TextOverlay
    {
        public List<Line> Lines { get; set; }
        public bool HasOverlay { get; set; }
        public string Message { get; set; }
    }

    public class Line
    {
        public List<Word> Words { get; set; }
        public float MaxHeight { get; set; }
        public float MinTop { get; set; }
    }

    public class Word
    {
        public string WordText { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
        public float Height { get; set; }
        public float Width { get; set; }
    }

    public class TranslateObject
    {
        public int code { get; set; }
        public string lang { get; set; }
        public List<string> text { get; set; }
    }

    public class TranslateOverlayConfig
    {
        public string translateAPIKey { get; set; }
        public string ocrAPIKey { get; set; }
        public string language { get; set; }
    }
}