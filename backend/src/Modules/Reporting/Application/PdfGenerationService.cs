using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace DefaultNamespace;


/*
 *
 *extern "C"
 * int nordiska_document_generate_json
 * (const uint8_t* json_utf8,
 * size_t json_length,
 * nordiska_document_callback callback,
 * void* callback_context,
   char* error_buffer, 
   size_t error_buffer_length) 
 *
 * 
 */

/*
 *
 *  Att ge:
 *      minnesadress till json
 *      antal bytes av json
 *
 *      callback som gör ---
 *      error minnesaddress + längd
 * 
 */

public class PdfGenerationService
{

	private const string DllName = "";
    private const string EntryPoint = "";
    
	private const int ErrorSize = 1024;


	private delegate int CallbackMethod();
	



	[DllImport(""), (CallingConvention= CallingConvention.Cdecl)]
    public static string? GetVersion()
	{
		return 
	}

	//call to get the pdf 
    public static byte[] GeneratePdf(string json)
	{
		byte[] input = Encoding.UTF8.GetBytes(json);
		byte[] error = new byte[ErrorSize];
		byte[] pdf = [];




	}
    
    // Helpers

    private static byte[] ProvideJsonLength(string json) 
        => Encoding.UTF8.GetBytes(json);

    private static void ValidateJson(string json)
    {
        
    }
}

public static class Validate
{
    public static class Clazz
    {
        public static void GeneratePdf(){}
        public static void ProvideJsonLength(string json){}

    }
}