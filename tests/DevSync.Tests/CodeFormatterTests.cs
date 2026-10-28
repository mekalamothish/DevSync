using DevSync.Web.Features.Notes.Format;
using Microsoft.CodeAnalysis.CSharp;

namespace DevSync.Tests;
public sealed class CodeFormatterTests
{
    [Fact]
    public void Csharp_formatting_preserves_tokens_comments_and_literal_values()
    {
        const string source="public class Example{ // keep this comment\npublic string Name{get;set;}=\"Alice\";public int Add(int a,int b){var result=a+b;return result;}}";
        var result=CodeFormatter.CSharp(source);
        Assert.Null(result.Error); Assert.NotNull(result.Code);
        Assert.Contains("    public string Name",result.Code); Assert.Contains("int a, int b",result.Code);
        Assert.Contains("\n        var result = a + b;",result.Code);
        Assert.Contains("// keep this comment",result.Code);
        Assert.True(SyntaxFactory.AreEquivalent(CSharpSyntaxTree.ParseText(source).GetRoot(),CSharpSyntaxTree.ParseText(result.Code).GetRoot()));
        Assert.Equal(result.Code,CodeFormatter.CSharp(result.Code).Code);
        const string literal="var text = @\"first\r\nsecond\";";
        var multiline=CodeFormatter.CSharp(literal);
        Assert.Null(multiline.Error); Assert.Contains("first\r\nsecond",multiline.Code);
    }
    [Theory]
    [InlineData("public class Broken{")]
    [InlineData("public string Name{return \"bad property\";}")]
    public void Invalid_csharp_returns_a_diagnostic_without_replacement_code(string code)
    {
        var result=CodeFormatter.CSharp(code); Assert.Null(result.Code); Assert.Contains("syntax error on line",result.Error);
    }
    [Fact]
    public void Oversized_code_is_rejected_before_parsing() => Assert.NotNull(CodeFormatter.CSharp(new string(' ',60001)).Error);
}
