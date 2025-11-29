namespace SharpOMatic.Engine.FastSerializer;

public ref struct FastJsonTokenizer
{
    private static readonly FullTokenKind[] s_mapKind = new FullTokenKind[65536];
    private static readonly FullTokenKind[] s_hexKind = new FullTokenKind[65536];
    private static readonly byte[] s_hexValues = new byte[65536];
    private static readonly EscapeKind[] s_escKind = new EscapeKind[65536];
    private static readonly char[] s_escChar = new char[(int)EscapeKind.u];
    private static readonly ThreadLocal<StringBuilder> s_cachedBuilder = new(() => new(4096));

    private readonly ReadOnlySpan<char> _text;
    private readonly int _length = 0;
    private readonly StringBuilder _sb;
    private FullTokenKind _tokenKind = FullTokenKind.StartOfText;
    private char _c = ' ';
    private int _index = 0;
    private int _lineIndex = 0;
    private int _lineNumber = 1;
    private int _tokenIndex = 0;

    static FastJsonTokenizer()
    {
        // All _mapKind and _hexKind entries are implicitly FullTokenKind.IllegalCharacter because
        // ithas the enumeration value of 0 and the arrays are initialized to all 0 by default

        // Skip the Byte Order Mark (BOM) and simple whitespace
        s_mapKind[0xFEFF] = FullTokenKind.Skip;
        s_mapKind[' '] = FullTokenKind.Skip;
        s_mapKind['\t'] = FullTokenKind.Skip;

        // Individual characters, mostly punctuators
        s_mapKind['\n'] = FullTokenKind.NewLine;
        s_mapKind['\r'] = FullTokenKind.CarriageReturn;
        s_mapKind[':'] = FullTokenKind.Colon;
        s_mapKind['['] = FullTokenKind.LeftSquareBracket;
        s_mapKind[']'] = FullTokenKind.RightSquareBracket;
        s_mapKind['{'] = FullTokenKind.LeftCurlyBracket;
        s_mapKind['}'] = FullTokenKind.RightCurlyBracket;
        s_mapKind['.'] = FullTokenKind.Dot;
        s_mapKind['-'] = FullTokenKind.Minus;
        s_mapKind['+'] = FullTokenKind.Plus;
        s_mapKind['"'] = FullTokenKind.DoubleQuote;
        s_mapKind[','] = FullTokenKind.Comma;

        // Letters and numbers
        for (var c = 'A'; c <= 'Z'; c++)
            s_mapKind[c] = FullTokenKind.Letter;
        for (var c = 'a'; c <= 'z'; c++)
            s_mapKind[c] = FullTokenKind.Letter;
        for (var c = '0'; c <= '9'; c++)
            s_mapKind[c] = FullTokenKind.Digit;

        // Hexadecimal characters
        for (var c = 'A'; c <= 'F'; c++)
            s_hexKind[c] = FullTokenKind.Hexadecimal;
        for (var c = 'a'; c <= 'f'; c++)
            s_hexKind[c] = FullTokenKind.Hexadecimal;
        for (var c = '0'; c <= '9'; c++)
            s_hexKind[c] = FullTokenKind.Hexadecimal;
        s_hexKind['}'] = FullTokenKind.RightCurlyBracket;

        // Hexadecimal values
        for (var c = 'A'; c <= 'F'; c++)
            s_hexValues[c] = (byte)(c + 10 - 'A');
        for (var c = 'a'; c <= 'f'; c++)
            s_hexValues[c] = (byte)(c + 10 - 'a');
        for (var c = '0'; c <= '9'; c++)
            s_hexValues[c] = (byte)(c - '0');

        // Escape characters in simple strings
        s_escKind['\"'] = EscapeKind.DoubleQuote;
        s_escKind['\\'] = EscapeKind.Backslash;
        s_escKind['/'] = EscapeKind.Slash;
        s_escKind['b'] = EscapeKind.Backspace;
        s_escKind['f'] = EscapeKind.Feed;
        s_escKind['n'] = EscapeKind.NewLine;
        s_escKind['r'] = EscapeKind.CarriageReturn;
        s_escKind['t'] = EscapeKind.Tab;
        s_escKind['u'] = EscapeKind.u;

        // Reverse lookup
        s_escChar[(int)EscapeKind.DoubleQuote] = '\"';
        s_escChar[(int)EscapeKind.Backslash] = '\\';
        s_escChar[(int)EscapeKind.Slash] = '/';
        s_escChar[(int)EscapeKind.Backspace] = '\b';
        s_escChar[(int)EscapeKind.Feed] = '\f';
        s_escChar[(int)EscapeKind.NewLine] = '\n';
        s_escChar[(int)EscapeKind.CarriageReturn] = '\r';
        s_escChar[(int)EscapeKind.Tab] = '\t';
    }

    public FastJsonTokenizer(ReadOnlySpan<char> text)
    {
        _sb = s_cachedBuilder.Value!;

        if (text.Length == 0)
            _tokenKind = FullTokenKind.EndOfText;
        else
        {
            _text = text;
            _length = _text.Length;
            _tokenKind = FullTokenKind.StartOfText;
        }
    }

    public readonly FastJsonTokenKind TokenKind => (FastJsonTokenKind)_tokenKind;
    public readonly string TokenValue => new(_text[_tokenIndex.._index]);
    public readonly string TokenString => _sb.ToString();
    public readonly int LineNumber => _lineNumber;
    public readonly int ColumnNumber => 1 + _tokenIndex - _lineIndex;
    public readonly Helpers.Location Location => new(_index, LineNumber, ColumnNumber);

    public bool Next()
    {
        while (_index < _length)
        {
            _c = _text[_index++];
            var token = s_mapKind[_c];

            switch (token)
            {
                case FullTokenKind.IllegalCharacter:
                    _tokenIndex = _index - 1;
                    throw SyntaxException.IllegalCharacterCode(Location, _c);
                case FullTokenKind.Skip:
                    break;
                case FullTokenKind.NewLine:
                    ScanNewLine();
                    break;
                case FullTokenKind.CarriageReturn:
                    ScanCarriageReturn();
                    break;
                case FullTokenKind.Colon:
                case FullTokenKind.LeftSquareBracket:
                case FullTokenKind.RightSquareBracket:
                case FullTokenKind.LeftCurlyBracket:
                case FullTokenKind.RightCurlyBracket:
                case FullTokenKind.Comma:
                    ScanPunctuator(token);
                    return true;
                case FullTokenKind.Letter:
                    ScanKeyword();
                    return true;
                case FullTokenKind.Minus:
                case FullTokenKind.Digit:
                    ScanNumber();
                    return true;
                case FullTokenKind.DoubleQuote:
                    ScanString();
                    return true;
                default:
                    _tokenIndex = _index - 1;
                    throw SyntaxException.UnrecognizedCharacterCode(Location, _c);
            }
        }

        _tokenIndex = _index;
        _tokenKind = FullTokenKind.EndOfText;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanNewLine()
    {
        _lineNumber++;
        _lineIndex = _index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanCarriageReturn()
    {
        // Skip over any optional following newline
        if ((_index < _length) && (_text[_index] == '\n'))
            _index++;

        _lineNumber++;
        _lineIndex = _index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanPunctuator(FullTokenKind token)
    {
        _tokenIndex = _index - 1;
        _tokenKind = token;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanKeyword()
    {
        _tokenIndex = _index - 1;
        while ((_index < _length) && (s_mapKind[_text[_index]] == FullTokenKind.Letter))
            _index++;

        switch (_index - _tokenIndex)
        {
            case 4:
                if ((_text[_tokenIndex + 0] == 't') && (_text[_tokenIndex + 1] == 'r') &&
                    (_text[_tokenIndex + 2] == 'u') && (_text[_tokenIndex + 3] == 'e'))
                {
                    _tokenKind = FullTokenKind.TrueValue;
                    break;
                }
                else if ((_text[_tokenIndex + 0] == 'n') && (_text[_tokenIndex + 1] == 'u') &&
                         (_text[_tokenIndex + 2] == 'l') && (_text[_tokenIndex + 3] == 'l'))
                {
                    _tokenKind = FullTokenKind.NullValue;
                    break;
                }
                else
                    throw SyntaxException.UnrecognizedKeyword(Location, TokenValue);
            case 5:
                if ((_text[_tokenIndex + 0] == 'f') && (_text[_tokenIndex + 1] == 'a') && (_text[_tokenIndex + 2] == 'l') &&
                    (_text[_tokenIndex + 3] == 's') && (_text[_tokenIndex + 4] == 'e'))
                {
                    _tokenKind = FullTokenKind.FalseValue;
                    break;
                }
                else
                    throw SyntaxException.UnrecognizedKeyword(Location, TokenValue);
            default:
                throw SyntaxException.UnrecognizedKeyword(Location, TokenValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanNumber()
    {
        _tokenIndex = _index - 1;
        if (_c == '-')
        {
            if (_index == _length)
                throw SyntaxException.UnexpectedEndOfFile(Location);

            _c = _text[_index];
            if (s_mapKind[_c] != FullTokenKind.Digit)
                throw SyntaxException.MinusMustBeFollowedByDigit(Location);

            _index++;
        }

        if (_c != '0')
        {
            // Skip over all the whole number digits
            while ((_index < _length) && (s_mapKind[_text[_index]] == FullTokenKind.Digit))
                _index++;
        }

        if (_index == _length)
        {
            _tokenKind = FullTokenKind.IntValue;
            return;
        }

        _c = _text[_index];
        if (_c == '.')
        {
            if (++_index == _length)
                throw SyntaxException.UnexpectedEndOfFile(Location);

            _c = _text[_index];
            if (s_mapKind[_c] != FullTokenKind.Digit)
                throw SyntaxException.PointMustBeFollowedByDigit(Location);

            _index++;
            while ((_index < _length) && (s_mapKind[_text[_index]] == FullTokenKind.Digit))
                _index++;

            if (_index < _length)
            {
                _c = _text[_index];
                if ((_c == 'e') || (_c == 'E'))
                    ScanFloatExponent();
            }

            _tokenKind = FullTokenKind.FloatValue;
            return;
        }
        else if ((_c == 'e') || (_c == 'E'))
            ScanFloatExponent();
        else
        {
            if (s_mapKind[_text[_index]] == FullTokenKind.Letter)
                throw SyntaxException.IntCannotBeFollowed(Location, s_mapKind[_c].ToString());

            _tokenKind = FullTokenKind.IntValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanFloatExponent()
    {
        if (++_index == _length)
            throw SyntaxException.UnexpectedEndOfFile(Location);

        _c = _text[_index++];
        if ((s_mapKind[_c] == FullTokenKind.Minus) || (s_mapKind[_c] == FullTokenKind.Plus))
        {
            if (_index == _length)
                throw SyntaxException.UnexpectedEndOfFile(Location);

            _c = _text[_index++];
        }

        if (s_mapKind[_c] != FullTokenKind.Digit)
        {
            _index--;
            throw SyntaxException.ExponentMustHaveDigit(Location);
        }

        while (_index < _length && s_mapKind[_text[_index]] == FullTokenKind.Digit)
            _index++;

        if (_index == _length)
        {
            _tokenKind = FullTokenKind.FloatValue;
            return;
        }

        switch (s_mapKind[_text[_index]])
        {
            case FullTokenKind.Dot:
            case FullTokenKind.Letter:
                throw SyntaxException.FloatCannotBeFollowed(Location, s_mapKind[_text[_index]].ToString());
        }

        _tokenKind = FullTokenKind.FloatValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanString()
    {
        _sb.Clear();

        _tokenIndex = _index;
        var copyIndex = _tokenIndex;

        while (_index < _length)
        {
            _c = _text[_index];
            if (_c == '"')
            {
                if (copyIndex < _index)
                    _sb.Append(_text[copyIndex.._index]);

                _index++;
                _tokenKind = FullTokenKind.StringValue;
                return;
            }
            else if (_c == '\\')
            {
                if (++_index == _length)
                    throw SyntaxException.UnexpectedEndOfFile(Location);

                _c = _text[_index];
                var k = s_escKind[_c];
                switch (k)
                {
                    case EscapeKind.DoubleQuote:
                    case EscapeKind.Backslash:
                    case EscapeKind.Slash:
                    case EscapeKind.Backspace:
                    case EscapeKind.Feed:
                    case EscapeKind.NewLine:
                    case EscapeKind.CarriageReturn:
                    case EscapeKind.Tab:
                        // Copy waiting characters, except the previous '\'
                        if (copyIndex < (_index - 1))
                            _sb.Append(_text[copyIndex..(_index - 1)]);

                        _sb.Append(s_escChar[(int)k]);
                        copyIndex = _index + 1;
                        break;
                    case EscapeKind.u:
                        _index++;
                        if (_index == _length)
                            throw SyntaxException.UnexpectedEndOfFile(Location);

                        _c = _text[_index];
                        if ((_index + 3) >= _length)
                        {
                            _index = _length;
                            throw SyntaxException.UnexpectedEndOfFile(Location);
                        }

                        var hexToken1 = s_hexKind[_text[_index++]];
                        var hexToken2 = s_hexKind[_text[_index++]];
                        var hexToken3 = s_hexKind[_text[_index++]];
                        var hexToken4 = s_hexKind[_text[_index]];
                        if ((hexToken1 != FullTokenKind.Hexadecimal) || (hexToken2 != FullTokenKind.Hexadecimal) ||
                            (hexToken3 != FullTokenKind.Hexadecimal) || (hexToken4 != FullTokenKind.Hexadecimal))
                        {
                            _index++;
                            throw SyntaxException.EscapeOnlyUsingHex(Location);
                        }

                        // Copy waiting characters, except the previous '\uXXXX'
                        if (copyIndex < (_index - 5))
                            _sb.Append(_text[copyIndex..(_index - 5)]);

                        _sb.Append((char)(s_hexValues[_text[_index]] |
                                            (s_hexValues[_text[_index - 1]] << 4) |
                                            (s_hexValues[_text[_index - 2]] << 8) |
                                            (s_hexValues[_text[_index - 3]] << 12)));

                        copyIndex = _index + 1;
                        break;
                    default:
                        throw SyntaxException.EscapeMustBeOneOf(Location);
                }
            }

            _index++;
        }

        throw SyntaxException.UnexpectedEndOfFile(Location);
    }

    private enum FullTokenKind : byte
    {
        IllegalCharacter = 0,
        Skip = 1,
        NewLine = 2,
        CarriageReturn = 3,
        Colon = 4,
        LeftSquareBracket = 5,
        RightSquareBracket = 6,
        LeftCurlyBracket = 7,
        RightCurlyBracket = 8,
        Comma = 9,
        Letter = 10,
        Digit = 11,
        Minus = 12,
        DoubleQuote = 13,
        Plus = 14,
        Dot = 15,
        Hexadecimal = 16,
        StartOfText = 17,
        EndOfText = 18,
        IntValue = 19,
        FloatValue = 20,
        StringValue = 21,
        TrueValue = 22,
        FalseValue = 23,
        NullValue = 24,
    }

    private enum EscapeKind : byte
    {
        Other = 0,
        DoubleQuote = 1,
        Backslash = 2,
        Slash = 3,
        Backspace = 4,
        Feed = 5,
        NewLine = 6,
        CarriageReturn = 7,
        Tab = 8,
        u = 9
    }
}
