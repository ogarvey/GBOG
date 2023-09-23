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
    
		public static byte GetLSB(ushort inputByte)
		{
			// Use a bitwise AND operation with 1 to extract the LSB.
			byte lsb = (byte)(inputByte & 0b_0000_0000_1111_1111);
			return lsb;
		}
		public static byte BitReset(byte value, int bitPosition)
		{
			return (byte)(value & ~(0b_0000_0001 << bitPosition));
		}
		public static bool IsBitSet(byte value, int bitPosition)
		{
			return ((value >> bitPosition) & 0b_0000_0001) == 1;
		}
	}
}
