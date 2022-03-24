using System.ComponentModel;
using System.Text.RegularExpressions;

namespace DotEnvGenerator
{
    public static class StringExtensions
    {
        private const char SnakeSeparator = '_';
        private const char KebabSeparator = '-';

        private const string LiteralPattern = @"^(?<prefix>[+-]?)(?<number>(?:0[xX])?[[0-9a-fA-F]+(?:_[0-9]+|[.][0-9]{1,3})*)(?<suffix>ul|lu|u|l|f|d|m?)$";
        private static readonly Regex LiteralRegex = new Regex(LiteralPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);


        private static readonly List<Type> KnownTypes = new() { typeof(bool), typeof(DateTime), typeof(Guid) };

        /// <summary>
        ///     Determines if the specified char array contains only uppercase characters.
        /// </summary>
        private static bool IsUpper(this Span<char> array)
        {
            foreach (var currentChar in array)
            {
                if (!char.IsUpper(currentChar) && currentChar is not (SnakeSeparator or KebabSeparator))
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        ///     Determines if the specified <paramref name="value"/> is a Base64 string.
        /// </summary>
        /// <param name="value">The string to validate as Base64.</param>W
        /// <returns>true if the specified <paramref name="value"/> is a Base64 string. Otherwise false.</returns>
        public static bool IsBase64String(this string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length % 4 != 0
                || value.Contains(' ') || value.Contains('\t') || value.Contains('\r') || value.Contains('\n'))
            {
                return false;
            }
            var index = value.Length - 1;
            if (value[index] == '=')
            {
                index--;
            }
            if (value[index] == '=')
            {
                index--;
            }
            for (var i = 0; i <= index; i++)
            {
                if (value[i] != '-' && value[i] != '_' && IsInvalidBase64Char(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Determines if the specified <paramref name="value"/> is an invalid Base64 character.
        /// </summary>
        /// <param name="value">The character to check.</param>
        /// <returns>true if the specified <paramref name="value"/> is an invalid character for a Base64 string. Otherwise false.</returns>
        private static bool IsInvalidBase64Char(char value)
        {
            var intValue = (int)value;
            return intValue switch
            {
                >= 48 and <= 57 => false,
                >= 65 and <= 90 => false,
                >= 97 and <= 122 => false,
                _ => intValue is not 43 and not 47
            };
        }

        /// <summary>
        ///     Converts the specified <paramref name="input"/> string into PascalCase.
        /// </summary>
        /// <param name="input">The input string that will be converted.</param>
        /// <returns>The mutated string.</returns>
        public static string ToPascalCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }
            // DO capitalize both characters on two-character acronyms.
            if (input.Length <= 2)
            {
                return input.ToUpper();
            }
            // Remove invalid characters.
            var charArray = new Span<char>(input.ToCharArray());
            // Set first letter to uppercase
            if (char.IsLower(charArray[0]))
            {
                charArray[0] = char.ToUpperInvariant(charArray[0]);
            }

            // DO capitalize only the first character of acronyms with three or more characters, except the first word.
            // DO NOT capitalize any of the characters of any acronyms, whatever their length.
            if (charArray.IsUpper())
            {
                // Replace all characters following the first to lowercase when the entire string is uppercase (ABC -> Abc)
                for (var i = 1; i < charArray.Length; i++)
                {
                    charArray[i] = char.ToLowerInvariant(charArray[i]);
                }
            }

            for (var i = 1; i < charArray.Length; i++)
            {
                var currentChar = charArray[i];
                var lastChar = charArray.Peek(i is 1 ? 1 : i - 1);
                var nextChar = charArray.Peek(i + 1);

                if (currentChar.IsDecimalDigit() && char.IsLower(nextChar))
                {
                    charArray[i + 1] = char.ToUpperInvariant(nextChar);
                }
                else if (currentChar is SnakeSeparator or KebabSeparator)
                {
                    if (char.IsLower(nextChar))
                    {
                        charArray[i + 1] = char.ToUpperInvariant(nextChar);
                    }
                    if (char.IsUpper(lastChar))
                    {
                        charArray[i - 1] = char.ToLowerInvariant(lastChar);
                    }
                }
            }
            return new string(charArray.ToArray().Where(c => c is not (SnakeSeparator or KebabSeparator)).ToArray());
        }

        /// <summary>
        ///     Converts a byte array to a hex delimited string.
        /// </summary>
        public static string ToHex(this byte[] value)
        {
            return value.Select(b => $"0x{b:X2}").Aggregate((f, s) => $"{f}, {s}");
        }
        /// <summary>
        /// Tries to parse a number from its string representation.
        /// </summary>
        /// <param name="value">The string which may contain a number value.</param>
        private static Type? TryGetNumberType(string value)
        {
            if (value.IsQuoted())
            {
                return null;
            }
            static bool IsExponentialFormat(string str) => (str.Contains("E") || str.Contains("e")) && double.TryParse(str, out double _);
            if (IsExponentialFormat(value))
            {
                return typeof(double);
            }

            var match = LiteralRegex.Match(value);
            if (match.Groups.Count <= 0)
            {
                return null;
            }
            bool isNegative = false;

            if (string.Equals(match.Groups["prefix"].ToString(), "-", StringComparison.OrdinalIgnoreCase))
            {
                isNegative = true;
            }

            var suffix = match.Groups["suffix"].ToString();
            var number = match.Groups["number"].ToString().Replace("_", string.Empty);
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                number = number.Replace(suffix, string.Empty);
            }
            if (string.IsNullOrWhiteSpace(number))
            {
                return null;
            }
            return (suffix.ToUpper()) switch
            {
                "L" when long.TryParse(number, out _) => typeof(long),
                "UL" or "LU" when !isNegative && ulong.TryParse(number, out _) => typeof(ulong),
                "U" when !isNegative && uint.TryParse(number, out _) => typeof(uint),
                "D" when double.TryParse(number, out _) => typeof(double),
                "F" when float.TryParse(number, out _) => typeof(float),
                "M" when double.TryParse(number, out _) => typeof(decimal),
                "U" when number.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) && !isNegative && uint.TryParse(number.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _) => typeof(uint),
                "UL" or "LU" when number.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) && !isNegative && ulong.TryParse(number.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _) => typeof(ulong),
                "L" when number.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) && long.TryParse(number.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _) => typeof(long),
                _ when number.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) && int.TryParse(number.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _) => typeof(int),
                _ when number.Contains('.') && double.TryParse(number, out _) => typeof(double),
                _ when number.Contains('.') && float.TryParse(number, out _) => typeof(float),
                _ when number.Contains('.') && decimal.TryParse(number, out _) => typeof(decimal),
                _ when int.TryParse(number, out _) => typeof(int),
                _ when uint.TryParse(number, out _) => typeof(uint),
                _ when long.TryParse(number, out _) => typeof(long),
                _ when ulong.TryParse(number, out _) => typeof(ulong),
                _ => null
            };
        }

        /// <summary>
        /// Determines if a string is surrounded in quotes.
        /// </summary>
        private static bool IsQuoted(this string value) => value.StartsWith("\"") && value.EndsWith("\"");

        /// <summary>
        ///     Detects what the CLR type of the specified <paramref name="value"/> is.
        /// </summary>
        /// <param name="value">The string that contains a value that is bindable to another CLR type.</param>
        /// <returns>The type that the string value can be converted to.</returns>
        /// <exception cref="TypeAccessException">When there are issues with the type.</exception>
        public static Type DetectType(this string value)
        {
            // try and parse a string to a number.
            // this allows to support integer literals
            var numberType = TryGetNumberType(value);
            if (numberType is not null)
            {
                return numberType;
            }
            // try and convert the string to a known type.
            foreach (var type in KnownTypes)
            {
                var converter = TypeDescriptor.GetConverter(type);
                if (converter.CanConvertFrom(typeof(string)))
                {
                    try
                    {
                        var newValue = converter.ConvertFromInvariantString(value);
                        if (newValue is not null)
                        {
                            return type;
                        }
                    }
                    catch
                    {
                        // Can't convert given string to this type
                    }
                }
            }
            // try and parse an array
            if (!value.IsQuoted() && value.Contains(','))
            {
                var detectedTypes = value.CommaSplit().Select(v => v.Trim().DetectType()).ToArray();
                var type = detectedTypes.FirstOrDefault();
                if (type is null)
                {
                    throw new TypeAccessException($"No type could be determined for array value '{value}'");
                }
                // if for some reason the array has multiple types treat all values as strings
                if (!detectedTypes.AreAllSame())
                {
                    return typeof(string[]);
                }
                var typeString = $"{type.FullName}[]";
                var arrayType = Type.GetType(typeString);
                return arrayType ?? throw new TypeAccessException($"Unable to form type from: {typeString}");
            }

            if (!value.IsQuoted() && value.IsBase64String())
            {
                return typeof(byte[]);
            }
            return typeof(string);
        }

        public static string QuoteString(this string value)
        {
            value = value.Trim();
            if (!value.StartsWith("\""))
            {
                value = $"\"{value}";
            }
            if (!value.EndsWith("\""))
            {
                value = $"{value}\"";
            }
            return "@" + value;
        }
        public static string[] CommaSplit(this string value)
        {
            return Regex.Split(value, ",(?=(?:[^']*'[^']*')*[^']*$)", RegexOptions.Compiled);
        }

        /// <summary>
        ///     Checks whether all items in the enumerable are same (Uses <see cref="object.Equals(object)"/> to check for
        ///     equality)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">The enumerable.</param>
        /// <returns>
        ///     Returns true if there is 0 or 1 item in the enumerable or if all items in the enumerable are same (equal to
        ///     each other) otherwise false.
        /// </returns>
        private static bool AreAllSame<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }

            using (var enumerator = enumerable.GetEnumerator())
            {
                var toCompare = default(T);
                if (enumerator.MoveNext())
                {
                    toCompare = enumerator.Current;
                }

                while (enumerator.MoveNext())
                {
                    if (toCompare != null && !toCompare.Equals(enumerator.Current))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        ///     Peeks a char at the specified <paramref name="index"/> from the provided <paramref name="array"/>
        /// </summary>
        private static char Peek(this Span<char> array, int index)
        {
            return index < array.Length && index >= 0 ? array[index] : default;
        }

        /// <summary>
        ///     Returns true if the character is a decimal digit.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        internal static bool IsDecimalDigit(this char c) => (uint)(c - '0') <= 9;
    }
}