namespace SharpOMatic.Engine.Test;

public class ContextSetUnitTests
{
    [Fact]
    public void Set_SimpleProperty_AddsThenReplaces()
    {
        var ctx = new ContextObject();
        ctx.Set("Foo", 1);
        Assert.Equal(1, ctx.Get<int>("Foo"));

        ctx.Set("Foo", 2);
        Assert.Equal(2, ctx.Get<int>("Foo"));
    }

    [Fact]
    public void Set_NestedProperty_AutoCreatesIntermediateObjects()
    {
        var ctx = new ContextObject();
        ctx.Set("User.Profile.Name", "Ada");

        Assert.Equal("Ada", ctx.Get<string>("User.Profile.Name"));
    }

    [Fact]
    public void Set_NestedProperty_ExistingWrongType_Throws()
    {
        var ctx = new ContextObject { { "User", 5 } }; // not a ContextObject
        Assert.Throws<SharpOMaticException>(() => ctx.Set("User.Name", "Ada"));
    }

    [Fact]
    public void Set_ListIndex_Final_ReplacesElement()
    {
        var list = new ContextList { 1, 2, 3 };
        var ctx = new ContextObject { { "Nums", list } };

        ctx.Set("Nums[1]", 42);
        Assert.Equal(42, ctx.Get<int>("Nums[1]"));
    }

    [Fact]
    public void Set_ListIndex_Intermediate_ListNotAutoCreated_Throws()
    {
        var ctx = new ContextObject();
        Assert.Throws<SharpOMaticException>(() => ctx.Set("Items[0]", 1));
    }

    [Fact]
    public void Set_OnList_RootIndexThenProperty_Succeeds()
    {
        var users = new ContextList { new ContextObject(), new ContextObject() };
        users.Set("[1].Name", "Grace");
        Assert.Equal("Grace", users.Get<string>("[1].Name"));
    }

    [Fact]
    public void Set_OnList_RootIndexNotStartingWithIndex_Throws()
    {
        var list = new ContextList { 1 };
        Assert.Throws<SharpOMaticException>(() => list.Set("0", 0));
    }

    [Fact]
    public void Set_OnList_ElementNotContextObject_ThenProperty_Throws()
    {
        var list = new ContextList { 1 };
        Assert.Throws<SharpOMaticException>(() => list.Set("[0].Name", "X"));
    }

    [Fact]
    public void TrySet_MissingList_ReturnsFalse_NoThrow()
    {
        var ctx = new ContextObject();
        var ok = ctx.TrySet("Items[0]", 1);
        Assert.False(ok);
    }

    [Fact]
    public void TrySet_WrongIntermediateType_ReturnsFalse()
    {
        var ctx = new ContextObject { { "User", 5 } };
        var ok = ctx.TrySet("User.Name", "Ada");
        Assert.False(ok);
    }

    [Fact]
    public void TrySet_OnList_RootIndexThenProperty_ReturnsTrue()
    {
        var users = new ContextList { new ContextObject(), new ContextObject() };
        var ok = users.TrySet("[1].Name", "Grace");
        Assert.True(ok);
        Assert.Equal("Grace", users.Get<string>("[1].Name"));
    }

    [Fact]
    public void Remove_Property_FromObject_ReturnsTrue()
    {
        var ctx = new ContextObject { { "Foo", new ContextObject { { "Bar", 1 } } } };
        Assert.True(ctx.RemovePath("Foo.Bar"));
        Assert.False(ctx.TryGet<int>("Foo.Bar", out _));
    }

    [Fact]
    public void Remove_Index_FromList_ReturnsTrueAndShifts()
    {
        var list = new ContextList { 10, 20, 30 };
        var ctx = new ContextObject { { "Nums", list } };
        Assert.True(ctx.RemovePath("Nums[1]"));
        Assert.Equal(30, ctx.Get<int>("Nums[1]"));
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void TryGetObject_And_TryGetList_Work()
    {
        var users = new ContextList { new ContextObject { { "Name", "Ada" } } };
        var ctx = new ContextObject { { "Users", users } };

        Assert.True(ctx.TryGetList("Users", out var gotList));
        Assert.Same(users, gotList);

        Assert.True(ctx.TryGetObject("Users[0]", out var user0));
        Assert.Equal("Ada", user0.Get<string>("Name"));
    }
}
