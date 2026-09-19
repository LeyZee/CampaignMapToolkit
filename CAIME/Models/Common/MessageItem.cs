using System;
using System.Windows;
using System.Windows.Media;

namespace CAIME
{
    public class MessageItem
    {
        public string       Time        { get; private set; }
        public string       Message     { get; private set; }
        public Brush        Colour      { get; private set; }
        public FontStyle    FontStyle   { get; private set; }
        public FontWeight   FontWeight  { get; private set; }

        public MessageItem(string message, Brush colourBrush, FontStyle style, FontWeight weight)
        {
            Time        = DateTime.Now.ToLongTimeString();
            Message     = message;
            Colour      = colourBrush;
            FontStyle   = style;
            FontWeight  = weight;
        }
    }
}
