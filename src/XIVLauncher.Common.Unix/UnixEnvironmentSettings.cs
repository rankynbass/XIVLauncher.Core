namespace XIVLauncher.Common.Unix
{
    public static class UnixEnvironmentSettings
    {
        public static bool IsWineD3D => CheckEnvBool("XL_FORCE_WINED3D");

        private static bool CheckEnvBool(string var)
        {
            // Check if ENV variable is set. Accepts 1, true, on, yes, and y.
            var = (System.Environment.GetEnvironmentVariable(var) ?? "false").ToLower();
            return (var.Equals("1") || var.Equals("true") || var.Equals("on") || var.Equals("yes") || var.Equals("y"));
        }
    }
}