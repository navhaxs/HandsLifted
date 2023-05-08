using Newtonsoft.Json;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;

namespace HandsLiftedApp.Utils
{
    public class NewtonsoftJsonSuspensionDriver<T> : ISuspensionDriver where T : new()
    {
        private readonly string _file;
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public NewtonsoftJsonSuspensionDriver(string file) => _file = file;

        public IObservable<Unit> InvalidateState()
        {
            if (File.Exists(_file))
                File.Delete(_file);
            return Observable.Return(Unit.Default);
        }

        public IObservable<object> LoadState()
        {
            if (!File.Exists(_file))
            {
                return Observable.Return((object)new T());
            }
            var lines = File.ReadAllText(_file);
            var state = JsonConvert.DeserializeObject<object>(lines, _settings);
            var result = Observable.Return(state);
            Log.Information("Loaded appstate.json");
            return result;
        }

        public IObservable<Unit> SaveState(object state)
        {
            var lines = JsonConvert.SerializeObject(state, _settings);
            File.WriteAllText(_file, lines);
            Log.Information("Saved appstate.json");
            return Observable.Return(Unit.Default);
        }
    }
}
