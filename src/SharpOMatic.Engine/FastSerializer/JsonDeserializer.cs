namespace SharpOMatic.Engine.FastSerializer;

public ref struct FastJsonDeserializer(ReadOnlySpan<char> json)
{
    private FastJsonTokenizer _tokenizer = new(json);

    public object? Deserialize()
    {
        // Move to the first real token
        _tokenizer.Next();
        return ParseValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object? ParseValue()
    {
        return _tokenizer.TokenKind switch
        {
            FastJsonTokenKind.TrueValue => true,
            FastJsonTokenKind.FalseValue => false,
            FastJsonTokenKind.NullValue => null,
            FastJsonTokenKind.StringValue => _tokenizer.TokenString,
            FastJsonTokenKind.IntValue => int.Parse(_tokenizer.TokenValue),
            FastJsonTokenKind.FloatValue => double.Parse(_tokenizer.TokenValue),
            FastJsonTokenKind.LeftSquareBracket => ParseArray(),
            FastJsonTokenKind.LeftCurlyBracket => ParseObject(),
            _ => throw FastSerializationException.TokenNotAllowedHere(_tokenizer.Location, _tokenizer.TokenKind.ToString()),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ContextList ParseArray()
    {
        MandatoryNext();

        ContextList values = [];

        while (_tokenizer.TokenKind != FastJsonTokenKind.RightSquareBracket)
        {
            values.Add(ParseValue());
            MandatoryNext();

            if (_tokenizer.TokenKind == FastJsonTokenKind.Comma)
                MandatoryNext();
        }

        return values;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ContextObject ParseObject()
    {
        MandatoryNext();

        ContextObject values = [];

        while (_tokenizer.TokenKind != FastJsonTokenKind.RightCurlyBracket)
        {
            MandatoryToken(FastJsonTokenKind.StringValue);
            var propertyName = _tokenizer.TokenString;
            MandatoryNextToken(FastJsonTokenKind.Colon);
            MandatoryNext();
            values.Add(propertyName, ParseValue());
            MandatoryNext();

            if (_tokenizer.TokenKind == FastJsonTokenKind.Comma)
                MandatoryNext();
        }

        return values;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MandatoryNext()
    {
        if (_tokenizer.TokenKind == FastJsonTokenKind.EndOfText || !_tokenizer.Next())
            throw FastSerializationException.UnexpectedEndOfFile(_tokenizer.Location);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void MandatoryToken(FastJsonTokenKind token)
    {
        if (_tokenizer.TokenKind == FastJsonTokenKind.EndOfText)
            throw FastSerializationException.UnexpectedEndOfFile(_tokenizer.Location);

        if (_tokenizer.TokenKind != token)
            throw FastSerializationException.ExpectedTokenNotFound(_tokenizer.Location, token.ToString(), _tokenizer.TokenKind.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MandatoryNextToken(FastJsonTokenKind token)
    {
        if (_tokenizer.TokenKind == FastJsonTokenKind.EndOfText || !_tokenizer.Next())
            throw FastSerializationException.UnexpectedEndOfFile(_tokenizer.Location);

        if (_tokenizer.TokenKind != token)
            throw FastSerializationException.ExpectedTokenNotFound(_tokenizer.Location, token.ToString(), _tokenizer.TokenKind.ToString());
    }
}
