namespace AK.Examples.Rewards
{
	/// <summary>
	/// Stub CurrencyModel for the example.
	/// </summary>
	public class CurrencyModel
	{
		public long Amount;
		public void Add(int amount) => Amount += amount;

		public void Deduct(int costOptionAmount)
		{
			
		}
	}
}