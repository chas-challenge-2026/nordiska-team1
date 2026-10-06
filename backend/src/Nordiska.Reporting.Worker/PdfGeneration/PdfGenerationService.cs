using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Nordiska.Modules.Reporting.PdfGeneration;

public sealed record GeneratedPdfBatch(
    ulong CustomerId,
    IReadOnlyDictionary<string, byte[]> Documents);

/*
 * Marshaller = translates data between native memory and managed (.NET) objects.
 */
internal static unsafe class NativeBatchMarshaller
{
    /* Copies and returns batched PDF bytes. */
    internal static GeneratedPdfBatch Copy(
        NativePdfBatchView batch)
    {
        ValidateBatch(batch);

        // Returned document count converted to an integer.
        int docCount = (int)batch.DocumentCount;

        // Will hold each document ID and its PDF bytes.
        var docs = new Dictionary<string, byte[]>(
            docCount,
            StringComparer.Ordinal);

        // Processes each document returned by native code.
        for (int i = 0; i < docCount; i++)
        {
            NativePdfDocumentView nativeDoc =
                batch.Documents[i];

            ValidateDocument(nativeDoc);

            // Converts the UTF-8 document ID into a C# string.
            string documentId =
                ReadDocumentId(nativeDoc);

            byte[] pdfBytes =
                CopyPdfBytes(nativeDoc);

            // Stores the document ID and copied PDF bytes.
            if (!docs.TryAdd(documentId, pdfBytes))
            {
                throw new InvalidDataException(
                    $"Native batch contains duplicate document ID: " +
                    $"{documentId}");
            }
        }

        return new GeneratedPdfBatch(
            batch.CustomerId,
            docs);
    }

    internal static void ValidateBatch(
        NativePdfBatchView batch)
    {
        if (batch.CustomerId == 0)
        {
            throw new InvalidDataException(
                "Native batch contains an invalid customer ID.");
        }

        if (batch.DocumentCount == 0)
        {
            throw new InvalidDataException(
                "Native batch contains no documents.");
        }

        if (batch.DocumentCount > (nuint)int.MaxValue)
        {
            throw new InvalidDataException(
                "Native batch contains too many documents.");
        }

        if (batch.Documents == null)
        {
            throw new InvalidDataException(
                "Native batch documents are null.");
        }
    }

    internal static void ValidateDocument(
        NativePdfDocumentView document)
    {
        if (document.DocumentId == null)
        {
            throw new InvalidDataException(
                "Native document has no document ID.");
        }

        if (document.Length > (nuint)int.MaxValue)
        {
            throw new InvalidDataException(
                "Native PDF is too large for a managed array.");
        }

        if (document.Length != 0 &&
            document.Bytes == null)
        {
            throw new InvalidDataException(
                "Native document has no PDF buffer.");
        }
    }

    private static string ReadDocumentId(
        NativePdfDocumentView document)
    {
        string? documentId =
            Marshal.PtrToStringUTF8(
                (nint)document.DocumentId);

        if (string.IsNullOrEmpty(documentId))
        {
            throw new InvalidDataException(
                "Native document ID is empty.");
        }

        return documentId;
    }

    private static byte[] CopyPdfBytes(
        NativePdfDocumentView document)
    {
        if (document.Length == 0)
        {
            return Array.Empty<byte>();
        }

        /*
         * Copies the bytes into a C# byte array.
         * we do not want to rely on the C++ pointer because
         * its memory is released when the callback returns.
         */
        return new ReadOnlySpan<byte>(
            document.Bytes,
            (int)document.Length)
            .ToArray();
    }
}

// holds the result returned by the native callback.
internal sealed class NativeCallbackState
{
    internal GeneratedPdfBatch? Result { get; set; }

    internal Exception? Failure { get; set; }

    internal bool CallbackReceived { get; set; }
}

internal static unsafe class NativeGenerateCallback
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int Receive(
        NativePdfBatchView* batch,
        nint context)
    {
        NativeCallbackState? state = null;

        try
        {
            // gets the callback state from the GCHandle token.
            state =
                GCHandle.FromIntPtr(context).Target
                    as NativeCallbackState
                ?? throw new InvalidOperationException(
                    "Native callback context was unavailable.");

            if (state.CallbackReceived)
            {
                throw new InvalidOperationException(
                    "Native callback was invoked more than once.");
            }

            state.CallbackReceived = true;

            if (batch == null)
            {
                throw new InvalidDataException(
                    "Native callback returned a null batch.");
            }

            // copies the returned native PDFs into managed memory.
            state.Result =
                NativeBatchMarshaller.Copy(*batch);

            // tells native code that the callback succeeded.
            return 0;
        }
        catch (Exception exception)
        {
            // saves the error so it can be thrown after native code returns.
            if (state is not null)
            {
                state.Failure ??= exception;
            }

            // tells native code that the callback failed.
            return 1;
        }
    }
}

public sealed class PdfGenerationService
{
    private static readonly UTF8Encoding Utf8 =
        new(false, true);

    public unsafe GeneratedPdfBatch GeneratePdfBatch(
        string json)
    {
        // Calculates how many UTF-8 bytes the JSON requires.
        int byteCount =
            ValidateInputAndGetByteCount(json);

        // Rents a reusable byte array for the JSON.
        byte[] jsonBuffer =
            ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            // Writes the JSON into the rented byte array.
            int writtenBytes = Utf8.GetBytes(
                json.AsSpan(),
                jsonBuffer.AsSpan(0, byteCount));

            // Holds the result returned by the native callback.
            var state = new NativeCallbackState();

            // Keeps the callback state alive while native code uses it.
            GCHandle context =
                GCHandle.Alloc(
                    state,
                    GCHandleType.Normal);

            try
            {
                NativeStatus status;

                /*
                 * prevents the garbage collector from moving the JSON
                 * array while native code uses its pointer.
                 */
                fixed (byte* jsonPointer = jsonBuffer)
                {
                    status =
                        NativeMethods.GenerateCustomerBatch(
                            jsonPointer,
                            (nuint)writtenBytes,
                            &NativeGenerateCallback.Receive,
                            GCHandle.ToIntPtr(context));
                }

                // Copies the native error before making another native call.
                string nativeError =
                    status == NativeStatus.Ok
                        ? string.Empty
                        : ReadNativeUtf8(
                            NativeMethods.GetLastError());

                // throws an exception that happened inside the callback.
                if (state.Failure is not null)
                {
                    string message =
                        nativeError.Length == 0
                            ? "Native callback rejected the PDF batch."
                            : nativeError;

                    throw new InvalidOperationException(
                        $"Native PDF callback failed: {message}",
                        state.Failure);
                }

                // throws when native PDF generation failed.
                if (status != NativeStatus.Ok)
                {
                    string statusName =
                        ReadNativeUtf8(
                            NativeMethods.StatusName(
                                (int)status));

                    if (statusName.Length == 0)
                    {
                        statusName = status.ToString();
                    }

                    string message =
                        nativeError.Length == 0
                            ? "No native diagnostic was provided."
                            : nativeError;

                    throw new InvalidOperationException(
                        $"Native PDF generation failed with " +
                        $"{statusName} ({(int)status}): " +
                        $"{message}");
                }

                if (!state.CallbackReceived)
                {
                    throw new InvalidOperationException(
                        "Native PDF generation completed without " +
                        "invoking the callback.");
                }

                // returns the PDFs copied by the callback.
                return state.Result
                    ?? throw new InvalidOperationException(
                        "Native PDF generation returned no result.");
            }
            finally
            {
                // releases the handle that kept the callback state alive.
                if (context.IsAllocated)
                {
                    context.Free();
                }
            }
        }
        finally
        {
            // clears the JSON data and returns the array to the pool.
            ArrayPool<byte>.Shared.Return(
                jsonBuffer,
                clearArray: true);
        }
    }

    private static int ValidateInputAndGetByteCount(
        string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        int byteCount =
            Utf8.GetByteCount(json);

        if (byteCount == 0)
        {
            throw new ArgumentException(
                "JSON must not be empty.",
                nameof(json));
        }

        nuint maximumJsonBytes =
            NativeMethods.MaxJsonBytes();

        if ((nuint)byteCount > maximumJsonBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(json),
                $"UTF-8 JSON exceeds the native limit of " +
                $"{maximumJsonBytes} bytes.");
        }

        return byteCount;
    }

    // Converts a null-terminated native UTF-8 string into a C# string.
    private static unsafe string ReadNativeUtf8(
        byte* pointer)
    {
        if (pointer == null)
        {
            return string.Empty;
        }

        return Marshal.PtrToStringUTF8((nint)pointer)
            ?? string.Empty;
    }
}