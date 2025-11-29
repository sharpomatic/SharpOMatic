namespace SharpOMatic.Engine.Test;

public class ContextListPathAccessUnitTests
{
    [Fact]
    public void Get_FromList_RootIndex_Succeeds()
    {
        var list = new ContextList { 10, 20, 30 };
        Assert.Equal(20, list.Get<int>("[1]"));
    }

    [Fact]
    public void Get_FromList_PathMustStartWithIndex_Throws()
    {
        var list = new ContextList { 10, 20, 30 };
        Assert.Throws<SharpOMaticException>(() => list.Get<int>("1"));
    }

    [Fact]
    public void Get_FromList_IndexOutOfRange_Throws()
    {
        var list = new ContextList { 1 };
        Assert.Throws<SharpOMaticException>(() => list.Get<int>("[2]"));
    }

    [Fact]
    public void Get_FromList_StrictTypeMismatch_Throws()
    {
        var list = new ContextList { 1L };
        Assert.Throws<SharpOMaticException>(() => list.Get<int>("[0]"));
    }

    [Fact]
    public void TryGet_FromList_StrictTypeMismatch_ReturnsFalse()
    {
        var list = new ContextList { 1L };
        Assert.False(list.TryGet<int>("[0]", out _));
    }

    [Fact]
    public void Get_FromList_ThenProperty_Succeeds()
    {
        var u1 = new ContextObject { { "Name", "Ada" } };
        var u2 = new ContextObject { { "Name", "Grace" } };
        var users = new ContextList { u1, u2 };

        Assert.Equal("Grace", users.Get<string>("[1].Name"));
    }

    [Fact]
    public void Get_FromNestedLists_Succeeds()
    {
        var row0 = new ContextList { 1, 2 };
        var row1 = new ContextList { 3, 4 };
        var matrix = new ContextList { row0, row1 };

        Assert.Equal(4, matrix.Get<int>("[1][1]"));
    }
}

