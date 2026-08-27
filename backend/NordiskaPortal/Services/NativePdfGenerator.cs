using System.Runtime.InteropServices;

namespace NordiskaPortal.Services;

public sealed class NativePdfGenerator
{
    private const string LibraryName = "nordiska_document_c_api";
    private const int ErrorBufferLength = 1024;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DocumentCallback(IntPtr bytes, nuint length, nuint documentIndex, IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "nordiska_document_generate_json")]
    private static extern int GenerateJson(
        byte[] jsonUtf8,
        nuint jsonLength,
        DocumentCallback callback,
        IntPtr callbackContext,
        [Out] byte[] errorBuffer,
        nuint errorBufferLength);

    public byte[] Generate(string json)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(json);
        var error = new byte[ErrorBufferLength];
        byte[]? result = null;
        DocumentCallback callback = (bytes, length, documentIndex, _) =>
        {
            if (documentIndex != 0 || length > int.MaxValue)
                return 1;

            result = new byte[(int)length];
            if (length > 0)
                Marshal.Copy(bytes, result, 0, (int)length);
            return 0;
        };

        int status = GenerateJson(input, (nuint)input.Length, callback, IntPtr.Zero, error,
            (nuint)error.Length);
        if (status != 0 || result == null)
        {
            string message = System.Text.Encoding.UTF8.GetString(error).TrimEnd('\0');
            throw new InvalidOperationException(
                $"Native PDF generation failed with status {status}: {message}");
        }

        return result;
    }
}
