using System;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using System.Collections.Generic;
using Microsoft.Rest;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Azure.WebJobs.Host;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;

namespace azureFunctionPDF3
{
    public class AnalizadorTexto
    {
        public static string TextoTraducido = string.Empty;
        public static string APIKey = Environment.GetEnvironmentVariable("GoogleLanguageApi");

        public static string Clasificador(string textoGoogleClasificar, TraceWriter log)
        {
            textoGoogleClasificar = RemoveSpecialCharacters(textoGoogleClasificar);

            string textoTraducido = textoGoogleClasificar; //TraducirGoogle(textoGoogleClasificar).Replace("\"", "'");

            string entidadEncontrada = "";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://language.googleapis.com/v1beta2/documents:classifyText?key="+ APIKey.ToString());

                var DataAEnviar = "{\"document\":{\"type\":\"PLAIN_TEXT\",\"content\":\"" + textoTraducido.ToString() + "\"}}";
                var data = Encoding.ASCII.GetBytes(DataAEnviar);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = data.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                var response = (HttpWebResponse)request.GetResponse();
                StreamReader streamReaderClasificacion = new StreamReader(response.GetResponseStream());
                string responseClasificacion = streamReaderClasificacion.ReadToEnd();
                var jObjectClasificacion = JsonConvert.DeserializeObject<JObject>(responseClasificacion.ToString()).First;
                var cantidad = ((Newtonsoft.Json.Linq.JArray)jObjectClasificacion.First).Count;
                for (int i = 0; i < cantidad; i++)
                {
                    entidadEncontrada = entidadEncontrada + "\"" + jObjectClasificacion.First[i]["name"].ToString() + "\"" + ",";

                }

                var entidad = entidadEncontrada;

                if (entidadEncontrada != "")
                {
                    entidadEncontrada = entidadEncontrada.Substring(0, entidadEncontrada.Length - 1);
                }
                else
                {
                    entidadEncontrada = "Empty";
                }
                var inicioJson = "[";
                var finJson = "]";
                entidadEncontrada = inicioJson.ToString() + entidadEncontrada.ToString().PadRight(entidadEncontrada.Length - 1) + "]";


            }
            catch (Exception ex)
            {
                log.Info("Error en clasificador: " + ex.Message.ToString());
            }
            
            

            return entidadEncontrada.ToString();
        }

        public static string TraducirGoogle(string textoATraducir)
        {
            textoATraducir = RemoveSpecialCharacters(textoATraducir);
            object o = null;
            string cadena = "https://translation.googleapis.com/language/translate/v2?key=AIzaSyA5_dfj2UfIoUcpT_tft0otCijqf0FzGLs=en&format=text&q=" + textoATraducir.ToString();
            var requestTranslate = (HttpWebRequest)WebRequest.Create(cadena);
            requestTranslate.Method = "POST";
            requestTranslate.ContentType = "application/x-www-form-urlencoded";
            requestTranslate.GetRequestStreamAsync();
            var postDataTranslate = "";
            var datatranslate = Encoding.ASCII.GetBytes(postDataTranslate);
            HttpWebResponse webResponse;
            webResponse = (HttpWebResponse)requestTranslate.GetResponse();
            StreamReader streamReader = new StreamReader(webResponse.GetResponseStream());
            string responseTransalte = streamReader.ReadToEnd();
            var jObject = JsonConvert.DeserializeObject<JObject>(responseTransalte.ToString()).First.First;
            var textoTraducido = jObject["translations"][0]["translatedText"].ToString();

            TextoTraducido = textoTraducido.ToString();

            return textoTraducido.ToString();
        }

        public static string ProcesarGoogle(string textoGoogle, TraceWriter log)
        {
            string entidadEncontrada = "";
            string salience = "";

            try
            {
                textoGoogle = RemoveSpecialCharacters(textoGoogle);
                textoGoogle = textoGoogle.Replace("\"", "'");
                string textoTraducido = textoGoogle; // TextoTraducido.ToString().Replace("\"", "'");
                var request = (HttpWebRequest)WebRequest.Create("https://language.googleapis.com/v1beta2/documents:analyzeEntities?key=" + APIKey.ToString());

                var DataAEnviar = "{\"document\":{\"type\":\"PLAIN_TEXT\",\"content\":\"" + textoTraducido.ToString() + "\"}}";
                var data = Encoding.ASCII.GetBytes(DataAEnviar);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = data.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                var response = (HttpWebResponse)request.GetResponse();
                StreamReader streamReaderClasificacion = new StreamReader(response.GetResponseStream());
                string responseClasificacion = streamReaderClasificacion.ReadToEnd();
                var jObjectClasificacion = JsonConvert.DeserializeObject<JObject>(responseClasificacion.ToString()).First;
                var cantidad = ((Newtonsoft.Json.Linq.JArray)jObjectClasificacion.First).Count;
                for (int i = 0; i < cantidad; i++)
                {
                    entidadEncontrada = entidadEncontrada + "\"" + jObjectClasificacion.First[i]["name"].ToString() + "\"" + ",";
                    
                }

                //TODO: DEJAR CON CLASSIFY SOLAMENTE Y QUITAR TRADUCTOR Y QUITAR CORTANA

                entidadEncontrada = entidadEncontrada.Substring(0, entidadEncontrada.Length - 1);
                var inicioJson = "[";
                var finJson = "]";
                entidadEncontrada = inicioJson.ToString() + entidadEncontrada.ToString().PadRight(entidadEncontrada.Length - 1) + "]";

            }
            catch(Exception ex)
            {
                log.Info("Error en ProcesarGoogle: " + ex.Message.ToString());
            }
            


            return entidadEncontrada.ToString();
        }
        public static string AnalizarTexto(string textoPDF, string numPagina, TraceWriter log)
        {
            string result = string.Empty;
            var json = "";
            // Create a client.
            ITextAnalyticsAPI client = new TextAnalyticsAPI(new ApiKeyServiceClientCredentials());
            client.AzureRegion = AzureRegions.Southcentralus;

            KeyPhraseBatchResult result2;
            try
            {

                // Getting key-phrases
                textoPDF = RemoveSpecialCharacters(textoPDF);

                result2 = client.KeyPhrasesAsync(new MultiLanguageBatchInput(
                            new List<MultiLanguageInput>()
                            {
                          new MultiLanguageInput("es", numPagina, textoPDF)
                            })).Result;
                var TiempoEspera = System.Configuration.ConfigurationManager.AppSettings["CustomTrheadSleep"];
                Thread.Sleep(Convert.ToInt32(TiempoEspera));

                if (result2.Documents.Count > 0)
                {
                    json = string.Join(",", result2.Documents[0].KeyPhrases);
                }
            }
            catch (Exception ex)
            {
                log.Info(textoPDF);
            }

            return json;

        }


        public static string AnalizarTextoJson(string textoPDF, string numPagina, TraceWriter log)
        {
            //devuelve un json con las keyphrases
            string result = string.Empty;
            var json = "[\"\"]";
            // Create a client.
            ITextAnalyticsAPI client = new TextAnalyticsAPI(new ApiKeyServiceClientCredentials());
            client.AzureRegion = AzureRegions.Southcentralus;
            
            KeyPhraseBatchResult result2;
            try
            {
               
                // Getting key-phrases
                textoPDF = RemoveSpecialCharacters(textoPDF);

                result2 = client.KeyPhrasesAsync(new MultiLanguageBatchInput(
                            new List<MultiLanguageInput>()
                            {
                          new MultiLanguageInput("es", numPagina, textoPDF)
                            })).Result;
                Thread.Sleep(1000);

                if (result2.Documents.Count > 0)
                {
                    json = RemoveSpecialCharacters(JsonConvert.SerializeObject(result2.Documents[0].KeyPhrases));
                }
            }
            catch (Exception ex)
            {
                log.Info(textoPDF);
            }
            
            return json;

        }

        public static string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            str = str.Replace(".", "");
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_' || c == ' ' || c == 'á' || c == 'é' || c == 'í' || c == 'ó' || c == 'ú' || c == '{' || c == '}' || c == '[' || c == ']' || c == '"' || c == ',')
                {
                    
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    class ApiKeyServiceClientCredentials : ServiceClientCredentials
    {
        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //request.Headers.Add("Ocp-Apim-Subscription-Key", "e073ce96819c428d8d7f373c26a6796c");
            request.Headers.Add("Ocp-Apim-Subscription-Key", "33d7cd69f0d94e579ba37e52f1327b3f");
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
