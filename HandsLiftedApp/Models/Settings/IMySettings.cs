using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.Settings {
    public interface IMySettings {
        string AuthClientId { get; }

        string AuthClientSecret { get; }
    }
}
