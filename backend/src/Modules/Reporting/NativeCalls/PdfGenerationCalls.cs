using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nordiska.Modules.Reporting.PdfGeneration;

internal enum NativeStatus : int
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

[StructLayout(LayoutKind.Sequential)]
internal readonly unsafe struct NativePdfDocumentView
{
    public readonly byte* DocumentId;
    public readonly byte* Bytes;
    public readonly nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly unsafe struct NativePdfBatchView
{
    public readonly ulong CustomerId;
    public readonly NativePdfDocumentView* Documents;
    public readonly nuint DocumentCount;
}

internal static unsafe partial class NativeMethods
{
    internal const string LibraryName = "nordiska_pdf_generator_c_api";

    [LibraryImport(LibraryName, EntryPoint = "nordiska_pdf_v1_generate_customer_batch")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus GenerateCustomerBatch(
        byte* jsonUtf8,
        nuint jsonLength,
        delegate* unmanaged[Cdecl]<NativePdfBatchView*, nint, int> callback,
        nint userData
    );

    [LibraryImport(LibraryName, EntryPoint = "nordiska_pdf_v1_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* GetLastError();
}