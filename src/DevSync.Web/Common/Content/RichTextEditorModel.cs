namespace DevSync.Web.Common.Content;

public sealed record RichTextEditorModel(string Name,string Id,string? Value,bool ReadOnly=false,int MaxLength=60000);
