using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CLIT.OcrMicroOrchestration.Infrastructure.Helpers
{
    public static class XmlSerializerHelper
    {
        public static TType Deserialize<TType>(string xmlString)
        {
            using (var reader = new StringReader(xmlString))
            {
                if (reader != null)
                {
                    XmlSerializer xmlSerilaizer = new XmlSerializer(typeof(TType));
                    var _object = xmlSerilaizer.Deserialize(reader);
                    return (TType)((_object != null) ? _object : throw new InvalidOperationException("Błąd Deserializacji na obiekt: " + nameof(TType)));
                }
                throw new ArgumentNullException($"Błąd Deserializacji [{xmlString}] na obiekt: {nameof(TType)}");
            }
        }

        public static string Serialize<TType>(TType _object)
        {

            if (_object != null)
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(TType));
                using (StringWriter textWriter = new StringWriter())
                {
                    xmlSerializer.Serialize(textWriter, _object);
                    return textWriter.ToString();
                }
            }
            throw new ArgumentNullException($"Błąd serializacji obiektu: {nameof(TType)}");

        }


    }
}
