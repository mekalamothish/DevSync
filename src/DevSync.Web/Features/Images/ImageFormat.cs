using System.Buffers.Binary;
using System.IO.Compression;

namespace DevSync.Web.Features.Images;

// Validate the file structure, dimensions and PNG pixel stream. Never trust a browser's MIME type or filename.
public static class ImageFormat
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public sealed record Metadata(string ContentType, int Width, int Height);
    public static Metadata? Inspect(byte[] data)
    {
        try
        {
            if (data.Length is 0 or > MaxBytes) return null;
            return data.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}) ? Png(data) : Jpeg(data);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or OverflowException or ArgumentException) { return null; }
    }
    private static bool Dimensions(int w, int h) => w > 0 && h > 0 && (long)w * h <= 20_000_000;
    private static Metadata? Png(byte[] data)
    {
        var offset = 8; var width = 0; var height = 0; var depth = 0; var channels = 0;
        var header = false; var pixels = false; var endPixels = false; var palette = false;
        using var compressed = new MemoryStream();
        while (offset <= data.Length - 12)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset,4)));
            if (length > data.Length - offset - 12) return null;
            var type = System.Text.Encoding.ASCII.GetString(data,offset+4,4);
            var chunk = data.AsSpan(offset+8,length);
            uint crc = 0xffffffff;
            foreach (var value in data.AsSpan(offset+4,length+4))
            {
                crc ^= value;
                for (var bit=0;bit<8;bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0);
            }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset+8+length,4))) return null;
            if (!header && type != "IHDR") return null;
            switch(type)
            {
                case "IHDR":
                    if (header || length != 13) return null;
                    width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(chunk[..4]));
                    height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(chunk.Slice(4,4)));
                    depth = chunk[8]; var color = chunk[9];
                    channels = color switch {0=>1,2=>3,3=>1,4=>2,6=>4,_=>0};
                    if (!Dimensions(width,height) || channels == 0 || chunk[10] != 0 || chunk[11] != 0 || chunk[12] != 0
                        || !(color is 0 or 3 ? depth is 1 or 2 or 4 or 8 || color == 0 && depth == 16 : depth is 8 or 16)) return null;
                    header=true; break;
                case "PLTE":
                    if (pixels || length is 0 or > 768 || length % 3 != 0) return null;
                    palette=true; break;
                case "IDAT":
                    if (endPixels || data[25] == 3 && !palette) return null;
                    pixels=true; compressed.Write(chunk); break;
                case "IEND":
                    if (!pixels || length != 0 || offset+12 != data.Length) return null;
                    compressed.Position=0;
                    using (var decoded = new ZLibStream(compressed,CompressionMode.Decompress,true))
                    {
                        var rowBytes = checked((width * channels * depth + 7) / 8);
                        var buffer = new byte[Math.Min(8192,rowBytes)];
                        for(var row=0;row<height;row++)
                        {
                            var filter=decoded.ReadByte(); if(filter is < 0 or > 4) return null;
                            var remaining=rowBytes;
                            while(remaining>0) { var read=decoded.Read(buffer,0,Math.Min(buffer.Length,remaining)); if(read==0) return null; remaining-=read; }
                        }
                        if(decoded.ReadByte()!=-1) return null;
                    }
                    return new("image/png",width,height);
                default:
                    if (type.Length != 4 || type.Any(c => !char.IsAsciiLetter(c)) || char.IsUpper(type[0])) return null;
                    if(pixels) endPixels=true;
                    break;
            }
            offset += length+12;
        }
        return null;
    }
    private static Metadata? Jpeg(byte[] data)
    {
        if(data.Length<4 || data[0]!=255 || data[1]!=216) return null;
        var offset=2; var width=0; var height=0; var scanned=false;
        while(offset<data.Length)
        {
            if(data[offset++]!=255) return null;
            while(offset<data.Length && data[offset]==255) offset++;
            if(offset>=data.Length) return null;
            var marker=data[offset++];
            if(marker==217) return scanned && Dimensions(width,height) && offset==data.Length ? new("image/jpeg",width,height) : null;
            if(marker is 0 or 216 || marker is >=208 and <=215 || offset+2>data.Length) return null;
            var length=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset,2));
            if(length<2 || length>data.Length-offset) return null;
            if(marker is 192 or 194)
            {
                if(width!=0 || length<8 || data[offset+2]!=8) return null;
                height=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset+3,2));
                width=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset+5,2));
                var components=data[offset+7];
                if(!Dimensions(width,height) || components is not (1 or 3) || length!=8+3*components) return null;
            }
            else if(marker is >=193 and <=207 && marker is not (196 or 200 or 204)) return null;
            offset+=length;
            if(marker==218)
            {
                if(width==0 || length<6) return null;
                scanned=true; var start=offset;
                while(offset<data.Length-1)
                {
                    if(data[offset]!=255) { offset++; continue; }
                    if(data[offset+1]==0 || data[offset+1] is >=208 and <=215) { offset+=2; continue; }
                    break;
                }
                if(offset==start) return null;
            }
        }
        return null;
    }
}
