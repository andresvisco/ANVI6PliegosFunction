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
using PdfSharp;
using System.Xml.Linq;
using System.Collections.Generic;
using static PdfSharp.Pdf.PdfDictionary;
using PdfSharp.Pdf.Content.Objects;
using System.Text;
using System.Linq;
using PdfSharp.Pdf.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Host;
using System.Threading;
using System.Data.SqlClient;
using FunctionPDF3;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using System.Diagnostics;

namespace azureFunctionPDF3
{
    public class ProcesoPDF
    {
        //private PdfReader sourcePDF { get; set; }

        public PdfDocument doc { get; set; }
        public Stream strOriginal { get; set; }

        private static SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings"));

        
        public ProcesoPDF()
        { }

        public ProcesoPDF(Stream streamPDF)
        {
            doc = PdfReader.Open(streamPDF, PdfDocumentOpenMode.ReadOnly);
            strOriginal = streamPDF;
        }



        public string ParsearPDF()
        {

            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string azureFolderPath = "";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("carpetapdf");
            CloudBlobDirectory cloudBlobDirectory = container.GetDirectoryReference(azureFolderPath);

            ////get source file to split
            string pdfFile = "sample.pdf";
            CloudBlockBlob blockBlob1 = cloudBlobDirectory.GetBlockBlobReference(pdfFile);

            //convert to memory stream
            MemoryStream memStream = new MemoryStream();
            blockBlob1.DownloadToStreamAsync(memStream).Wait();


           PdfDocument outputDocument = new PdfDocument();

            //get source file to split
            for (var i = 0; i <= doc.Pages.Count - 1; i++)
            {
                //define output file for azure
                string outputPDFFile = "output" + i + ".pdf";
                CloudBlockBlob outputblockBlob = cloudBlobDirectory.GetBlockBlobReference(outputPDFFile);

                //create new document
                MemoryStream newPDFStream = new MemoryStream();
                
                PdfDocument pdfDoc = PdfReader.Open(strOriginal, PdfDocumentOpenMode.Import);
                
                for (var j = doc.Pages.Count - 1; j >= 0; j--)
                {
                    if (j != i)
                    {
                        pdfDoc.Pages.RemoveAt(j);
                        
                    }
                }

                

                byte[] pdfData;
                using (var ms = new MemoryStream())
                {
                    //doc.Save(ms);
                    pdfDoc.Save(ms);
                    pdfData = ms.ToArray();

                    

                    //outputblockBlob.UploadFromStreamAsync(ms);
                    outputblockBlob.UploadFromByteArrayAsync(pdfData, 0, pdfData.Length);
                }

                
            }

            return "Ok";
        }



        public async Task<string> ExtraerkeyPhrasesFromPDFAsync(TraceWriter log, string PDFNameGuid)
        {
            string Horainicio = string.Format("{0:HH:mm:ss tt}", DateTime.Now);
            log.Info(Horainicio.ToString());

            Stopwatch sw = new Stopwatch();
            sw.Start();


            var result = new StringBuilder();
            string keyPhrasesGoogle = string.Empty;
            string classifyTextGoogle = string.Empty;
            string textoPagina;
            string keyPhrases = string.Empty;
            int numeroPagina = 1;
            string separador = ",";
            string jsonRegistro;
            int cantidadLlamadasAPI = 0;
            var TiempoEspera = Environment.GetEnvironmentVariable("CustomTrheadSleep");
            var TiempoEsperaInt = Convert.ToInt32(TiempoEspera);
            log.Info("Tiempo Espera: " + TiempoEsperaInt.ToString());

            


            foreach (PdfSharp.Pdf.PdfPage page in doc.Pages)
            {

                result.Length = 0;

                ExtractText(ContentReader.ReadContent(page), result);
                              

                textoPagina = AnalizadorTexto.RemoveSpecialCharacters(result.ToString());
                Dictionary<int, string> subPaginas = splitPagina(textoPagina);

                foreach (KeyValuePair<int, string> entry in subPaginas)
                {
                    try
                    {
                        if (cantidadLlamadasAPI == 3)
                        {
                            Thread.Sleep(TiempoEsperaInt);
                            cantidadLlamadasAPI = 0;
                        }
                        else
                        {
                            cantidadLlamadasAPI = cantidadLlamadasAPI + 1;
                            MultiLanguageBatchInput classifyTextGoogleML = new MultiLanguageBatchInput(
                            new List<MultiLanguageInput>()
                            {
                          new MultiLanguageInput("es", numeroPagina.ToString(), entry.Value.ToString())
                            });
                            var texto = classifyTextGoogleML.Documents[0].Text.ToString();

                            //try { classifyTextGoogle = AnalizadorTexto.Clasificador(texto.Replace(".", ""), log);}
                            //catch (Exception ex) { log.Info("Error en API Google Classify: " + ex.Message.ToString()); }

                            try { keyPhrasesGoogle = AnalizadorTexto.ProcesarGoogle(texto.Replace(".", ""), log); }
                            catch (Exception ex) { log.Info("Error en API Google Process: " + ex.Message.ToString()); }
                            //TODO: DEJAR SOLO ANALISIS ENTIDADES EN ESPAÑOLS
                            //try { keyPhrases = AnalizadorTexto.AnalizarTextoJson(entry.Value.ToString(), numeroPagina.ToString(), log); }
                            //catch (Exception ex) { log.Info("Error en API AnalizarTextoJson: " + ex.Message.ToString()); }


                            
                            Thread.Sleep(TiempoEsperaInt);

                        }
                        //keyPhrases = AnalizadorTexto.AnalizarTexto(entry.Value.ToString(), numeroPagina.ToString(), log); //este se usa cuando el resultado va a una DB
                    }
                    catch (Exception ex)
                    {
                        log.Info(ex.Message);

                    }
                    var idPDF = PDFNameGuid.ToString() + "-" + doc.Guid.ToString();
                    keyPhrases = "";
                    classifyTextGoogle = "";
                    Pliego pliego = new Pliego(idPDF, numeroPagina, entry.Key, keyPhrases, keyPhrasesGoogle ,classifyTextGoogle);
                    await InsertarKeyPhrase(pliego, log);
                    //GuardarClasificador(pliego, log);



                    log.Info("Se procesó el bloque : " + entry.Key.ToString() + " de la página " + numeroPagina.ToString() + ". Total de páginas: " + doc.PageCount.ToString() );
                }
                
                numeroPagina++;
            }

            //InsertarEnTxt(result1.ToString());
            EjecutarSPEliminarVacios(log);
            string HoraFin = string.Format("{0:HH:mm:ss tt}", DateTime.Now);
            sw.Stop();

            var tiempoPasado = sw.Elapsed;
            log.Info(tiempoPasado.ToString());
            

            return result.ToString();
        }

        private bool EjecutarSPEliminarVacios(TraceWriter log)
        {
            try
            {
                SqlConnection sqlConnection1 = new SqlConnection("Server = tcp:anvi6sqlserver.database.windows.net,1433; Initial Catalog = ANVI6PliegosProductoSQL; Persist Security Info = False; User ID =andres.visco; Password =2363Andy; MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 30");
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand("TEICPLliegos_Classify_EliminarVacios");
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Cadena", "[Empty]");
                cmd.Connection = sqlConnection1;

                sqlConnection1.Open();
                cmd.ExecuteNonQuery();
                sqlConnection1.Close();

                return true;
            }
            catch (Exception ex)
            {
                log.Info(ex.Message.ToString());
                return false;
            }
        }

        private bool GuardarClasificador(Pliego pliego, TraceWriter log)
        {
            try
            {
                var IdPdf = pliego.IdPdf.ToString();
                var Classify = pliego.KeyGoogleClassify;
                //SqlConnection sqlConnection1 = new SqlConnection("Server=tcp:techintpliegossqlserver.database.windows.net,1433;Initial Catalog=TECHINTPliegosSqlDB;;Persist Security Info=False;User ID=andres.visco;Password=2363Andy;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
                SqlConnection sqlConnection1 = new SqlConnection("Server = tcp:anvi6sqlserver.database.windows.net,1433; Initial Catalog = ANVI6PliegosProductoSQL; Persist Security Info = False; User ID =andres.visco; Password =2363Andy; MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 30");
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandText = "INSERT INTO dbo.PliegosClassifyText(IdPdf, Categorias) VALUES('" + IdPdf + "','" + Classify.ToString() + "')";
                cmd.Connection = sqlConnection1;

                sqlConnection1.Open();
                cmd.ExecuteNonQuery();
                sqlConnection1.Close();

                return true;
            }
            catch (Exception ex)
            {
                log.Info(ex.Message.ToString());
                return false;
            }

        }


        private Dictionary<int, string> splitPagina(string texto)
        {
            int partLength = 10000;
            //string texto = "Silver badges are awarded for longer term goals. Silver badges are uncommon.";
            string[] words = texto.Split(' ');
            var parts = new Dictionary<int, string>();
            string part = string.Empty;
            int partCounter = 0;
            foreach (var word in words)
            {
                if (part.Length + word.Length < partLength)
                {
                    part += string.IsNullOrEmpty(part) ? word : " " + word;
                }
                else
                {
                    parts.Add(partCounter, part);
                    part = word;
                    partCounter++;
                }
            }
            parts.Add(partCounter, part);

            return parts;
            //foreach (var item in parts)
            //{
            //    Console.WriteLine("Part {0} (length = {2}): {1}", item.Key, item.Value, item.Value.Length);
            //}
            //Console.ReadLine();
        }

        private async Task<string> InsertarKeyPhrase(Pliego pliego, TraceWriter log)
        {
            try
            {
                var IdPdf = pliego.IdPdf;
                var Pag = pliego.Pagina;
                var Bloque = pliego.Bloque;
                var Keys = pliego.KeyPhrases;
                var KeysGoogle = pliego.KeyGoogle;
                System.Data.SqlClient.SqlConnection sqlConnection1 = new System.Data.SqlClient.SqlConnection("Server=tcp:anvi6sqlserver.database.windows.net,1433;Initial Catalog=ANVI6PliegosProductoSQL;Persist Security Info=False;User ID=andres.visco;Password=2363Andy;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30");

                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandText = "INSERT INTO dbo.Pliegos(IdPdf, pag, bloque, Keys, KeysGoogleEntidades) VALUES('" + IdPdf + "', " + Pag + ", " + Bloque + ", '" + Keys + "', '" + KeysGoogle + "')";
                cmd.Connection = sqlConnection1;

                sqlConnection1.Open();
                cmd.ExecuteNonQuery();
                sqlConnection1.Close();
            }
            catch(Exception ex)
            {
                log.Info("Error: " + ex.Message.ToString());
                return "Error" ;

            }


          

            return "ok";

           

        }


        #region CObject Visitor
        private static void ExtractText(CObject obj, StringBuilder target)
        {
            if (obj is CArray)
                ExtractText((CArray)obj, target);
            else if (obj is CComment)
                ExtractText((CComment)obj, target);
            else if (obj is CInteger)
                ExtractText((CInteger)obj, target);
            else if (obj is CName)
                ExtractText((CName)obj, target);
            else if (obj is CNumber)
                ExtractText((CNumber)obj, target);
            else if (obj is COperator)
                ExtractText((COperator)obj, target);
            else if (obj is CReal)
                ExtractText((CReal)obj, target);
            else if (obj is CSequence)
                ExtractText((CSequence)obj, target);
            else if (obj is CString)
                ExtractText((CString)obj, target);
            else
                throw new NotImplementedException(obj.GetType().AssemblyQualifiedName);
        }

        private static void ExtractText(CArray obj, StringBuilder target)
        {
            foreach (var element in obj)
            {
                ExtractText(element, target);
            }
        }
        private static void ExtractText(CComment obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CInteger obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CName obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CNumber obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(COperator obj, StringBuilder target)
        {
            if (obj.OpCode.OpCodeName == OpCodeName.Tj || obj.OpCode.OpCodeName == OpCodeName.TJ)
            {
                foreach (var element in obj.Operands)
                {
                    ExtractText(element, target);
                }
                target.Append(" ");
            }
        }
        private static void ExtractText(CReal obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CSequence obj, StringBuilder target)
        {
            foreach (var element in obj)
            {
                ExtractText(element, target);
            }
        }
        private static void ExtractText(CString obj, StringBuilder target)
        {
            target.Append(obj.Value);
        }


        #endregion

    }
}
