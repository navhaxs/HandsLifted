using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml.Serialization;
using HandsLiftedApp.Common.JsonConverter;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Data.Models.Types;
using Newtonsoft.Json;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core
{
    public class XmlSuspensionDriver<T> : ISuspensionDriver where T : new()
    {
        private readonly string _file;
        // private readonly JsonSerializerSettings _settings = new()
        // {
        //     TypeNameHandling = TypeNameHandling.All,
        //     Converters = {new KeysJsonConverter(typeof(XmlColor), typeof(XmlFontFamily))}
        // };

        public XmlSuspensionDriver(string file) => _file = file;

        public IObservable<Unit> InvalidateState()
        {
            if (File.Exists(_file))
                File.Delete(_file);
            return Observable.Return(Unit.Default);
        }

        public IObservable<object> LoadState()
        {
            if (File.Exists(_file))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(CustomSlide));
                using (FileStream stream = new FileStream(_file, FileMode.Open))
                {
                    var x = serializer.Deserialize(stream);
                    if (x != null)
                    {
                        CustomSlide deserialized = (CustomSlide)x;
                        Log.Information("Loaded appstate");
                        var result = Observable.Return(deserialized);
                        return result;
                    }
                }
            }

            return Observable.Return((object)new T());
        }

        public IObservable<Unit> SaveState(object state)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(CustomSlide));

            MemoryStream memoryStream = new MemoryStream();
            serializer.Serialize(memoryStream, state);

            // only once all serialization is OK, now write to disk
            memoryStream.Position = 0;
            using (FileStream fileStream = new FileStream(_file, FileMode.Create))
            {
                memoryStream.CopyTo(fileStream);
            }

            Log.Information("Saved appstate");
            return Observable.Return(Unit.Default);
        }
    }
}