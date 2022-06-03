using HandsLiftedApp.Data.Models.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Models
{
    public class Playlist
    {
        public Dictionary<String, String> Meta { get; set; } = new Dictionary<String, String>();
        public List<Item> Items { get; set; }= new List<Item>();
    }
}
