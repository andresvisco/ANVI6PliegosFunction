using System;
using System.Collections.Generic;
using System.Text;

namespace 
    
    FunctionPDF3
{
    public class Pliego
    {
        public string IdPdf { get; set; }

        public int Pagina { get; set; }

        public int Bloque { get; set; }

        public string KeyPhrases { get; set; }

        public string KeyGoogle { get; set; }

        public string KeyGoogleClassify { get; set; }

        public Pliego() { }

        public Pliego(string idpdf, int pagina, int bloque, string key, string keyGoogle, string keyGoogleClassify)
        {
            IdPdf = idpdf;
            Pagina = pagina;
            Bloque = bloque;
            KeyPhrases = key;

            KeyGoogle = keyGoogle;
            KeyGoogleClassify = keyGoogleClassify;

        }
    }
}
