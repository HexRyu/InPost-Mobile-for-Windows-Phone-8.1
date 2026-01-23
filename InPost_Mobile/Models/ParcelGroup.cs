using System.Collections.Generic;
using InPost_Mobile.Models;

namespace InPost_Mobile.Models
{
    public class ParcelGroup : List<ParcelItem>
    {
        public string Key { get; set; }

        public ParcelGroup(string key, IEnumerable<ParcelItem> items) : base(items)
        {
            Key = key;
        }
    }
}
