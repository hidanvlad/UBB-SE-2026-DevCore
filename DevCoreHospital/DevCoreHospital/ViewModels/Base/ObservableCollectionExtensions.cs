using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DevCoreHospital.ViewModels.Base
{
    public static class ObservableCollectionExtensions
    {
        public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> source)
        {
            collection.Clear();
            foreach (var item in source)
            {
                collection.Add(item);
            }
        }
    }
}
