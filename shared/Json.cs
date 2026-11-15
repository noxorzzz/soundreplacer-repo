using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SoundReplacer.Shared
{
    /// <summary>
    /// Минимальный JSON-парсер и сериализатор без внешних зависимостей.
    ///
    /// Зачем свой JSON, а не Newtonsoft/System.Text.Json:
    ///  - мод живёт внутри процесса Unity, где уже могут быть загружены чужие версии
    ///    Newtonsoft.Json (из самого Unity или других модов). Своя реализация полностью
    ///    исключает конфликт версий сборок и «Could not load file or assembly» на старте;
    ///  - приложению не нужен NuGet-пакет, а мод не тащит лишние DLL в BepInEx/plugins;
    ///  - формат конфига фиксированный и простой, полноценный JSON-фреймворк не нужен.
    ///
    /// Модель данных после парсинга: Dictionary&lt;string, object&gt; / List&lt;object&gt; /
    /// string / double / bool / null. Этого достаточно для config.json и catalog.json.
    /// </summary>
    public static class Json
    {
        private const int MaxDepth = 64;

        public static object Parse(string text)
        {
            object value;
            string error;
            if (!TryParse(text, out value, out error))
                throw new FormatException(error);
            return value;
        }

        public static bool TryParse(string text, out object value, out string error)
        {
            value = null;
            error = null;
            if (text == null)
            {
                error = "JSON: пустой ввод (null)";
                return false;
            }

            try
            {
                var parser = new Parser(text);
                value = parser.ParseValue(0);
                parser.SkipWhitespace();
                if (!parser.AtEnd)
                    throw parser.Error("лишние символы после конца документа");
                return true;
            }
            catch (FormatException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string Serialize(object value, bool pretty = true)
        {
            var sb = new StringBuilder(512);
            WriteValue(sb, value, pretty, 0);
            if (pretty) sb.Append('\n');
            return sb.ToString();
        }

        // ---------------------------------------------------------------- helpers

        public static IDictionary<string, object> AsObject(object value)
        {
            return value as IDictionary<string, object>;
        }

        public static IList<object> AsArray(object value)
        {
            return value as IList<object>;
        }

        public static object Get(IDictionary<string, object> obj, string key)
        {
            object value;
            if (obj != null && obj.TryGetValue(key, out value)) return value;
            return null;
        }

        public static string GetString(IDictionary<string, object> obj, string key, string fallback = null)
        {
            string value = Get(obj, key) as string;
            return value ?? fallback;
        }

        public static bool GetBool(IDictionary<string, object> obj, string key, bool fallback)
        {
            object value = Get(obj, key);
            if (value is bool) return (bool)value;
            if (value is string)
            {
                bool parsed;
                if (bool.TryParse((string)value, out parsed)) return parsed;
            }
            return fallback;
        }

        public static int GetInt(IDictionary<string, object> obj, string key, int fallback)
        {
            object value = Get(obj, key);
            if (value == null) return fallback;
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        // ---------------------------------------------------------------- writer

        private static void WriteValue(StringBuilder sb, object value, bool pretty, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string)
            {
                WriteString(sb, (string)value);
                return;
            }

            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is char)
            {
                WriteString(sb, value.ToString());
                return;
            }

            var dict = value as IDictionary<string, object>;
            if (dict != null)
            {
                WriteObject(sb, dict, pretty, depth);
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                WriteArray(sb, enumerable, pretty, depth);
                return;
            }

            if (IsNumber(value))
            {
                WriteNumber(sb, value);
                return;
            }

            WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static bool IsNumber(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }

        private static void WriteNumber(StringBuilder sb, object value)
        {
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                sb.Append("null");
                return;
            }

            if (number == Math.Floor(number) && Math.Abs(number) < 9.0e15)
            {
                sb.Append(((long)number).ToString(CultureInfo.InvariantCulture));
                return;
            }

            sb.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> dict, bool pretty, int depth)
        {
            if (dict.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> pair in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty)
                {
                    sb.Append('\n');
                    AppendIndent(sb, depth + 1);
                }

                WriteString(sb, pair.Key);
                sb.Append(pretty ? ": " : ":");
                WriteValue(sb, pair.Value, pretty, depth + 1);
            }

            if (pretty)
            {
                sb.Append('\n');
                AppendIndent(sb, depth);
            }

            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable items, bool pretty, int depth)
        {
            var buffer = new StringBuilder();
            int count = 0;
            foreach (object item in items)
            {
                if (count > 0) buffer.Append(',');
                if (pretty)
                {
                    buffer.Append('\n');
                    AppendIndent(buffer, depth + 1);
                }

                WriteValue(buffer, item, pretty, depth + 1);
                count++;
            }

            if (count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            sb.Append(buffer);
            if (pretty)
            {
                sb.Append('\n');
                AppendIndent(sb, depth);
            }

            sb.Append(']');
        }

        private static void AppendIndent(StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------------------------------------------------------- parser

        private sealed class Parser
        {
            private readonly string _text;
            private int _position;

            internal Parser(string text)
            {
                _text = text;
            }

            internal bool AtEnd
            {
                get { return _position >= _text.Length; }
            }

            internal FormatException Error(string message)
            {
                return new FormatException("JSON, позиция " + _position + ": " + message);
            }

            internal void SkipWhitespace()
            {
                while (_position < _text.Length)
                {
                    char c = _text[_position];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') _position++;
                    else break;
                }
            }

            internal object ParseValue(int depth)
            {
                if (depth > MaxDepth) throw Error("слишком большая вложенность");
                SkipWhitespace();
                if (AtEnd) throw Error("неожиданный конец документа");

                char c = _text[_position];
                switch (c)
                {
                    case '{': return ParseObject(depth);
                    case '[': return ParseArray(depth);
                    case '"': return ParseString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("неожиданный символ '" + c + "'");
                }
            }

            private void ExpectLiteral(string literal)
            {
                if (_position + literal.Length > _text.Length ||
                    string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
                {
                    throw Error("ожидалось '" + literal + "'");
                }
                _position += literal.Length;
            }

            private IDictionary<string, object> ParseObject(int depth)
            {
                _position++; // '{'
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (AtEnd) throw Error("незакрытый объект");
                if (_text[_position] == '}')
                {
                    _position++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd) throw Error("незакрытый объект");
                    if (_text[_position] != '"') throw Error("ожидалось имя свойства в кавычках");

                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || _text[_position] != ':') throw Error("ожидалось ':'");
                    _position++;

                    object value = ParseValue(depth + 1);
                    result[key] = value;

                    SkipWhitespace();
                    if (AtEnd) throw Error("незакрытый объект");
                    char c = _text[_position++];
                    if (c == ',') continue;
                    if (c == '}') return result;
                    throw Error("ожидалось ',' или '}'");
                }
            }

            private IList<object> ParseArray(int depth)
            {
                _position++; // '['
                var result = new List<object>();
                SkipWhitespace();
                if (AtEnd) throw Error("незакрытый массив");
                if (_text[_position] == ']')
                {
                    _position++;
                    return result;
                }

                while (true)
                {
                    object value = ParseValue(depth + 1);
                    result.Add(value);

                    SkipWhitespace();
                    if (AtEnd) throw Error("незакрытый массив");
                    char c = _text[_position++];
                    if (c == ',') continue;
                    if (c == ']') return result;
                    throw Error("ожидалось ',' или ']'");
                }
            }

            private string ParseString()
            {
                _position++; // opening quote
                var sb = new StringBuilder(32);

                while (true)
                {
                    if (AtEnd) throw Error("незакрытая строка");
                    char c = _text[_position++];

                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (AtEnd) throw Error("незакрытая escape-последовательность");
                    char esc = _text[_position++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_position + 4 > _text.Length) throw Error("некорректный \\u escape");
                            int code = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                int hex = HexValue(_text[_position++]);
                                if (hex < 0) throw Error("некорректный \\u escape");
                                code = (code << 4) | hex;
                            }
                            sb.Append((char)code);
                            break;
                        default:
                            throw Error("недопустимый escape '\\" + esc + "'");
                    }
                }
            }

            private object ParseNumber()
            {
                int start = _position;
                if (_text[_position] == '-') _position++;

                while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;

                if (_position < _text.Length && _text[_position] == '.')
                {
                    _position++;
                    while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;
                }

                if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    _position++;
                    if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-')) _position++;
                    while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;
                }

                string raw = _text.Substring(start, _position - start);
                double number;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    throw Error("некорректное число '" + raw + "'");
                return number;
            }

            private static int HexValue(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }
        }
    }
}