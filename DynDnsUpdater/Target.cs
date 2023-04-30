namespace DynDnsUpdater
{
    class Target
    {
		public string Name              { get; set; }
		public string Method		    { get; set; }
        public string DynDnsPassword	{ get; set; }
        public string DynDnsUsername	{ get; set; }
		public string Host              { get; set; }
		public string Request           { get; set; }
		public string Authentication    { get; set; }
		public string Username          { get; set; }
		public string Password          { get; set; }
		public string SubdomainlistFile { get; set; }
    }
}
