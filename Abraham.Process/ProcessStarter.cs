using System;
using System.Diagnostics;
using System.Text;

namespace Abraham.ProcessIO
{
	public class ProcessStarter
	{
		public bool RedirectStdError { get; set; } = true;

		private int _standardWaitTimeoutInSeconds;

		public ProcessStarter()
		{
            _standardWaitTimeoutInSeconds = 30;
		}

		public ProcessStarter(int standardWaitTimeoutInSeconds)
		{
            _standardWaitTimeoutInSeconds = standardWaitTimeoutInSeconds;
		}

        public string CallProcessAndReturnConsoleOutput(string filename, string arguments, int timeoutInSeconds = 0)
        {
            if (timeoutInSeconds == 0)
                timeoutInSeconds = _standardWaitTimeoutInSeconds;

            var output = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo.FileName = filename;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = RedirectStdError;
                p.Start();

                int MaxLineCount = 10000;
                DateTime Timeout = DateTime.Now.AddSeconds(timeoutInSeconds);
                while (!p.StandardOutput.EndOfStream)
                {
                    output.Append(p.StandardOutput.ReadLine() + "\n");
                    if (RedirectStdError)
                        output.Append(p.StandardError.ReadLine() + "\n");
                    MaxLineCount--;
                    if (MaxLineCount <= 0 || DateTime.Now > Timeout)
                        break; // prevent from looping endless
                }
                if (DateTime.Now > Timeout)
                {
                    p.Kill();
                    throw new Exception($"Error, possible endless loop! killing the subprocess after {timeoutInSeconds} seconds");
                }
                if (MaxLineCount <= 0)
                {
                    p.Kill();
                    throw new Exception("Error, possible endless loop! killing the subprocess after reading 1000 lines");
                }

                bool ProcessHasExited = p.WaitForExit(timeoutInSeconds * 1000);
                if (!ProcessHasExited)
                    throw new Exception($"Error in Method CallProcessAndReturnConsoleOutput! Process hasn't exited after {timeoutInSeconds} seconds!");
            }
            
            return output.ToString();
        }
	}
}
