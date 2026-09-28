
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nordiska.Modules.Reporting.PdfGeneration;

/*
    Callback metoden som c++ koden behöver. 
    C++ metoden:
    const int callback_status = callback(
    reinterpret_cast<const uint8_t*>(bytes.data()), // PDF-datans adress
    bytes.size(),                                  // Antal byte
    index,                                         // Dokumentets index
    callback_context                               // Ditt GCHandle-token
);


*/

//  p/invoke koden hålls i en managed wrapper enligt Microsofts rekommendationer. 
internal static unsafe class NativeGenerateCallback
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int Recieve(NativePdfBatchView* batch, nint ctx)
    {
        ReportGenerationState? state = null;
        try
        {
            state = (ReportGenerationState)GCHandle.FromIntPtr(ctx).Target!;
            lock (state.ThreadSyncronizer)
            {
                if (batch != null)
                {
                    for (nuint i = 0; i < batch->DocumentCount; i++)
                    {
                        var doc = batch->Documents[i];
                        string docId = Marshal.PtrToStringUTF8((nint)doc.DocumentId) ?? $"doc_{i}";
                        state.Documents[docId] = new ReadOnlySpan<byte>(doc.Bytes, (int)doc.Length).ToArray();
                    }
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (state != null)
            {
                lock (state.ThreadSyncronizer)
                {
                    state.Failed = ex;
                }
            }

            return 1;
        }
    }
}


/*
    Detta är en wrapper klass som hålls kvar i minnet utanför garbage collectorns grepp.
    Den innehåller både resultatet av genereringen, och kan även innehålla mer info om hur långt i processen den är osv!
    Iom att vi håller kvar denna i minnet sen, kan vi ha runtime uppdatering till användaren potentiellt!

*/
internal sealed class ReportGenerationState
{
    internal string Status { get; set; } = "Pending";
    internal int CompletedPages { get; set; }
    internal DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    internal DateTimeOffset? FinishedAt { get; set; }
    internal object ThreadSyncronizer { get; } = new object();
    internal int MaxDocBytes { get; }
    internal readonly Dictionary<string, byte[]> Documents = new();
    internal Exception? Failed = null;
    internal ReportGenerationState(int maxDocBytes)
    {
        MaxDocBytes = maxDocBytes;
    }
}



public class PdfGenerationService
{

    private static readonly UTF8Encoding Utf8 = new(false, true); //TODO

    /*
        .NET Garbage collector (GC) flyttar inte stora objekt (large object heaps (alla objekt större än ca 85kb)).
        Vilket innebär att vi kommer löpa risk för något som kallas
        external fragmentation (specifikt LOH Fragmentation). Kortfattat
        får vi stora luckor i minnet när stora objekt rensas av GC. 

        För att förhindra detta har jag satt en gräns på antal bytes som
        ett objekt får ta upp i heap minnet. Om json datan tar upp för mycket plats
        kommer vi använda en ArrayPool som istället återanvänder samma array som sparats (i samband med arraypoolen tidigare)
        i heap minnet. Så istället för att vi måste skapa nya minnesområden för nya stora objekt, kan vi istället
        ta referensen till förgående skapade objekt och skriva det nya objektet till dess allokerade minnesområde (om minnet tillåter).

        
        Vinsten vi får är:
        1. GC behöver inte allokera stora block i minnet på nya minnesplatser hela tiden
        2. Risken för stora tomma luckor mellan objekt i LOH minskar, eftersom färre stora objekt skapas och tas bort 
    */
    private static bool ShouldRentMemory(int limit) => limit > 1000;

    public unsafe Dictionary<string, byte[]> GenerateBatch(string json)
    {
        int byteCount = Encoding.UTF8.GetByteCount(json);
        bool shouldRent = ShouldRentMemory(byteCount);
        byte[] jsonBytes = shouldRent ? ArrayPool<byte>.Shared.Rent(byteCount) : new byte[byteCount];

        int maxBytes = 64_000_000;
        var state = new ReportGenerationState(maxBytes);
        GCHandle gcCtx = default;

        try
        {
            int bufferedBytes = Encoding.UTF8.GetBytes(json, 0, json.Length, jsonBytes, 0);
            gcCtx = GCHandle.Alloc(state, GCHandleType.Normal);
            NativeStatus response;

            fixed (byte* jsonPointer = jsonBytes)
            {
                response = NativeMethods.GenerateCustomerBatch(
                    jsonPointer,
                    (nuint)bufferedBytes,
                    &NativeGenerateCallback.Recieve,
                    GCHandle.ToIntPtr(gcCtx));
            }

            lock (state.ThreadSyncronizer)
            {
                if (state.Failed != null)
                {
                    throw new InvalidOperationException("Callback failed", state.Failed);
                }

                if (response != NativeStatus.Ok)
                {
                    byte* errPtr = NativeMethods.GetLastError();
                    string msg = errPtr != null ? Marshal.PtrToStringUTF8((nint)errPtr) ?? "Unknown native error" : "Unknown native error";
                    throw new InvalidOperationException($"Error from Native ({response}): {msg}");
                }

                if (state.Documents.Count == 0)
                {
                    throw new InvalidOperationException("No PDF documents returned in batch!");
                }

                return new Dictionary<string, byte[]>(state.Documents);
            }
        }
        finally
        {
            if (gcCtx.IsAllocated) gcCtx.Free();
            if (shouldRent)
            {
                ArrayPool<byte>.Shared.Return(jsonBytes, clearArray: true);
            }
        }
    }

    public byte[] Generate(string json)
    {
        var batch = GenerateBatch(json);
        return batch.Values.First();
    }


    private static byte[] EncodeJson(string json)
    {
        // the native method needs json in bytes so first we convert it
        return Encoding.UTF8.GetBytes(json);
    }
}




/*
   "fixed" ensures that the GC doesnt move or remove the
   pointer.

   During runtime the json bytes will be saved to the heap memory as
   one "managed object". 

*/