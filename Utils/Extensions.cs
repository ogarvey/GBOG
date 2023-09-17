using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Utils
{
  public static class Extensions
  {
    public static Dictionary<T, U> AddMultiple<T, U>(this Dictionary<T, U> destination, Dictionary<T, U> source)
      where T : notnull
    {
      var data = destination.Union(source)
          .GroupBy(kvp => kvp.Key)
          .Select(grp => grp.First())
          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
      return data;
    }

    public static bool TestBit(this byte b, int bit)
    {
      return (b & (1 << bit)) != 0;
    }
  }
}
