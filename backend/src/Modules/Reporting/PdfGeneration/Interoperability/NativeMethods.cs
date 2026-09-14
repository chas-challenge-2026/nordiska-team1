using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;



namespace Nordiska.Modules.Reporting.PdfGeneration;


internal static unsafe partial class NativeMethods
{
    internal const string LibraryName = "nordiska_document_c_api";


    [LibraryImport(LibraryName, EntryPoint = "nordiska_document_generate_json")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus Generate(
        byte* jsonPointer, nuint jsonByteCount, 
        delegate* unmanaged[Cdecl]<byte*, nuint,nuint,nint, int> callback,
        nint callbackCtx,
        byte* errorMemoryBuffer,
        nuint errorMemoryBufferByteLength
    );
    
    [LibraryImport(LibraryName, EntryPoint = "nordiska_document_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint Version();
    
}