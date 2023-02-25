using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandsLiftedApp.Extensions
{
    public static class EnumExtension
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self) => self?.Select((item, index) => (item, index)) ?? new List<(T, int)>();
        public static IEnumerable<(T item, int index)> WithIndex<T>(this ObservableCollection<T> self) => self?.Select((item, index) => (item, index)) ?? new List<(T, int)>();
    }
}
