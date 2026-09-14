namespace Nordiska.Modules.Reporting.PdfGeneration;


internal enum NativeStatus : int
{
    Ok = 0,
    InvalidArgs = 1,
    InvalidInput = 2,
    CallbackFailure = 3,
    InternalError = 4
}
