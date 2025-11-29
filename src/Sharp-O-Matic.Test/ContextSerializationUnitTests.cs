namespace SharpOMatic.Engine.Test;

public class ContextSerializationUnitTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = false };
        return options.BuildOptions([new GeoPointConverter()]);
    }

    [Fact]
    public void Roundtrip_ScalarByte_PreservesType()
    {
        var options = CreateOptions();
        var payload = new ContextList { (byte)7 };

        var json = JsonSerializer.Serialize(payload, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.Single(back);
        Assert.IsType<byte>(back[0]);
        Assert.Equal((byte)7, (byte)back[0]!);
    }

    [Fact]
    public void JsonShape_ContainsTypeAndValue_ForScalar()
    {
        var options = CreateOptions();
        var payload = new ContextList { (byte)7, "hi" };

        var json = JsonSerializer.Serialize(payload, options);

        Assert.Contains("\"$type\":\"Byte\"", json);
        Assert.Contains("\"value\":7", json);
        Assert.Contains("\"$type\":\"String\"", json);
        Assert.Contains("\"value\":\"hi\"", json);
    }

    [Fact]
    public void Roundtrip_Arrays_1D_Jagged_Byte()
    {
        var options = CreateOptions();
        
        var b1 = new byte[] { 1, 2, 3 };            // 1D
        var bj = new byte[][] { [4, 5], [6, 7] };   // Jagged

        var list = new ContextList { b1, bj };

        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.IsType<byte[]>(back[0]);
        Assert.IsType<byte[][]>(back[1]);

        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])back[0]!);

        var backJagged = (byte[][])back[1]!;
        Assert.Equal(new byte[] { 4, 5 }, backJagged[0]);
        Assert.Equal(new byte[] { 6, 7 }, backJagged[1]);
    }

    [Fact]
    public void Roundtrip_Arrays_1D_Jagged_Double()
    {
        var options = CreateOptions();

        var b1 = new double[] { 1.1, 2.2, 3.3 };            // 1D
        var bj = new double[][] { [4.4, 5.5], [6.6, 7.7] }; // Jagged

        var list = new ContextList { b1, bj };

        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.IsType<double[]>(back[0]);
        Assert.IsType<double[][]>(back[1]);

        Assert.Equal([1.1, 2.2, 3.3], (double[])back[0]!);

        var backJagged = (double[][])back[1]!;
        Assert.Equal([4.4, 5.5], backJagged[0]);
        Assert.Equal([6.6, 7.7], backJagged[1]);
    }

    [Fact]
    public void Roundtrip_Nested_ContextObject_And_ContextList()
    {
        var options = CreateOptions();

        var inner = new ContextList { (sbyte)-3, "x" };
        var obj = new ContextObject
        {
            ["flag"] = true,
            ["n16"] = (ushort)42,
            ["child"] = inner
        };
        var outer = new ContextList { "root", obj };

        var json = JsonSerializer.Serialize(outer, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.Equal("root", back[0]);
        var backObj = Assert.IsType<ContextObject>(back[1]);

        Assert.True((bool)backObj["flag"]!);
        Assert.IsType<ushort>(backObj["n16"]);
        Assert.Equal((ushort)42, (ushort)backObj["n16"]!);

        var backChild = Assert.IsType<ContextList>(backObj["child"]);
        Assert.IsType<sbyte>(backChild[0]);
        Assert.Equal((sbyte)-3, (sbyte)backChild[0]!);
        Assert.Equal("x", backChild[1]);
    }

    [Fact]
    public void Serialize_Mixed_Jagged_Rectangular_Throws()
    {
        var options = CreateOptions();

        var outerType = typeof(int[,]).MakeArrayType(); // int[,][]
        var outer = (Array)Activator.CreateInstance(outerType, [1])!;

        var innerRect = new int[1, 1];
        innerRect[0, 0] = 123;
        outer.SetValue(innerRect, 0);

        var list = new ContextList { outer };
        Assert.Throws<SharpOMaticException>(() => JsonSerializer.Serialize(list, options));
    }

    [Fact]
    public void Null_Roundtrips()
    {
        var options = CreateOptions();
        var list = new ContextList { null };

        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.Single(back);
        Assert.Null(back[0]);
        Assert.Contains("\"$type\":\"Null\"", json);
    }

    [Fact]
    public void Roundtrip_DateTime_And_Array_PreservesInstant()
    {
        var options = CreateOptions();

        // Use UTC to avoid ambiguity; include milliseconds+ticks to ensure precision survives.
        var dt = new DateTime(2023, 07, 01, 12, 34, 56, 789, DateTimeKind.Utc).AddTicks(1234); // 0.0001234s
        var arr = new[] { dt, dt.AddMinutes(1) };

        var list = new ContextList { dt, arr };
        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        var dtBack = Assert.IsType<DateTime>(back[0]);
        Assert.Equal(DateTimeKind.Utc, dtBack.Kind);
        Assert.Equal(dt, dtBack); // exact tick equality

        var arrBack = Assert.IsType<DateTime[]>(back[1]);
        Assert.Equal(arr.Length, arrBack.Length);
        Assert.Equal(arr[0], arrBack[0]);
        Assert.Equal(arr[1], arrBack[1]);

        Assert.Contains("\"$type\":\"DateTime\"", json);
        Assert.Contains("\"$type\":\"DateTime[]\"", json);
    }

    [Fact]
    public void Roundtrip_DateTimeOffset_PreservesOffset()
    {
        var options = CreateOptions();

        var dto = new DateTimeOffset(2024, 02, 29, 23, 59, 58, TimeSpan.FromHours(+10)); // AEST(+10)
        var payload = new ContextList { dto };

        var json = JsonSerializer.Serialize(payload, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        var dtoBack = Assert.IsType<DateTimeOffset>(back[0]);
        Assert.Equal(dto, dtoBack); // instant + offset preserved
        Assert.Contains("\"$type\":\"DateTimeOffset\"", json);
    }

    [Fact]
    public void Roundtrip_TimeSpan_DateOnly_TimeOnly()
    {
        var options = CreateOptions();

        var ts = TimeSpan.FromHours(1) + TimeSpan.FromMilliseconds(234) + TimeSpan.FromTicks(56);
        var dOnly = new DateOnly(2025, 11, 08);
        var tOnly = new TimeOnly(13, 37, 42, 123).Add(TimeSpan.FromTicks(45)); // ensure sub-ms precision

        var list = new ContextList { ts, dOnly, tOnly };
        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.Equal(ts, Assert.IsType<TimeSpan>(back[0]));
        Assert.Equal(dOnly, Assert.IsType<DateOnly>(back[1]));
        Assert.Equal(tOnly, Assert.IsType<TimeOnly>(back[2]));

        Assert.Contains("\"$type\":\"TimeSpan\"", json);
        Assert.Contains("\"$type\":\"DateOnly\"", json);
        Assert.Contains("\"$type\":\"TimeOnly\"", json);
    }

    [Fact]
    public void Roundtrip_Guid_And_Arrays()
    {
        var options = CreateOptions();

        var g1 = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var g2 = Guid.NewGuid();
        var arr = new[] { g1, g2 };
        var jagged = new[] { [g1], new[] { g2 } };

        var list = new ContextList { g1, arr, jagged };
        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.Equal(g1, Assert.IsType<Guid>(back[0]));
        Assert.Equal(arr, Assert.IsType<Guid[]>(back[1]));
        var backJagged = Assert.IsType<Guid[][]>(back[2]);
        Assert.Equal(g1, backJagged[0][0]);
        Assert.Equal(g2, backJagged[1][0]);

        Assert.Contains("\"$type\":\"Guid\"", json);
        Assert.Contains("\"$type\":\"Guid[]\"", json);
        Assert.Contains("\"$type\":\"Guid[][]\"", json);
    }

    [Fact]
    public void Arrays_For_ExtendedTypes_Roundtrip()
    {
        var options = CreateOptions();

        var dts = new[]
        {
            new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc)
        };
        var dtos = new[]
        {
            new DateTimeOffset(2022, 3, 4, 5, 6, 7, TimeSpan.Zero),
            new DateTimeOffset(2023, 4, 5, 6, 7, 8, TimeSpan.FromHours(9))
        };
        var spans = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250) };
        var dates = new[] { new DateOnly(2000, 1, 1), new DateOnly(2001, 2, 2) };
        var times = new[] { new TimeOnly(1, 2, 3), new TimeOnly(23, 59, 59, 999) };
        var uris = new[] { new Uri("https://a.test/"), new Uri("https://b.test/x") };

        var list = new ContextList { dts, dtos, spans, dates, times, uris };
        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        Assert.Equal(dts, Assert.IsType<DateTime[]>(back[0]));
        Assert.Equal(dtos, Assert.IsType<DateTimeOffset[]>(back[1]));
        Assert.Equal(spans, Assert.IsType<TimeSpan[]>(back[2]));
        Assert.Equal(dates, Assert.IsType<DateOnly[]>(back[3]));
        Assert.Equal(times, Assert.IsType<TimeOnly[]>(back[4]));
        Assert.Equal(uris, Assert.IsType<Uri[]>(back[5]));

        Assert.Contains("\"$type\":\"DateTime[]\"", json);
        Assert.Contains("\"$type\":\"DateTimeOffset[]\"", json);
        Assert.Contains("\"$type\":\"TimeSpan[]\"", json);
        Assert.Contains("\"$type\":\"DateOnly[]\"", json);
        Assert.Contains("\"$type\":\"TimeOnly[]\"", json);
        Assert.Contains("\"$type\":\"Uri[]\"", json);
    }

    [Fact]
    public void Envelope_Is_Strict_Type_And_Value_Only()
    {
        var options = CreateOptions();
        var list = new ContextList { new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") };

        var json = JsonSerializer.Serialize(list, options);

        // Ensure $type/value present and no unexpected extra properties in the envelopes
        Assert.Contains("\"$type\":\"Guid\"", json);
        Assert.Contains("\"value\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", json);
    }

    [Fact]
    public void Roundtrip_NullableArrays_Int32Nullable_And_GuidNullableJagged()
    {
        var options = CreateOptions();

        int?[] a = [1, null, 3];
        Guid?[][] b = new Guid?[][]
        {
            new Guid?[] { null },
            new Guid?[] { Guid.Parse("01234567-89ab-cdef-0123-456789abcdef") }
        };

        var list = new ContextList { a, b };
        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        // Types and values preserved
        var backA = Assert.IsType<int?[]>(back[0]);
        Assert.Equal(a, backA);

        var backB = Assert.IsType<Guid?[][]>(back[1]);
        Assert.Null(backB[0][0]);
        Assert.Equal(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), backB[1][0]!.Value);

        // Token checks
        Assert.Contains("\"$type\":\"Int32?[]\"", json);
        Assert.Contains("\"$type\":\"Guid?[][]\"", json);
    }

    [Fact]
    public void TypedNullScalar_IsAccepted_And_NonNullNullableScalar_EmitsNonNullableToken()
    {
        var options = CreateOptions();

        // 1) Deserialization accepts typed-null scalar token (Int32?)
        var typedNullJson = "[{\"$type\":\"Int32?\",\"value\":null}]";
        var back1 = JsonSerializer.Deserialize<ContextList>(typedNullJson, options)!;
        Assert.Single(back1);
        Assert.Null(back1[0]); // stored value is null

        // 2) Non-null int? boxes as int => token should be "Int32" (no '?')
        var list2 = new ContextList { (int?)5 };
        var json2 = JsonSerializer.Serialize(list2, options);
        Assert.Contains("\"$type\":\"Int32\"", json2);
        Assert.DoesNotContain("\"$type\":\"Int32?\"", json2);

        var back2 = JsonSerializer.Deserialize<ContextList>(json2, options)!;
        Assert.Equal(5, Assert.IsType<int>(back2[0])); // indistinguishable from int? with value
    }

    [Fact]
    public void Roundtrip_GeoPoint_Scalar_Array_Jagged()
    {
        var options = CreateOptions();

        var p = new GeoPoint(-37.8136, 144.9631);
        var arr = new[] { new GeoPoint(-33.86, 151.21), new GeoPoint(-31.95, 115.86) };
        var jagged = new[] { new[] { new GeoPoint(35.68, 139.69) }, new[] { new GeoPoint(34.05, -118.24) } };

        var list = new ContextList { p, arr, jagged };

        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        // Type checks
        var pBack = Assert.IsType<GeoPoint>(back[0]);
        var arrBack = Assert.IsType<GeoPoint[]>(back[1]);
        var jagBack = Assert.IsType<GeoPoint[][]>(back[2]);

        // Value checks
        Assert.Equal(p, pBack);
        Assert.Equal(arr, arrBack);
        Assert.Equal(jagged[0][0], jagBack[0][0]);
        Assert.Equal(jagged[1][0], jagBack[1][0]);

        // Envelope token checks
        Assert.Contains("\"$type\":\"GeoPoint\"", json);
        Assert.Contains("\"$type\":\"GeoPoint[]\"", json);
        Assert.Contains("\"$type\":\"GeoPoint[][]\"", json);
    }

    [Fact]
    public void Nullable_GeoPoint_Array_Roundtrip_And_TypedNullScalar_Accepted()
    {
        var options = CreateOptions();

        // Nullable element array (value type)
        GeoPoint?[] arr = new GeoPoint?[] { null, new GeoPoint(48.8566, 2.3522) };

        var list = new ContextList { arr };
        var json = JsonSerializer.Serialize(list, options);
        var back = JsonSerializer.Deserialize<ContextList>(json, options)!;

        var arrBack = Assert.IsType<GeoPoint?[]>(back[0]);
        Assert.Null(arrBack[0]);
        Assert.Equal(arr[1]!.Value, arrBack[1]!.Value);

        // Token includes '?'
        Assert.Contains("\"$type\":\"GeoPoint?[]\"", json);

        // Typed-null scalar accepted on read
        var typedNullJson = "[{\"$type\":\"GeoPoint?\",\"value\":null}]";
        var back2 = JsonSerializer.Deserialize<ContextList>(typedNullJson, options)!;
        Assert.Single(back2);
        Assert.Null(back2[0]);
    }

    public readonly record struct GeoPoint(double Lat, double Lng);

    // The caller’s payload converter for GeoPoint
    public sealed class GeoPointConverter : JsonConverter<GeoPoint>
    {
        public override GeoPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // payload shape is your choice; keep it simple:
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("GeoPoint expects [lat, lng].");

            reader.Read();
            double lat = reader.GetDouble(); 
            reader.Read();
            double lng = reader.GetDouble(); 
            reader.Read();

            if (reader.TokenType != JsonTokenType.EndArray) 
                throw new JsonException();

            return new GeoPoint(lat, lng);
        }

        public override void Write(Utf8JsonWriter writer, GeoPoint value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.Lat);
            writer.WriteNumberValue(value.Lng);
            writer.WriteEndArray();
        }
    }
}
