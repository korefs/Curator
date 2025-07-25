namespace TelegramStorage.Services;

public class FileChunkingService
{
    private const int CHUNK_SIZE = 40 * 1024 * 1024; // 40MB para deixar margem de segurança
    
    public static bool ShouldChunk(long fileSize)
    {
        return fileSize > CHUNK_SIZE;
    }
    
    public static int CalculateChunkCount(long fileSize)
    {
        return (int)Math.Ceiling((double)fileSize / CHUNK_SIZE);
    }
    
    public static async IAsyncEnumerable<(int index, byte[] data)> SplitStreamAsync(Stream stream)
    {
        var buffer = new byte[CHUNK_SIZE];
        int chunkIndex = 0;
        
        while (true)
        {
            var totalBytesRead = 0;
            
            // Lê um chunk completo
            while (totalBytesRead < CHUNK_SIZE)
            {
                var bytesRead = await stream.ReadAsync(
                    buffer, 
                    totalBytesRead, 
                    CHUNK_SIZE - totalBytesRead);
                
                if (bytesRead == 0)
                    break;
                
                totalBytesRead += bytesRead;
            }
            
            if (totalBytesRead == 0)
                break;
            
            // Cria array do tamanho exato dos dados lidos
            var chunkData = new byte[totalBytesRead];
            Array.Copy(buffer, 0, chunkData, 0, totalBytesRead);
            
            yield return (chunkIndex++, chunkData);
        }
    }
    
    public static async Task<Stream> ReassembleChunksAsync(IEnumerable<Stream> chunkStreams)
    {
        var combinedStream = new MemoryStream();
        
        foreach (var chunkStream in chunkStreams)
        {
            chunkStream.Position = 0;
            await chunkStream.CopyToAsync(combinedStream);
        }
        
        combinedStream.Position = 0;
        return combinedStream;
    }
}