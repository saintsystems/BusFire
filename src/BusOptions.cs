namespace BusFire
{
	public class BusOptions
	{
		/// <summary>The Hangfire queues this server should process. Used by <c>AddBusFireServer</c>.</summary>
		public string[] Queues { get; set; } = {"default"};
	}
}
