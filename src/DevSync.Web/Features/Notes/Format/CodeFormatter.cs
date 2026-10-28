using System.ComponentModel.DataAnnotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;

namespace DevSync.Web.Features.Notes.Format;

public sealed class Command
{
    [Required, StringLength(30)] public string Language { get; set; } = "";
    [Required(AllowEmptyStrings=true), StringLength(60000)] public string Code { get; set; } = "";
}
public sealed record Result(string? Code, string? Error);
public static class CodeFormatter
{
    public static Result CSharp(string code, CancellationToken ct=default)
    {
        if(code.Length>60000) return new(null,"Code blocks can contain up to 60,000 characters.");
        var tree=CSharpSyntaxTree.ParseText(code,cancellationToken:ct);
        var error=tree.GetDiagnostics(ct).FirstOrDefault(d=>d.Severity==DiagnosticSeverity.Error);
        if(error is not null)
            return new(null,$"C# syntax error on line {error.Location.GetLineSpan().StartLinePosition.Line+1}: {error.GetMessage()}");
        using var workspace=new AdhocWorkspace();
        var options=workspace.Options
            .WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine,false)
            .WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine,false);
        var root=Formatter.Format(tree.GetRoot(ct),workspace,options,cancellationToken:ct);
        var output=root.ToFullString();
        return output.Length>60000 ? new(null,"Formatted code exceeds the 60,000-character limit.") : new(output,null);
    }
}
