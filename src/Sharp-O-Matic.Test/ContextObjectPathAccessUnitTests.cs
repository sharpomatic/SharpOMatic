namespace SharpOMatic.Engine.Test;

public class ContextObjectPathAccessUnitTests
{
    [Fact]
    public void Get_SimpleProperty_StrictType_Succeeds()
    {
        var ctx = new ContextObject { { "FooBar", 42 } };
        var v = ctx.Get<int>("FooBar");
        Assert.Equal(42, v);
    }

    [Fact]
    public void Get_SimpleProperty_WrongType_Throws()
    {
        var ctx = new ContextObject { { "FooBar", 42L } }; // Int64 vs Int32
        var ex = Assert.Throws<SharpOMaticException>(() => ctx.Get<int>("FooBar"));
        Assert.Contains("Int64", ex.Message);
    }

    [Fact]
    public void TryGet_SimpleProperty_WrongType_ReturnsFalse()
    {
        var ctx = new ContextObject { { "FooBar", 42L } };
        Assert.False(ctx.TryGet<int>("FooBar", out _));
    }

    [Fact]
    public void Get_NullToReferenceType_Succeeds()
    {
        var ctx = new ContextObject { { "Name", null } };
        string? name = ctx.Get<string?>("Name");
        Assert.Null(name);
    }

    [Fact]
    public void Get_NullToNonNullableValueType_Throws()
    {
        var ctx = new ContextObject { { "Age", null } };
        Assert.Throws<SharpOMaticException>(() => ctx.Get<int>("Age"));
    }

    [Fact]
    public void Get_NullableValueTypeFromValue_Succeeds()
    {
        var ctx = new ContextObject { { "Qty", 5 } };
        int? qty = ctx.Get<int?>("Qty");
        Assert.True(qty.HasValue);
        Assert.Equal(5, qty.Value);
    }

    [Fact]
    public void Get_NestedContextObject_Succeeds()
    {
        var child = new ContextObject { { "Bar", "baz" } };
        var ctx = new ContextObject { { "Foo", child } };

        var s = ctx.Get<string>("Foo.Bar");
        Assert.Equal("baz", s);
    }

    [Fact]
    public void Get_MissingSegment_Throws()
    {
        var ctx = new ContextObject { { "Foo", new ContextObject() } };
        Assert.Throws<SharpOMaticException>(() => ctx.Get<string>("Foo.Bar"));
    }

    [Fact]
    public void Get_ListIndex_Succeeds()
    {
        var list = new ContextList { 10, 20, 30 };
        var ctx = new ContextObject { { "Nums", list } };

        var v = ctx.Get<int>("Nums[1]");
        Assert.Equal(20, v);
    }

    [Fact]
    public void Get_ListIndex_OutOfRange_Throws()
    {
        var list = new ContextList { 10, 20 };
        var ctx = new ContextObject { { "Nums", list } };

        Assert.Throws<SharpOMaticException>(() => ctx.Get<int>("Nums[2]"));
    }

    [Fact]
    public void Get_IndexOnNonList_Throws()
    {
        var ctx = new ContextObject { { "Nums", new ContextObject() } };
        Assert.Throws<SharpOMaticException>(() => ctx.Get<int>("Nums[0]"));
    }

    [Fact]
    public void Get_UsersList_InnerProperty_Succeeds()
    {
        var u1 = new ContextObject { { "Name", "Ada" } };
        var u2 = new ContextObject { { "Name", "Grace" } };
        var users = new ContextList { u1, u2 };
        var ctx = new ContextObject { { "Users", users } };

        var n0 = ctx.Get<string>("Users[0].Name");
        var n1 = ctx.Get<string>("Users[1].Name");

        Assert.Equal("Ada", n0);
        Assert.Equal("Grace", n1);
    }

    [Fact]
    public void Get_NestedLists_MultiIndex_Succeeds()
    {
        var row0 = new ContextList { 1, 2 };
        var row1 = new ContextList { 3, 4 };
        var matrix = new ContextList { row0, row1 };
        var ctx = new ContextObject { { "Matrix", matrix } };

        Assert.Equal(3, ctx.Get<int>("Matrix[1][0]"));
        Assert.Equal(4, ctx.Get<int>("Matrix[1][1]"));
    }
}

