using System.Text;

namespace Synology.Services
{
    /// <summary>
    /// Splits markdown content into chunks that fit WeCom's message size limit (2048 bytes).
    /// </summary>
    public static class MarkdownMessageSplitter
    {
        public const int DefaultMaxBytes = 1900;

        public static IEnumerable<string> Split(string content, int maxBytes = DefaultMaxBytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);

            if (string.IsNullOrEmpty(content))
            {
                yield return string.Empty;
                yield break;
            }

            if (Encoding.UTF8.GetByteCount(content) <= maxBytes)
            {
                yield return content;
                yield break;
            }

            var builder = new StringBuilder();
            foreach (var rawLine in content.Split('\n'))
            {
                foreach (var line in SplitLongLine(rawLine, maxBytes))
                {
                    var addition = builder.Length == 0 ? line : $"\n{line}";
                    if (builder.Length > 0 && Encoding.UTF8.GetByteCount(builder.ToString()) + Encoding.UTF8.GetByteCount(addition) > maxBytes)
                    {
                        yield return builder.ToString();
                        builder.Clear();
                        builder.Append(line);
                    }
                    else
                    {
                        builder.Append(addition);
                    }
                }
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private static IEnumerable<string> SplitLongLine(string line, int maxBytes)
        {
            while (Encoding.UTF8.GetByteCount(line) > maxBytes)
            {
                int characterCount = 0;
                int byteCount = 0;
                foreach (var rune in line.EnumerateRunes())
                {
                    if (byteCount + rune.Utf8SequenceLength > maxBytes)
                    {
                        break;
                    }

                    byteCount += rune.Utf8SequenceLength;
                    characterCount += rune.Utf16SequenceLength;
                }

                if (characterCount == 0)
                {
                    break;
                }

                yield return line[..characterCount];
                line = line[characterCount..];
            }

            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }
}
