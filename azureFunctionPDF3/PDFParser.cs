using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Net.Http;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using System.Text;

namespace azureFunctionPDF3
{
    public  class PDFParser
    {
        [FunctionName("PDFParser")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            

            string name = req.Query["name"];

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);

            string result = ParserPDF(log, name);

            
            
            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        private static string SplitPDF(CloudBlockBlob blob)
        {
            const string fileName = "";



            return "ok";

        }
        public static string PDFnameGuid = string.Empty;
 
        private static string ParserPDF(TraceWriter log, string name)
        {
            
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string azureFolderPath = "";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("carpetapdf");
            CloudBlobDirectory cloudBlobDirectory = container.GetDirectoryReference(azureFolderPath);

            ////get source file to split
            string pdfFile = name+".pdf";
            PDFnameGuid = pdfFile.ToString();
            CloudBlockBlob blockBlob1 = cloudBlobDirectory.GetBlockBlobReference(pdfFile);



            //convert to memory stream
            MemoryStream memStream = new MemoryStream();
            blockBlob1.DownloadToStreamAsync(memStream).Wait();


            LoadPdf(memStream, log);
            
            
            
             return "Ok";
        }

        public async Task<bool> DescargarStream(MemoryStream stream)
        {

            return true;
        }

        public static void LoadPdf(Stream stream, TraceWriter log)
        {
            // Create PDF Document
            log.Info("Inicio subida stream.");
            ProcesoPDF proceso = new ProcesoPDF(stream);

            log.Info("Fin subida stream.");
            //proceso.ParsearPDF();
            //proceso.ExtraerTextoFromPDF();

            proceso.ExtraerkeyPhrasesFromPDFAsync(log, PDFnameGuid);



        }

    }





}
