using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// JsonTool 单元测试
/// </summary>
public class JsonToolTests
{
    private readonly JsonTool _tool = new();

    #region FormatJson Tests

    [Fact]
    public void FormatJson_ValidJson_ReturnsFormatted()
    {
        var json = """{"name":"test","value":123}""";
        var result = _tool.FormatJson(json);

        result.Should().Contain("\"name\"");
        result.Should().Contain("\"test\"");
        result.Should().Contain("\n"); // 格式化后应有换行
    }

    [Fact]
    public void FormatJson_EmptyInput_ReturnsError()
    {
        var result = _tool.FormatJson("");
        result.Should().Contain("错误");
    }

    [Fact]
    public void FormatJson_InvalidJson_ReturnsError()
    {
        var result = _tool.FormatJson("{invalid}");
        result.Should().Contain("JSON 解析错误");
    }

    [Fact]
    public void FormatJson_NullJson_ReturnsNull()
    {
        var result = _tool.FormatJson("null");
        result.Should().Be("null");
    }

    #endregion

    #region CompactJson Tests

    [Fact]
    public void CompactJson_ValidJson_ReturnsCompacted()
    {
        var json = """
            {
                "name": "test",
                "value": 123
            }
            """;
        var result = _tool.CompactJson(json);

        result.Should().NotContain("\n");
        result.Should().Contain("\"name\":\"test\"");
    }

    [Fact]
    public void CompactJson_EmptyInput_ReturnsError()
    {
        var result = _tool.CompactJson("");
        result.Should().Contain("错误");
    }

    [Fact]
    public void CompactJson_InvalidJson_ReturnsError()
    {
        var result = _tool.CompactJson("{invalid}");
        result.Should().Contain("JSON 解析错误");
    }

    #endregion

    #region ValidateJson Tests

    [Fact]
    public void ValidateJson_ValidObject_ReturnsValid()
    {
        var result = _tool.ValidateJson("""{"key": "value"}""");
        result.Should().Contain("有效的 JSON");
        result.Should().Contain("对象");
    }

    [Fact]
    public void ValidateJson_ValidArray_ReturnsValid()
    {
        var result = _tool.ValidateJson("""[1, 2, 3]""");
        result.Should().Contain("有效的 JSON");
        result.Should().Contain("数组");
        result.Should().Contain("3 个元素");
    }

    [Fact]
    public void ValidateJson_ValidValue_ReturnsValid()
    {
        var result = _tool.ValidateJson("123");
        result.Should().Contain("有效的 JSON");
        result.Should().Contain("值");
    }

    [Fact]
    public void ValidateJson_Null_ReturnsValid()
    {
        var result = _tool.ValidateJson("null");
        result.Should().Contain("有效的 JSON");
        result.Should().Contain("null");
    }

    [Fact]
    public void ValidateJson_EmptyInput_ReturnsInvalid()
    {
        var result = _tool.ValidateJson("");
        result.Should().Contain("无效");
    }

    [Fact]
    public void ValidateJson_InvalidJson_ReturnsInvalid()
    {
        var result = _tool.ValidateJson("{invalid}");
        result.Should().Contain("无效的 JSON");
    }

    #endregion

    #region ExtractJsonPath Tests

    [Fact]
    public void ExtractJsonPath_SimpleKey_ReturnsValue()
    {
        var json = """{"name": "test"}""";
        var result = _tool.ExtractJsonPath(json, "name");
        result.Should().Contain("test");
    }

    [Fact]
    public void ExtractJsonPath_NestedKey_ReturnsValue()
    {
        var json = """{"data": {"user": {"name": "Alice"}}}""";
        var result = _tool.ExtractJsonPath(json, "data.user.name");
        result.Should().Contain("Alice");
    }

    [Fact]
    public void ExtractJsonPath_ArrayIndex_ReturnsValue()
    {
        var json = """{"items": [{"id": 1}, {"id": 2}]}""";
        var result = _tool.ExtractJsonPath(json, "items[0].id");
        result.Should().Contain("1");
    }

    [Fact]
    public void ExtractJsonPath_EmptyJson_ReturnsError()
    {
        var result = _tool.ExtractJsonPath("", "name");
        result.Should().Contain("错误");
    }

    [Fact]
    public void ExtractJsonPath_EmptyPath_ReturnsError()
    {
        var result = _tool.ExtractJsonPath("""{"name": "test"}""", "");
        result.Should().Contain("错误");
    }

    [Fact]
    public void ExtractJsonPath_NonExistentPath_ReturnsNotFound()
    {
        var json = """{"name": "test"}""";
        var result = _tool.ExtractJsonPath(json, "nonexistent.path");
        result.Should().Contain("不存在");
    }

    [Fact]
    public void ExtractJsonPath_NullJson_ReturnsNull()
    {
        var result = _tool.ExtractJsonPath("null", "name");
        result.Should().Contain("null");
    }

    #endregion
}
