using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;



namespace Nordiska.Modules.Reporting.PdfGeneration;

internal enum NativeStatus 
{
    Ok = 0,
    InvalidArgument = 1,
    InvalidInput = 2,
    CallbackFailed = 3,
    InternalError = 4,
    ResourceLimitExceeded = 5,
    OutOfMemory = 6,
    SigningFailed = 7
}

/*
 * PDF bytes returned back after successful generation 
 *
 * LayoutKind.Sequential = store the fields in memory in the same order they are declared
 * This is to ensure it is sent exactly in the correct shape. Otherwise theoretically the
 * documentId could be read as the PDF pointer and so on.
 *
 */
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativePdfDocumentView
{
    public byte* DocumentId;
    public byte* Bytes;
    public nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativePdfBatchView
{
    public ulong CustomerId;
    public NativePdfDocumentView* Documents;
    public nuint DocumentCount;
}



internal static unsafe partial class NativeMethods
{
    internal const string LibraryName = "nordiska_pdf_generator_c_api";


    /* Generates all requested PDFs per customer  */
    [LibraryImport(LibraryName,EntryPoint = "nordiska_pdf_v1_generate_customer_batch")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus GenerateCustomerBatch(
        byte* jsonUtf8,
        nuint jsonLength,
        delegate* unmanaged[Cdecl]<NativePdfBatchView*, nint, int> callback,
        nint userData);

    /* Fetches max payload size accepted by the native pdf generator  */
    [LibraryImport(LibraryName,EntryPoint = "nordiska_pdf_v1_max_json_bytes")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nuint MaxJsonBytes();

    /* Gets last generates diagnostic on the current running thread  */
    [LibraryImport(LibraryName,EntryPoint = "nordiska_pdf_v1_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* GetLastError();

    /* converts status into human-readable string!  */
    [LibraryImport(LibraryName, EntryPoint = "nordiska_pdf_v1_status_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* StatusName(int status);

}