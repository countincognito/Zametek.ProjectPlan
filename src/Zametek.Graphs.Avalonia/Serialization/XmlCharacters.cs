using System.Text;
using System.Xml;

namespace Zametek.Graphs.Avalonia
{
    // The characters of a text that XML 1.0 permits. A label can have a character that XML does not permit, for example
    // U+000B: a project file can give one with an escape of JSON, and an import can give one too. The SVG writer then
    // failed with a message of the XML library, and the GraphML writer wrote &#xB;, which a reader of XML refuses. Both
    // writers give each text to this helper first.
    internal static class XmlCharacters
    {
        // The text without the characters that XML 1.0 does not permit. A pair of surrogates that make one character stays,
        // and a surrogate that is not part of a pair goes.
        public static string RemoveNotPermitted(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            int index = 0;

            while (index < text.Length
                && IsPermittedAt(text, index, out int length))
            {
                index += length;
            }

            if (index == text.Length)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            builder.Append(text, 0, index);

            while (index < text.Length)
            {
                if (IsPermittedAt(text, index, out int length))
                {
                    builder.Append(text, index, length);
                    index += length;
                }
                else
                {
                    index++;
                }
            }

            return builder.ToString();
        }

        private static bool IsPermittedAt(string text, int index, out int length)
        {
            char character = text[index];

            if (char.IsHighSurrogate(character)
                && index + 1 < text.Length
                && XmlConvert.IsXmlSurrogatePair(text[index + 1], character))
            {
                length = 2;
                return true;
            }

            // XmlConvert.IsXmlChar is true for a surrogate alone, which XML does not permit.
            length = 1;
            return !char.IsSurrogate(character)
                && XmlConvert.IsXmlChar(character);
        }
    }
}
