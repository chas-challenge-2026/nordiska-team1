using System.IO;
using Nordiska.Modules.Reporting.PdfGeneration;

namespace Nordiska.Reporting.Worker.Tests.PdfGeneration;

public sealed class NativeBatchMarshallerTests
{
    [Fact]
    public unsafe void Copy_EmptyBatch_ThrowsInvalidDataException()
    {
        var batch = new NativePdfBatchView
        {
            CustomerId = 123,
            Documents = null,
            DocumentCount = 0
        };

        Assert.Throws<InvalidDataException>(
            () => NativeBatchMarshaller.Copy(batch));
    }

    [Fact]
    public unsafe void Copy_DocumentCountExceedsManagedLimit_ThrowsInvalidDataException()
    {
        var batch = new NativePdfBatchView
        {
            CustomerId = 123,
            Documents = null,
            DocumentCount = (nuint)int.MaxValue + (nuint)1
        };

        Assert.Throws<InvalidDataException>(
            () => NativeBatchMarshaller.Copy(batch));
    }

    [Fact]
    public unsafe void ValidateBatch_NullDocumentArray_ThrowsInvalidDataException()
    {
        var batch = new NativePdfBatchView
        {
            CustomerId = 123,
            Documents = null,
            DocumentCount = 1
        };

        Assert.Throws<InvalidDataException>(
            () => NativeBatchMarshaller.ValidateBatch(batch));
    }
}