namespace BusFire
{
	public class BusOptions
	{
		public string ConnectionStringOrName { get; set; } = "BusFire";

		public string SchemaName { get; set; } = "HangFire";

		public string DashboardPath { get; set; } = "/hangfire";

		public string[] Queues { get; set; } = {"default"};
	}
}
