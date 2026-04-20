namespace SharpOMatic.Tests.Workflows;

public sealed class ActivityJsonPatchHelperUnitTests
{
    private const string WeatherLookupWithThreeStepsJson =
        """
        {
          "title": "Weather Lookup",
          "steps": [
            {
              "id": "resolve-location",
              "label": "Resolve location",
              "state": "completed"
            },
            {
              "id": "fetch-weather",
              "label": "Fetch weather",
              "state": "running"
            },
            {
              "id": "format-response",
              "label": "Format response",
              "state": "pending"
            }
          ]
        }
        """;

    [Fact]
    public void Build_patch_replaces_a_single_value()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            """
            {"title":"Weather Lookup","status":"running"}
            """,
            """
            {"title":"Weather Lookup","status":"completed"}
            """
        );

        var operation = Assert.Single(patch);
        Assert.Equal("replace", operation["op"]);
        Assert.Equal("/status", operation["path"]);
        Assert.Equal("completed", operation["value"]);
    }

    [Fact]
    public void Build_patch_adds_a_single_value()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            """
            {"title":"Weather Lookup"}
            """,
            """
            {"title":"Weather Lookup","status":"running"}
            """
        );

        var operation = Assert.Single(patch);
        Assert.Equal("add", operation["op"]);
        Assert.Equal("/status", operation["path"]);
        Assert.Equal("running", operation["value"]);
    }

    [Fact]
    public void Build_patch_removes_a_single_value()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            """
            {"title":"Weather Lookup","status":"running"}
            """,
            """
            {"title":"Weather Lookup"}
            """
        );

        var operation = Assert.Single(patch);
        Assert.Equal("remove", operation["op"]);
        Assert.Equal("/status", operation["path"]);
        Assert.False(operation.ContainsKey("value"));
    }

    [Fact]
    public void Build_patch_targets_a_single_leaf_when_a_nested_array_item_changes()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            WeatherLookupWithThreeStepsJson,
            """
            {
              "title": "Weather Lookup",
              "steps": [
                {
                  "id": "resolve-location",
                  "label": "Resolve location",
                  "state": "completed"
                },
                {
                  "id": "fetch-weather",
                  "label": "Fetch weather",
                  "state": "completed"
                },
                {
                  "id": "format-response",
                  "label": "Format response",
                  "state": "pending"
                }
              ]
            }
            """
        );

        var operation = Assert.Single(patch);
        Assert.Equal("replace", operation["op"]);
        Assert.Equal("/steps/1/state", operation["path"]);
        Assert.Equal("completed", operation["value"]);
    }

    [Fact]
    public void Build_patch_replaces_the_full_array_when_the_second_entry_is_deleted()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            WeatherLookupWithThreeStepsJson,
            """
            {
              "title": "Weather Lookup",
              "steps": [
                {
                  "id": "resolve-location",
                  "label": "Resolve location",
                  "state": "completed"
                },
                {
                  "id": "format-response",
                  "label": "Format response",
                  "state": "pending"
                }
              ]
            }
            """
        );

        var operation = Assert.Single(patch);
        Assert.Equal("replace", operation["op"]);
        Assert.Equal("/steps", operation["path"]);

        var value = Assert.IsType<ContextList>(operation["value"]);
        Assert.Equal(2, value.Count);
        Assert.Equal("resolve-location", Assert.IsType<ContextObject>(value[0])["id"]);
        Assert.Equal("format-response", Assert.IsType<ContextObject>(value[1])["id"]);
    }

    [Fact]
    public void Build_patch_emits_leaf_replacements_when_the_first_two_entries_are_reversed()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            WeatherLookupWithThreeStepsJson,
            """
            {
              "title": "Weather Lookup",
              "steps": [
                {
                  "id": "fetch-weather",
                  "label": "Fetch weather",
                  "state": "running"
                },
                {
                  "id": "resolve-location",
                  "label": "Resolve location",
                  "state": "completed"
                },
                {
                  "id": "format-response",
                  "label": "Format response",
                  "state": "pending"
                }
              ]
            }
            """
        );

        Assert.Equal(6, patch.Count);

        Dictionary<string, Dictionary<string, object?>> operationsByPath = [];
        foreach (var operation in patch)
            operationsByPath[(string)operation["path"]!] = operation;

        Assert.Equal("replace", operationsByPath["/steps/0/id"]["op"]);
        Assert.Equal("fetch-weather", operationsByPath["/steps/0/id"]["value"]);
        Assert.Equal("replace", operationsByPath["/steps/0/label"]["op"]);
        Assert.Equal("Fetch weather", operationsByPath["/steps/0/label"]["value"]);
        Assert.Equal("replace", operationsByPath["/steps/0/state"]["op"]);
        Assert.Equal("running", operationsByPath["/steps/0/state"]["value"]);

        Assert.Equal("replace", operationsByPath["/steps/1/id"]["op"]);
        Assert.Equal("resolve-location", operationsByPath["/steps/1/id"]["value"]);
        Assert.Equal("replace", operationsByPath["/steps/1/label"]["op"]);
        Assert.Equal("Resolve location", operationsByPath["/steps/1/label"]["value"]);
        Assert.Equal("replace", operationsByPath["/steps/1/state"]["op"]);
        Assert.Equal("completed", operationsByPath["/steps/1/state"]["value"]);
    }

    [Fact]
    public void Build_patch_emits_nested_changes_when_the_second_entry_is_replaced_with_a_different_object()
    {
        var patch = ActivityJsonPatchHelper.BuildPatch(
            WeatherLookupWithThreeStepsJson,
            """
            {
              "title": "Weather Lookup",
              "steps": [
                {
                  "id": "resolve-location",
                  "label": "Resolve location",
                  "state": "completed"
                },
                {
                  "code": "call-api",
                  "description": "Hit the provider",
                  "status": "queued"
                },
                {
                  "id": "format-response",
                  "label": "Format response",
                  "state": "pending"
                }
              ]
            }
            """
        );

        Assert.Equal(6, patch.Count);

        Dictionary<string, Dictionary<string, object?>> operationsByPath = [];
        foreach (var operation in patch)
            operationsByPath[(string)operation["path"]!] = operation;

        Assert.Equal("remove", operationsByPath["/steps/1/id"]["op"]);
        Assert.Equal("remove", operationsByPath["/steps/1/label"]["op"]);
        Assert.Equal("remove", operationsByPath["/steps/1/state"]["op"]);
        Assert.Equal("add", operationsByPath["/steps/1/code"]["op"]);
        Assert.Equal("call-api", operationsByPath["/steps/1/code"]["value"]);
        Assert.Equal("add", operationsByPath["/steps/1/description"]["op"]);
        Assert.Equal("Hit the provider", operationsByPath["/steps/1/description"]["value"]);
        Assert.Equal("add", operationsByPath["/steps/1/status"]["op"]);
        Assert.Equal("queued", operationsByPath["/steps/1/status"]["value"]);
    }
}
